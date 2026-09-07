[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$functionAppPath = Join-Path $repositoryRoot 'NotionManagementFunctionApp'
$localDataPath = Join-Path $repositoryRoot '.local\azurite'
$pidFilePath = Join-Path $localDataPath 'azurite.pid'
$logFilePath = Join-Path $localDataPath 'azurite.log'

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Resolve-AzuriteCommand {
    $azuriteCommand = Get-Command 'azurite.cmd' -ErrorAction SilentlyContinue
    if ($null -ne $azuriteCommand) {
        return $azuriteCommand.Source
    }

    $npmPrefix = (& npm prefix -g).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not determine the global npm directory for Azurite.'
    }

    $azuriteCommandPath = Join-Path $npmPrefix 'azurite.cmd'
    if (-not (Test-Path $azuriteCommandPath)) {
        throw "Azurite was not found. Install it with 'npm install --global azurite'."
    }

    return $azuriteCommandPath
}

function Test-ListeningPort {
    param([Parameter(Mandatory = $true)][int]$Port)

    return $null -ne (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
}

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
        Set-Content -LiteralPath $pidFilePath -Value $azuriteProcess.Id -NoNewline
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
    Read-Host 'Press Enter to close this window'
}
