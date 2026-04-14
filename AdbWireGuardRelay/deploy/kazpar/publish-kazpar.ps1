param(
    [string]$Configuration = "Release",
    [string]$Runtime = "",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent (Split-Path -Parent $scriptDir)
$repoRoot = Split-Path -Parent (Split-Path -Parent $projectDir)

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\kazpar-deploy"
}

$publishDir = Join-Path $OutputRoot "publish"
$packageRoot = Join-Path $OutputRoot "package"
$sourceRoot = Join-Path $OutputRoot "source"

if (Test-Path $OutputRoot) {
    Remove-Item -Recurse -Force $OutputRoot
}

New-Item -ItemType Directory -Path $publishDir | Out-Null
New-Item -ItemType Directory -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Path $sourceRoot | Out-Null

$robocopyArgs = @(
    $projectDir,
    $sourceRoot,
    "/MIR",
    "/XD", "bin", "obj", "artifacts"
)

& robocopy @robocopyArgs | Out-Null
$robocopyExitCode = $LASTEXITCODE
if ($robocopyExitCode -ge 8) {
    throw "robocopy zakonczyl sie bledem $robocopyExitCode"
}

$publishArgs = @(
    "publish",
    (Join-Path $sourceRoot "AdbWireGuardRelay.csproj"),
    "-c", $Configuration,
    "-o", $publishDir,
    "--nologo"
)

if (-not [string]::IsNullOrWhiteSpace($Runtime)) {
    $publishArgs += @("-r", $Runtime)
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish zakonczyl sie kodem $LASTEXITCODE"
}

Copy-Item -Path (Join-Path $publishDir "*") -Destination $packageRoot -Recurse -Force
$deployDestination = Join-Path $packageRoot "deploy\kazpar"
New-Item -ItemType Directory -Path $deployDestination -Force | Out-Null
Copy-Item -Path (Join-Path $projectDir "deploy\kazpar\*") -Destination $deployDestination -Recurse -Force
Copy-Item -Path (Join-Path $projectDir "README.md") -Destination $packageRoot -Force

$zipPath = Join-Path $OutputRoot "adbwireguard-broker-kazpar.zip"
Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Gotowe:"
Write-Host "Package root: $packageRoot"
Write-Host "ZIP: $zipPath"
