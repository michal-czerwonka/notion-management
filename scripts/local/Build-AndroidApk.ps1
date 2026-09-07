[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('LocalEmulator', 'DevelopmentPhone')]
    [string]$Target
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$appPath = Join-Path $repositoryRoot 'NotionManagementApp'
$environmentFile = switch ($Target) {
    'LocalEmulator' { '.env.local-emulator' }
    'DevelopmentPhone' { '.env.development-phone' }
}
$outputDirectory = Join-Path $repositoryRoot 'artifacts\android'
$sourceApkPath = Join-Path $appPath 'android\app\build\outputs\apk\debug\app-debug.apk'
$outputApkPath = Join-Path $outputDirectory "NotionManagementApp-$($Target.ToLowerInvariant())-debug.apk"

function Require-Command {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Ensure-JavaAvailable {
    if (-not (Get-Command 'java' -ErrorAction SilentlyContinue)) {
        if ([string]::IsNullOrWhiteSpace($env:JAVA_HOME)) {
            throw 'Java was not found. Set JAVA_HOME to a JDK 21 installation.'
        }

        $javaBinPath = Join-Path $env:JAVA_HOME 'bin'
        if (-not (Test-Path (Join-Path $javaBinPath 'java.exe'))) {
            throw "JAVA_HOME does not contain java.exe: $env:JAVA_HOME"
        }

        $env:Path = "$javaBinPath;$env:Path"
    }

    $javaVersionOutput = (& cmd.exe /c 'java -version 2>&1') -join "`n"
    if ($javaVersionOutput -match 'version "(\d+)') {
        $javaMajorVersion = [int]$Matches[1]
        if ($javaMajorVersion -eq 21) {
            return
        }

        throw "JDK 21 is required, but Java $javaMajorVersion is active. Set JAVA_HOME to a JDK 21 installation."
    }

    throw 'Could not determine the active Java version. Set JAVA_HOME to a JDK 21 installation.'
}

Require-Command 'npm'
Ensure-JavaAvailable

if (-not (Test-Path (Join-Path $appPath $environmentFile))) {
    throw "Missing $appPath\$environmentFile. Create it from $environmentFile.example and set VITE_INBOX_API_URL."
}

Push-Location $appPath
try {
    if (-not (Test-Path 'node_modules\.bin\tsc.cmd')) {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }

    $viteMode = $environmentFile.Substring(5)
    npm run build -- --mode $viteMode
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }

    npm exec cap sync android
    if ($LASTEXITCODE -ne 0) { throw 'Capacitor sync failed.' }

    Push-Location 'android'
    try {
        & cmd.exe /c .\gradlew.bat assembleDebug
        if ($LASTEXITCODE -ne 0) { throw 'Android debug APK build failed.' }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $sourceApkPath)) {
    throw "Expected APK was not created: $sourceApkPath"
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceApkPath -Destination $outputApkPath -Force
Write-Host "APK created: $outputApkPath"
Start-Process -FilePath 'explorer.exe' -ArgumentList "/select,`"$outputApkPath`""
