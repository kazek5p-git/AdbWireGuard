[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$delegate = Join-Path $scriptRoot "Start-ADB-WG-Admin.ps1"

if (-not (Test-Path $delegate)) {
    throw "Nie znaleziono pliku Start-ADB-WG-Admin.ps1."
}

& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $delegate
exit $LASTEXITCODE
