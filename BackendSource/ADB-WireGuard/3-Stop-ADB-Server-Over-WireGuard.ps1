[CmdletBinding()]
param(
    [string]$MikroTikHost = "",
    [int]$MikroTikPort = 22,
    [string]$MikroTikUser = "admin",
    [string]$MikroTikKeyPath = "",
    [switch]$SkipMikroTikCleanup
)

$ErrorActionPreference = "Stop"
$env:ADB_SERVER_SOCKET = $null

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($MikroTikKeyPath)) {
    $MikroTikKeyPath = Join-Path $scriptRoot "mikrotik\mikrotik_ed25519"
}

if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_HOST)) {
    $MikroTikHost = $env:ADB_WG_ROUTER_HOST.Trim()
}

if ($env:ADB_WG_ROUTER_PORT -match '^\d+$') {
    $MikroTikPort = [int]$env:ADB_WG_ROUTER_PORT
}

if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_USER)) {
    $MikroTikUser = $env:ADB_WG_ROUTER_USER.Trim()
}

function Resolve-SshPath {
    $candidates = @(
        "$env:SystemRoot\System32\OpenSSH\ssh.exe",
        "$env:WINDIR\System32\OpenSSH\ssh.exe"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    $command = Get-Command "ssh.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Nie znaleziono ssh.exe. Zainstaluj OpenSSH Client w Windows."
}

function Resolve-MikroTikKeySourcePath {
    param(
        [string]$PreferredPath,
        [string]$ScriptRoot
    )

    $candidates = [System.Collections.Generic.List[string]]::new()

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        $candidates.Add($PreferredPath)
    }

    $genericPath = Join-Path $ScriptRoot "mikrotik\mikrotik_ed25519"
    $candidates.Add($genericPath)

    $mikrotikDirectory = Join-Path $ScriptRoot "mikrotik"
    if (Test-Path $mikrotikDirectory) {
        foreach ($candidate in (Get-ChildItem -Path $mikrotikDirectory -File -Filter "mikrotik_*" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike "*.pub" } |
            Sort-Object Name |
            Select-Object -ExpandProperty FullName)) {
            $candidates.Add($candidate)
        }
    }

    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return ""
}

function Prepare-MikroTikKeyFile {
    param(
        [string]$SourcePath
    )

    if (-not (Test-Path $SourcePath)) {
        throw "Nie znaleziono klucza MikroTik: $SourcePath"
    }

    $baseDir = if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        Join-Path $env:LOCALAPPDATA "ADB-WireGuard"
    } else {
        Join-Path $env:TEMP "ADB-WireGuard"
    }
    $userSegment = if (-not [string]::IsNullOrWhiteSpace($env:USERNAME)) { $env:USERNAME } else { "user" }
    $keyDir = Join-Path $baseDir "mikrotik-key\$userSegment"
    $null = New-Item -ItemType Directory -Force -Path $keyDir

    $destinationPath = Join-Path $keyDir ("mikrotik_ed25519-{0}.tmp" -f $PID)
    Remove-Item -Force $destinationPath -ErrorAction SilentlyContinue
    Copy-Item -Force -Path $SourcePath -Destination $destinationPath

    & icacls $destinationPath /inheritance:r /grant:r "${env:USERNAME}:(R)" /c | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Nie udalo sie ustawic uprawnien do klucza SSH."
    }

    return $destinationPath
}

