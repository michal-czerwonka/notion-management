[CmdletBinding()]
param()

$buildScriptPath = Join-Path $PSScriptRoot 'Build-AndroidApk.ps1'

try {
    & $buildScriptPath -Target LocalEmulator
    Write-Host 'Local emulator APK build completed.' -ForegroundColor Green
}
catch {
    Write-Host "Local emulator APK build failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Read-Host 'Press Enter to close this window'
}
