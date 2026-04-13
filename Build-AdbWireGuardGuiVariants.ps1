[CmdletBinding()]
param(
    [string]$ProjectPath = "",
    [string]$ReleaseRoot = "",
    [string]$BackendSourceRoot = "",
    [string]$GuiShareRoot = "",
    [switch]$IncludeLocalMikroTikKey
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot "AdbWireGuardGui\AdbWireGuardGui.csproj"
}

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $ReleaseRoot = Join-Path $PSScriptRoot "release"
}

$publishRoot = Join-Path $ReleaseRoot "gui-unified"
$portablePackage = Join-Path $ReleaseRoot "adb-wireguard-gui-package"
$portableZip = Join-Path $ReleaseRoot "adb-wireguard-gui-package.zip"
$componentsZip = Join-Path $ReleaseRoot "adb-wireguard-components.zip"
$exeName = "ADB-przez-WireGuard.exe"
$localAppDataRoot = [Environment]::GetFolderPath("LocalApplicationData")
$localMikroTikKeyRoot = if ([string]::IsNullOrWhiteSpace($localAppDataRoot)) { "" } else { Join-Path $localAppDataRoot "ADB-WireGuard\mikrotik-key" }
$localMikroTikPrivateKey = if ([string]::IsNullOrWhiteSpace($localMikroTikKeyRoot)) { "" } else { Join-Path $localMikroTikKeyRoot "mikrotik_ed25519" }
$localMikroTikPublicKey = if ([string]::IsNullOrWhiteSpace($localMikroTikKeyRoot)) { "" } else { Join-Path $localMikroTikKeyRoot "mikrotik_ed25519.pub" }

if ([string]::IsNullOrWhiteSpace($BackendSourceRoot)) {
    $BackendSourceRoot = Join-Path $PSScriptRoot "BackendSource\ADB-WireGuard"
}

foreach ($path in @($publishRoot, $portablePackage)) {
    Remove-Item -Recurse -Force $path -ErrorAction SilentlyContinue
}

Remove-Item -Force $portableZip -ErrorAction SilentlyContinue

dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishTrimmed=false `
    -o $publishRoot

$publishedExe = Join-Path $publishRoot "AdbWireGuardGui.exe"

if (-not (Test-Path $BackendSourceRoot)) {
    throw "Brak katalogu backendu: $BackendSourceRoot"
}

$null = New-Item -ItemType Directory -Force -Path $portablePackage
$portablePackageExe = Join-Path $portablePackage $exeName
$portableBackendRoot = Join-Path $portablePackage "ADB-WireGuard"
$portableReadme = Join-Path $portablePackage "README.txt"

Copy-Item -Force $publishedExe $portablePackageExe
robocopy $BackendSourceRoot $portableBackendRoot /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null

$readmeLines = @(
    "ADB przez WireGuard - jeden program, dwa tryby pracy",
    "",
    "Uruchom ten plik:",
    $exeName,
    "",
    "1. Na komputerze z telefonem wybierz: Uruchom serwer na tym komputerze",
    "2. Na drugim komputerze wybierz: Połącz z drugim komputerem",
    "",
    "Folder z plikami pomocniczymi:",
    "ADB-WireGuard",
    "",
    "Jeśli trzeba, najpierw użyj w programie opcji 'Importuj klucz'.",
    "",
    "Jeśli chcesz wskazać inny folder ADB-WireGuard, utwórz obok EXE plik:",
    "package-root.txt",
    "i wpisz do niego pełną ścieżkę do tego folderu."
)

$readmeLines | Set-Content -Path $portableReadme -Encoding UTF8

Remove-Item -Force $portableZip -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $portablePackage "*") -DestinationPath $portableZip -CompressionLevel Optimal

if (-not [string]::IsNullOrWhiteSpace($GuiShareRoot)) {
    $null = New-Item -ItemType Directory -Force -Path $GuiShareRoot

    Copy-Item -Force $publishedExe (Join-Path $GuiShareRoot $exeName)
    Copy-Item -Force $portableZip (Join-Path $GuiShareRoot "adb-wireguard-gui-package.zip")
    Copy-Item -Force $componentsZip (Join-Path $GuiShareRoot "adb-wireguard-components.zip")

    $shareBackendRoot = Join-Path $GuiShareRoot "ADB-WireGuard"
    $null = New-Item -ItemType Directory -Force -Path $shareBackendRoot
    robocopy $BackendSourceRoot $shareBackendRoot /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null

    if ($IncludeLocalMikroTikKey -and -not [string]::IsNullOrWhiteSpace($localMikroTikPrivateKey) -and (Test-Path $localMikroTikPrivateKey)) {
        $shareMikroTikRoot = Join-Path $shareBackendRoot "mikrotik"
        $null = New-Item -ItemType Directory -Force -Path $shareMikroTikRoot
        Copy-Item -Force $localMikroTikPrivateKey (Join-Path $shareMikroTikRoot "mikrotik_ed25519")
        if (-not [string]::IsNullOrWhiteSpace($localMikroTikPublicKey) -and (Test-Path $localMikroTikPublicKey)) {
            Copy-Item -Force $localMikroTikPublicKey (Join-Path $shareMikroTikRoot "mikrotik_ed25519.pub")
        }
    }

    $sharePackageRoot = Join-Path $GuiShareRoot "adb-wireguard-gui-package"
    $null = New-Item -ItemType Directory -Force -Path $sharePackageRoot
    robocopy $portablePackage $sharePackageRoot /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null

    $allowedFiles = @(
        $exeName,
        "adb-wireguard-gui-package.zip",
        "adb-wireguard-components.zip"
    )
    $allowedDirectories = @(
        "ADB-WireGuard",
        "adb-wireguard-gui-package"
    )

    foreach ($file in (Get-ChildItem -LiteralPath $GuiShareRoot -File -ErrorAction SilentlyContinue)) {
        if ($allowedFiles -notcontains $file.Name) {
            Remove-Item -Force $file.FullName -ErrorAction SilentlyContinue
        }
    }

    foreach ($directory in (Get-ChildItem -LiteralPath $GuiShareRoot -Directory -ErrorAction SilentlyContinue)) {
        if ($allowedDirectories -notcontains $directory.Name) {
            Remove-Item -Recurse -Force $directory.FullName -ErrorAction SilentlyContinue
        }
    }
}

Remove-Item -Recurse -Force $publishRoot -ErrorAction SilentlyContinue

Write-Output "GUI_EXE=$publishedExe"
Write-Output "GUI_PACKAGE=$portablePackage"
Write-Output "GUI_ZIP=$portableZip"