function Invoke-MikroTikCommand {
    param(
        [string]$SshExe,
        [string]$HostAddress,
        [int]$HostPort,
        [string]$UserName,
        [string]$KeyPath,
        [string]$KnownHostsPath,
        [string]$Command
    )

    $stdoutPath = Join-Path $stateDir ("mikrotik-stop-{0}-stdout.log" -f $PID)
    $stderrPath = Join-Path $stateDir ("mikrotik-stop-{0}-stderr.log" -f $PID)
    Remove-Item -Force $stdoutPath, $stderrPath -ErrorAction SilentlyContinue

    $process = Start-Process `
        -FilePath $SshExe `
        -ArgumentList @(
            "-o", "BatchMode=yes",
            "-o", "IdentitiesOnly=yes",
            "-o", "StrictHostKeyChecking=accept-new",
            "-o", "UserKnownHostsFile=$KnownHostsPath",
            "-i", $KeyPath,
            "-p", "$HostPort",
            "$UserName@$HostAddress",
            $Command
        ) `
        -NoNewWindow `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -Wait `
        -PassThru

    $stdout = if (Test-Path $stdoutPath) { Get-Content -Raw -Path $stdoutPath } else { "" }
    $stderr = if (Test-Path $stderrPath) { Get-Content -Raw -Path $stderrPath } else { "" }
    if ($null -eq $stdout) { $stdout = "" }
    if ($null -eq $stderr) { $stderr = "" }

    if ($process.ExitCode -ne 0) {
        $combinedText = ($stdout + [Environment]::NewLine + $stderr).Trim()
        if ($combinedText -match 'kex_exchange_identification: read: Connection reset' -or
            $combinedText -match 'Connection closed by .+ port 22991') {
            return $combinedText
        }

        $details = @($stdout.Trim(), $stderr.Trim()) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        throw "Polecenie MikroTik nie powiodlo sie: $Command`n$($details -join [Environment]::NewLine)"
    }

    return (@($stdout.Trim(), $stderr.Trim()) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [Environment]::NewLine
}

$stateDir = Join-Path $scriptRoot "state"
$pidFile = Join-Path $stateDir "adb-server.pid"
$mikrotikStateFile = Join-Path $stateDir "mikrotik-forward-info.txt"
$knownHostsPath = Join-Path $stateDir "mikrotik_known_hosts"
$resolvedMikroTikKeyPath = Resolve-MikroTikKeySourcePath -PreferredPath $MikroTikKeyPath -ScriptRoot $scriptRoot
$canCleanupMikroTik = -not $SkipMikroTikCleanup -and
    -not [string]::IsNullOrWhiteSpace($MikroTikHost) -and
    -not [string]::IsNullOrWhiteSpace($MikroTikUser) -and
    -not [string]::IsNullOrWhiteSpace($resolvedMikroTikKeyPath)
$sshExe = $null
$preparedMikroTikKeyPath = ""

if ($canCleanupMikroTik) {
    $sshExe = Resolve-SshPath
    $preparedMikroTikKeyPath = Prepare-MikroTikKeyFile -SourcePath $resolvedMikroTikKeyPath
} else {
    $SkipMikroTikCleanup = $true
}

if (-not $SkipMikroTikCleanup) {
    try {
        Invoke-MikroTikCommand -SshExe $sshExe -HostAddress $MikroTikHost -HostPort $MikroTikPort -UserName $MikroTikUser -KeyPath $preparedMikroTikKeyPath -KnownHostsPath $knownHostsPath -Command ':foreach i in=[/ip firewall nat find where comment="ADB_WG_AUTO_NAT"] do={ /ip firewall nat remove numbers=$i }'
        Invoke-MikroTikCommand -SshExe $sshExe -HostAddress $MikroTikHost -HostPort $MikroTikPort -UserName $MikroTikUser -KeyPath $preparedMikroTikKeyPath -KnownHostsPath $knownHostsPath -Command ':foreach i in=[/ip firewall filter find where comment="ADB_WG_AUTO_FORWARD"] do={ /ip firewall filter remove numbers=$i }'
        Write-Host "Usunieto forward ADB z MikroTika."
    } catch {
        Write-Host "Nie udalo sie posprzatac MikroTika: $($_.Exception.Message)"
    }
}

if (Test-Path $pidFile) {
    $pidText = Get-Content -Raw -Path $pidFile -ErrorAction SilentlyContinue
    if ($pidText -match "^\d+$") {
        $process = Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue
        if ($process) {
            Stop-Process -Id $process.Id -Force
            Write-Host "Zatrzymano proces adb server PID $pidText."
        } else {
            Write-Host "Proces z PID $pidText juz nie dziala."
        }
    }
    Remove-Item -Force $pidFile -ErrorAction SilentlyContinue
} else {
    Write-Host "Brak pliku PID. Nie mam czego zatrzymac."
}

if (Test-Path $mikrotikStateFile) {
    Remove-Item -Force $mikrotikStateFile -ErrorAction SilentlyContinue
}
