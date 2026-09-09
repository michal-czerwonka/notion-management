[CmdletBinding()]
param()

$buildScriptPath = Join-Path $PSScriptRoot 'shared\Build-AndroidApk.ps1'

try {
    & $buildScriptPath -Target DevelopmentPhone
    Write-Host 'Development phone APK build completed.' -ForegroundColor Green
}
catch {
    Write-Host "Development phone APK build failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Read-Host 'Press Enter to close this window'
}
