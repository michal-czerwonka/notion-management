Set-StrictMode -Version Latest

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
