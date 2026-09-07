[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$pidFilePath = Join-Path $repositoryRoot '.local\azurite\azurite.pid'

try {
    if (-not (Test-Path $pidFilePath)) {
        Write-Host 'No Azurite process started by Start-LocalFunctionApp.ps1 was found.'
        return
    }

    $processId = Get-Content -LiteralPath $pidFilePath -Raw
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        Stop-Process -Id $processId
        Write-Host "Stopped Azurite (PID $processId)."
    }
    else {
        Write-Host "Azurite process $processId is no longer running."
    }

    Remove-Item -LiteralPath $pidFilePath -Force
}
catch {
    Write-Host "Stopping Azurite failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Read-Host 'Press Enter to close this window'
}
