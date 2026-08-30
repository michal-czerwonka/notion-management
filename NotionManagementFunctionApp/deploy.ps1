param(
    [switch]$SkipAzLogin
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------------
# Deployment configuration
# -----------------------------------------------------------------------------
# Fill these values before running the script. Do not commit real secrets.

$SubscriptionId = "a9d08d80-c04e-4928-8071-a3a600542f1f" # Optional. Example: "00000000-0000-0000-0000-000000000000"
$ResourceGroupName = "notion-management-rg"
$Location = "westeurope"

$FunctionAppName = "notion-management-func" # Must be globally unique under azurewebsites.net.
$StorageAccountName = "notionmanagementrgb0f3" # Must be globally unique, 3-24 chars, lowercase letters and numbers only. Example: "notionmgmtst001"

$SchedulerSchedule = "0 0 6 * * *"
$SchedulerTimeZone = "UTC"
$TasksFilePath = "CreateNotionTasks/tasks.json"
$NotionDataSourceId = "34c8bc09-19d3-80b2-9e8c-000b798e750e"

# Prefer setting NOTION_TOKEN in the current PowerShell session instead of writing
# the token into this file:
#   $env:NOTION_TOKEN = "secret_xxx"
$NotionToken = $env:NOTION_TOKEN

# Windows Consumption keeps costs low for this small private helper. If Azure CLI
# rejects .NET 10 for Windows Consumption in your region/subscription, use Flex
# Consumption as a follow-up deployment path.
$Runtime = "dotnet-isolated"
$RuntimeVersion = "10"
$FunctionsVersion = "4"
$OsType = "Windows"

$ProjectPath = $PSScriptRoot

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Require-ConfigValue {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [AllowEmptyString()][string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Configuration value '$Name' must be set before deployment."
    }
}

function Convert-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][securestring]$SecureString)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

Require-Command "az"
Require-Command "func"
Require-Command "dotnet"

Require-ConfigValue "ResourceGroupName" $ResourceGroupName
Require-ConfigValue "Location" $Location
Require-ConfigValue "FunctionAppName" $FunctionAppName
Require-ConfigValue "StorageAccountName" $StorageAccountName
Require-ConfigValue "NotionDataSourceId" $NotionDataSourceId

if ([string]::IsNullOrWhiteSpace($NotionToken)) {
    $secureToken = Read-Host "Enter Notion token" -AsSecureString
    $NotionToken = Convert-SecureStringToPlainText $secureToken
}

Require-ConfigValue "NotionToken" $NotionToken

if (-not $SkipAzLogin) {
    az login
}

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    az account set --subscription $SubscriptionId
}

Write-Host "Creating or updating resource group '$ResourceGroupName' in '$Location'..."
az group create `
    --name $ResourceGroupName `
    --location $Location `
    --output none

Write-Host "Creating storage account '$StorageAccountName' if needed..."
$storageAccountId = az storage account show `
    --name $StorageAccountName `
    --resource-group $ResourceGroupName `
    --query "id" `
    --output tsv 2>$null

if ([string]::IsNullOrWhiteSpace($storageAccountId)) {
    az storage account create `
        --name $StorageAccountName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --https-only true `
        --min-tls-version TLS1_2 `
        --allow-blob-public-access false `
        --output none
}
else {
    Write-Host "Storage account '$StorageAccountName' already exists in resource group '$ResourceGroupName'. Reusing it."
}

Write-Host "Creating Function App '$FunctionAppName' if needed..."
$functionAppId = az functionapp show `
    --name $FunctionAppName `
    --resource-group $ResourceGroupName `
    --query "id" `
    --output tsv 2>$null

if ([string]::IsNullOrWhiteSpace($functionAppId)) {
    az functionapp create `
        --name $FunctionAppName `
        --resource-group $ResourceGroupName `
        --consumption-plan-location $Location `
        --storage-account $StorageAccountName `
        --functions-version $FunctionsVersion `
        --runtime $Runtime `
        --runtime-version $RuntimeVersion `
        --os-type $OsType `
        --output none
}
else {
    Write-Host "Function App '$FunctionAppName' already exists. Reusing it."
}

Write-Host "Configuring Function App runtime and application settings..."
az functionapp config set `
    --name $FunctionAppName `
    --resource-group $ResourceGroupName `
    --net-framework-version "v10.0" `
    --output none

az functionapp config appsettings set `
    --name $FunctionAppName `
    --resource-group $ResourceGroupName `
    --settings `
        "FUNCTIONS_WORKER_RUNTIME=$Runtime" `
        "FUNCTIONS_EXTENSION_VERSION=~$FunctionsVersion" `
        "Scheduler__Schedule=$SchedulerSchedule" `
        "Scheduler__TimeZone=$SchedulerTimeZone" `
        "Tasks__FilePath=$TasksFilePath" `
        "Notion__DataSourceId=$NotionDataSourceId" `
        "Notion__Token=$NotionToken" `
    --output none

Write-Host "Building solution..."
dotnet build (Join-Path $PSScriptRoot "..\NotionManagement.sln") --configuration Release

Write-Host "Publishing Function App..."
Push-Location $ProjectPath
try {
    func azure functionapp publish $FunctionAppName --dotnet-isolated
}
finally {
    Pop-Location
}

Write-Host "Deployment finished."
Write-Host "HTTP endpoint: https://$FunctionAppName.azurewebsites.net/api/run"
Write-Host "Get the x-functions-key from Azure Portal: Function App -> Functions -> CreateNotionTasksFunctionHttp -> Function Keys."