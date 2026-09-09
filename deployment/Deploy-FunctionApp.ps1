[CmdletBinding()]
param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot 'config\development.json'),
    [switch]$SkipAzLogin
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "Required command '$Name' was not found on PATH." }
}

function Read-Secret([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
    $secureValue = Read-Host "Enter $Name" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureValue)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Invoke-AzCli([string[]]$Arguments) {
    & az @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI failed: $($Arguments -join ' ')" }
}

Require-Command 'az'
Require-Command 'dotnet'
$config = Get-Content -Raw (Resolve-Path $ConfigurationPath) | ConvertFrom-Json
$notionToken = Read-Secret 'NOTION_TOKEN'
$ntfyTopic = Read-Secret 'NTFY_TOPIC'

if (-not $SkipAzLogin) { Invoke-AzCli @('login') }
Invoke-AzCli @('account', 'set', '--subscription', $config.azure.subscriptionId)

$settings = @(
    "FUNCTIONS_WORKER_RUNTIME=$($config.functionApp.runtime)", "FUNCTIONS_EXTENSION_VERSION=$($config.functionApp.functionsExtensionVersion)",
    "WEBSITE_TIME_ZONE=$($config.functionApp.timeZone)", "Scheduler__Schedule=$($config.functionApp.schedulerSchedule)",
    "Scheduler__TimeZone=$($config.functionApp.schedulerTimeZone)", "Tasks__FilePath=$($config.functionApp.tasksFilePath)",
    "Notifications__Schedule=$($config.functionApp.notificationsSchedule)", "Notion__DataSourceId=$($config.functionApp.notionDataSourceId)",
    "Notion__InboxDataSourceId=$($config.functionApp.notionInboxDataSourceId)", "Notion__TodayViewId=$($config.functionApp.notionTodayViewId)",
    "Notion__TodayViewName=$($config.functionApp.notionTodayViewName)", "Notion__Token=$notionToken",
    "Ntfy__BaseUrl=$($config.functionApp.ntfyBaseUrl)", "Ntfy__Topic=$ntfyTopic"
)
Invoke-AzCli (@('functionapp', 'config', 'appsettings', 'set', '--resource-group', $config.azure.resourceGroupName, '--name', $config.azure.functionAppName, '--settings') + $settings + @('--output', 'none'))
foreach ($origin in $config.functionApp.corsAllowedOrigins) {
    Invoke-AzCli @('functionapp', 'cors', 'add', '--resource-group', $config.azure.resourceGroupName, '--name', $config.azure.functionAppName, '--allowed-origins', $origin, '--output', 'none')
}

$publishDirectory = Join-Path $env:TEMP "notion-management-function-app-$PID"
$zipPath = "$publishDirectory.zip"
try {
    dotnet publish (Join-Path $PSScriptRoot '..\NotionManagementFunctionApp\NotionManagementFunctionApp.csproj') --configuration Release --output $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Function App publish failed.' }
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -Force
    Invoke-AzCli @('functionapp', 'deployment', 'source', 'config-zip', '--resource-group', $config.azure.resourceGroupName, '--name', $config.azure.functionAppName, '--src', $zipPath, '--output', 'none')
}
finally {
    Remove-Item $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
}

Write-Host 'Deployment finished.'
