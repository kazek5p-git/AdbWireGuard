[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [switch]$Elevated
)

$ErrorActionPreference = "Stop"

$localReport = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\state\last-launcher-report-local.txt"
$startScript = Join-Path $PackageRoot "Start-ADB-WG-Admin.ps1"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-LocalReport {
    param([string[]]$Lines)
    $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $localReport)
    $Lines | Set-Content -Path $localReport
}

try {
    if (-not $Elevated) {
        $argList = @(
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $PSCommandPath,
            "-PackageRoot", $PackageRoot,
            "-Elevated"
        )

        $child = Start-Process `
            -FilePath "powershell.exe" `
            -Verb RunAs `
            -ArgumentList $argList `
            -PassThru `
            -Wait

        Write-LocalReport @(
            "Helper exit code: $($child.ExitCode)"
            "Package root: $PackageRoot"
            "Start script: $startScript"
        )
        exit $child.ExitCode
    }

    if (-not (Test-IsAdministrator)) {
        Write-LocalReport @(
            "Helper elevated: no"
            "Blad: podniesiona sesja nie ma praw administratora."
        )
        exit 1
    }

    $proc = Start-Process `
        -FilePath "powershell.exe" `
        -ArgumentList @(
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $startScript,
            "-Elevated"
        ) `
        -PassThru `
        -Wait

    Write-LocalReport @(
        "Helper exit code: $($proc.ExitCode)"
        "Package root: $PackageRoot"
        "Start script: $startScript"
        "Helper elevated: yes"
    )
    exit $proc.ExitCode
} catch {
    Write-LocalReport @(
        "Blad helpera:"
        ($_ | Out-String)
    )
    exit 1
}
