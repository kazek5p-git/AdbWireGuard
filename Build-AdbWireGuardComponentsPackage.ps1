[CmdletBinding()]
param(
    [string]$SourceRoot = "",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $PSScriptRoot "BackendSource\ADB-WireGuard"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $PSScriptRoot "release"
}

$componentFiles = @(
    "1-Start-ADB-Server-Over-WireGuard.ps1",
    "1-Start-ADB-Server-Over-WireGuard.bat",
    "2-Run-Remote-ADB-Command.ps1",
    "2-Run-Remote-ADB-Command.bat",
    "3-Stop-ADB-Server-Over-WireGuard.ps1",
    "3-Stop-ADB-Server-Over-WireGuard.bat",
    "4-Interactive-Remote-ADB.bat",
    "5-Start-ADB-Server-As-Admin.ps1",
    "5-Start-ADB-Server-As-Admin.bat",
    "5-Start-ADB-Server-As-Admin.vbs",
    "6-Remote-ADB-Devices.bat",
    "7-Remote-ADB-Logcat.bat",
    "8-Remote-ADB-Device-Info.bat",
    "9-Remote-ADB-Shell.bat",
    "10-Run-From-Admin-Terminal.bat",
    "Invoke-ADB-WG-Wrapper.ps1",
    "Run-ADB-WG-Task.ps1",
    "Run-ADB-WireGuard-Bootstrap.ps1",
    "Start-ADB-WG-Admin.bat",
    "Start-ADB-WG-Admin.ps1",
    "README-PL.txt"
)

$componentDirectories = @(
    "platform-tools"
)

if (-not (Test-Path $SourceRoot)) {
    throw "Nie znaleziono katalogu zrodlowego: $SourceRoot"
}

$null = New-Item -ItemType Directory -Force -Path $OutputRoot

$stagingRoot = Join-Path $OutputRoot "adb-wireguard-components"
$zipPath = Join-Path $OutputRoot "adb-wireguard-components.zip"
$manifestPath = Join-Path $stagingRoot "PACKAGE-INFO.txt"

Remove-Item -Recurse -Force $stagingRoot -ErrorAction SilentlyContinue
Remove-Item -Force $zipPath -ErrorAction SilentlyContinue

$null = New-Item -ItemType Directory -Force -Path $stagingRoot

foreach ($fileName in $componentFiles) {
    $sourcePath = Join-Path $SourceRoot $fileName
    if (-not (Test-Path $sourcePath)) {
        throw "Brak wymaganego pliku: $sourcePath"
    }

    $targetPath = Join-Path $stagingRoot $fileName
    $targetDirectory = Split-Path -Parent $targetPath
    if (-not [string]::IsNullOrWhiteSpace($targetDirectory)) {
        $null = New-Item -ItemType Directory -Force -Path $targetDirectory
    }

    Copy-Item -Force -Path $sourcePath -Destination $targetPath
}

foreach ($directoryName in $componentDirectories) {
    $sourceDirectory = Join-Path $SourceRoot $directoryName
    if (-not (Test-Path $sourceDirectory)) {
        throw "Brak wymaganego katalogu: $sourceDirectory"
    }

    $targetDirectory = Join-Path $stagingRoot $directoryName
    Copy-Item -Recurse -Force -Path $sourceDirectory -Destination $targetDirectory
}

$manifestLines = @(
    "ADB-WireGuard components package",
    "Created: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "Source: BackendSource/ADB-WireGuard",
    "",
    "Included files:",
    ($componentFiles | ForEach-Object { "- $_" }),
    "",
    "Included directories:",
    ($componentDirectories | ForEach-Object { "- $_" }),
    "",
    "Excluded on purpose:",
    "- mikrotik",
    "- state",
    "- local keys",
    "- local caches and logs"
)

$manifestLines | Set-Content -Path $manifestPath -Encoding UTF8

Remove-Item -Force $zipPath -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

Remove-Item -Recurse -Force $stagingRoot -ErrorAction SilentlyContinue

Write-Output "ZIP: $zipPath"
Write-Output "STAGING_REMOVED: $stagingRoot"
