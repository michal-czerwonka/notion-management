param(
    [switch]$SkipAzLogin
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# -----------------------------------------------------------------------------
# Deployment configuration
# -----------------------------------------------------------------------------
# Fill these values before running the script. Do not commit real secrets.
# Azure resources must already exist. This script does not create infrastructure.

$SubscriptionId = "a9d08d80-c04e-4928-8071-a3a600542f1f" # Optional. Example: "00000000-0000-0000-0000-000000000000"
$ResourceGroupName = "notion-management-rg"

$FunctionAppName = "notion-management-func"
$ApplicationInsightsName = "notion-management-ai"

$SchedulerSchedule = "0 0 6 * * *"
$SchedulerTimeZone = "UTC"
$TasksFilePath = "CreateNotionTasks/tasks.json"
$NotificationsSchedule = "0 0 18,23 * * *"
$FunctionAppTimeZone = "Central European Standard Time" # Europe/Warsaw for Windows Function Apps.
$NotionDataSourceId = "34c8bc09-19d3-80b2-9e8c-000b798e750e"
$NotionTodayViewId = "34e8bc0919d380c595f1000c54fb8ad5"
$NotionTodayViewName = "Na dzisiaj"
$NtfyBaseUrl = "https://ntfy.sh"
$NtfyTopic = "CHPqQe5yp12AJiv1" # Fill with your private, hard-to-guess ntfy topic name.

# Prefer setting secrets in the current PowerShell session instead of writing them
# into this file:
#   $env:NOTION_TOKEN = "secret_xxx"
$NotionToken = $env:NOTION_TOKEN

$Runtime = "dotnet-isolated"
$FunctionsVersion = "4"
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

function Invoke-AzCli {
    param(
        [Parameter(Mandatory = $true)][string]$Step,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Step..."
    $output = & az @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI step failed: $Step"
    }

    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Step finished."

    return $output
}

Require-Command "az"
Require-Command "func"
Require-Command "dotnet"

Require-ConfigValue "ResourceGroupName" $ResourceGroupName
Require-ConfigValue "FunctionAppName" $FunctionAppName
Require-ConfigValue "ApplicationInsightsName" $ApplicationInsightsName
Require-ConfigValue "NotionDataSourceId" $NotionDataSourceId
Require-ConfigValue "NotionTodayViewId" $NotionTodayViewId
Require-ConfigValue "NotionTodayViewName" $NotionTodayViewName
Require-ConfigValue "NtfyBaseUrl" $NtfyBaseUrl
Require-ConfigValue "NtfyTopic" $NtfyTopic
Require-ConfigValue "FunctionAppTimeZone" $FunctionAppTimeZone

if ([string]::IsNullOrWhiteSpace($NotionToken)) {
    $secureToken = Read-Host "Enter Notion token" -AsSecureString
    $NotionToken = Convert-SecureStringToPlainText $secureToken
}

Require-ConfigValue "NotionToken" $NotionToken

if (-not $SkipAzLogin) {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Starting Azure login..."
    az login
    if ($LASTEXITCODE -ne 0) {
        throw "Azure login failed."
    }
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Azure login finished."
}

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    Invoke-AzCli `
        -Step "Selecting Azure subscription" `
        -Arguments @("account", "set", "--subscription", $SubscriptionId)
}

$functionAppId = Invoke-AzCli `
    -Step "Checking existing Function App '$FunctionAppName'" `
    -Arguments @(
        "functionapp", "show",
        "--name", $FunctionAppName,
        "--resource-group", $ResourceGroupName,
        "--query", "id",
        "--output", "tsv"
    )

if ([string]::IsNullOrWhiteSpace($functionAppId)) {
    throw "Function App '$FunctionAppName' was not found in resource group '$ResourceGroupName'. Create it manually before running this script."
}

$applicationInsightsConnectionString = Invoke-AzCli `
    -Step "Reading existing Application Insights '$ApplicationInsightsName' connection string" `
    -Arguments @(
        "resource", "show",
        "--name", $ApplicationInsightsName,
        "--resource-group", $ResourceGroupName,
        "--resource-type", "Microsoft.Insights/components",
        "--query", "properties.ConnectionString",
        "--output", "tsv"
    )

if ([string]::IsNullOrWhiteSpace($applicationInsightsConnectionString)) {
    throw "Application Insights '$ApplicationInsightsName' was not found in resource group '$ResourceGroupName' or does not expose a connection string. Create it manually before running this script."
}

Invoke-AzCli `
    -Step "Configuring Function App application settings" `
    -Arguments @(
        "functionapp", "config", "appsettings", "set",
        "--name", $FunctionAppName,
        "--resource-group", $ResourceGroupName,
        "--settings",
        "FUNCTIONS_WORKER_RUNTIME=$Runtime",
        "FUNCTIONS_EXTENSION_VERSION=~$FunctionsVersion",
        "WEBSITE_TIME_ZONE=$FunctionAppTimeZone",
        "APPLICATIONINSIGHTS_CONNECTION_STRING=$applicationInsightsConnectionString",
        "Scheduler__Schedule=$SchedulerSchedule",
        "Scheduler__TimeZone=$SchedulerTimeZone",
        "Tasks__FilePath=$TasksFilePath",
        "Notifications__Schedule=$NotificationsSchedule",
        "Notion__DataSourceId=$NotionDataSourceId",
        "Notion__TodayViewId=$NotionTodayViewId",
        "Notion__TodayViewName=$NotionTodayViewName",
        "Notion__Token=$NotionToken",
        "Ntfy__BaseUrl=$NtfyBaseUrl",
        "Ntfy__Topic=$NtfyTopic",
        "--output", "none"
    )

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Building solution..."
dotnet build (Join-Path $PSScriptRoot "..\NotionManagement.sln") --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Build finished."

Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Publishing Function App..."
Push-Location $ProjectPath
try {
    func azure functionapp publish $FunctionAppName --dotnet-isolated
    if ($LASTEXITCODE -ne 0) {
        throw "Function App publish failed."
    }
}
finally {
    Pop-Location
}
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Publish finished."

Write-Host "Deployment finished."
Write-Host "Manual create endpoint: https://$FunctionAppName.azurewebsites.net/api/run"
Write-Host "Manual notifications endpoint: https://$FunctionAppName.azurewebsites.net/api/notifications/run"
Write-Host "Get the x-functions-key from Azure Portal: Function App -> Functions -> selected HTTP function -> Function Keys."
