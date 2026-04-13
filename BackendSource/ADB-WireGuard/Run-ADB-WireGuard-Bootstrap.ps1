[CmdletBinding()]
param(
    [string]$PackageRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$delegate = Join-Path $PackageRoot "Run-ADB-WG-Task.ps1"
if (-not (Test-Path $delegate)) {
    throw "Nie znaleziono pliku Run-ADB-WG-Task.ps1."
}

& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $delegate -PackageRoot $PackageRoot
exit $LASTEXITCODE
