[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$functionAppPath = Join-Path $repositoryRoot 'NotionManagementFunctionApp'
$localDataPath = Join-Path $repositoryRoot '.local\azurite'
$logFilePath = Join-Path $localDataPath 'azurite.log'
$azuriteProcess = $null

. (Join-Path $PSScriptRoot 'shared\LocalTools.ps1')

try {
    Require-Command 'npm'
    Require-Command 'func'
    Require-Command 'dotnet'
    $azuriteCommand = Resolve-AzuriteCommand

    if (-not (Test-Path (Join-Path $functionAppPath 'local.settings.json'))) {
        throw "Missing $functionAppPath\local.settings.json. Copy local.settings.json.example and fill its required settings."
    }

    New-Item -ItemType Directory -Path $localDataPath -Force | Out-Null

    if (-not (Test-ListeningPort -Port 10000)) {
        $azuriteProcess = Start-Process -FilePath $azuriteCommand -ArgumentList @('--location', $localDataPath, '--debug', $logFilePath) -WindowStyle Hidden -PassThru
        Start-Sleep -Milliseconds 750

        if (-not (Test-ListeningPort -Port 10000)) {
            throw "Azurite did not start on port 10000. Check ports 10000-10002 and $logFilePath."
        }

        Write-Host "Started Azurite (PID $($azuriteProcess.Id)). Log: $logFilePath"
    }
    else {
        Write-Host 'Azurite is already listening on port 10000.'
    }

    Write-Host 'Starting Function App at http://127.0.0.1:7071. Press Ctrl+C to stop the Function App.'
    Push-Location $functionAppPath
    try {
        func start
        if ($LASTEXITCODE -ne 0) {
            throw "Function App stopped with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Host "Local Function App failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($null -ne $azuriteProcess -and -not $azuriteProcess.HasExited) {
        Stop-Process -Id $azuriteProcess.Id -ErrorAction SilentlyContinue
        Write-Host "Stopped Azurite (PID $($azuriteProcess.Id))."
    }

    Read-Host 'Press Enter to close this window'
}
