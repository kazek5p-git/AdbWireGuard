[CmdletBinding()]
param(
    [string]$RemoteRoot = "",
    [switch]$Elevated
)

$ErrorActionPreference = "Stop"

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$localState = Join-Path $packageRoot "state"
$localReport = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\state\last-launcher-report-local.txt"
$localStartReport = Join-Path $localState "last-start-report.txt"
$localStartConsole = Join-Path $localState "last-start-console.txt"
$localStartError = Join-Path $localState "last-start-error.txt"
$localStartInfo = Join-Path $localState "adb-server-info.txt"
$localFallbackLog = Join-Path $localState "run-local-mikrotik-fallback.txt"
$localKnownHosts = Join-Path $localState "mikrotik_known_hosts"
$localStartScript = Join-Path $packageRoot "1-Start-ADB-Server-Over-WireGuard.ps1"
$localAdbPath = Join-Path $packageRoot "platform-tools\adb.exe"
$localMikroTikKey = Join-Path $packageRoot "mikrotik\mikrotik_ed25519"
$routerHost = if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_HOST)) { $env:ADB_WG_ROUTER_HOST.Trim() } else { "" }
$routerPort = if ($env:ADB_WG_ROUTER_PORT -match '^\d+$') { [int]$env:ADB_WG_ROUTER_PORT } else { 22 }
$routerUser = if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_USER)) { $env:ADB_WG_ROUTER_USER.Trim() } else { "admin" }
$routerWireGuardIp = if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_WG_IP)) { $env:ADB_WG_ROUTER_WG_IP.Trim() } else { "" }
$routerWireGuardPrefixLength = if ($env:ADB_WG_ROUTER_WG_PREFIX -match '^\d+$') { [int]$env:ADB_WG_ROUTER_WG_PREFIX } else { 24 }
$remoteState = if ([string]::IsNullOrWhiteSpace($RemoteRoot)) { "" } else { Join-Path $RemoteRoot "state" }

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Write-LocalReport {
    param([string[]]$Lines)
    $dir = Split-Path -Parent $localReport
    $null = New-Item -ItemType Directory -Force -Path $dir
    $Lines | Set-Content -Path $localReport
}

function Build-StartReport {
    param([int]$ExitCode)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("===== STATUS =====")
    $lines.Add("EXITCODE: $ExitCode")
    $lines.Add("")

    if (Test-Path $localStartConsole) {
        $lines.Add("===== CONSOLE =====")
        $lines.Add((Get-Content -Raw -Path $localStartConsole))
        $lines.Add("")
    }

    if (Test-Path $localStartError) {
        $lines.Add("===== ERROR =====")
        $lines.Add((Get-Content -Raw -Path $localStartError))
    } elseif (Test-Path $localStartInfo) {
        $lines.Add("===== INFO =====")
        $lines.Add((Get-Content -Raw -Path $localStartInfo))
    } else {
        $lines.Add("Brak pliku statusu. Skrypt nic nie zapisal.")
    }

    $lines | Set-Content -Path $localStartReport
}

function Sync-FileIfExists {
    param([string]$Source, [string]$Target)
    if (Test-Path $Source) {
        $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Target)
        Copy-Item -Force -Path $Source -Destination $Target
    }
}

function Sync-ToRemote {
    if ([string]::IsNullOrWhiteSpace($remoteState)) {
        return
    }

    try {
        $null = New-Item -ItemType Directory -Force -Path $remoteState
        Sync-FileIfExists -Source $localStartReport -Target (Join-Path $remoteState "last-start-report.txt")
        Sync-FileIfExists -Source $localStartConsole -Target (Join-Path $remoteState "last-start-console.txt")
        Sync-FileIfExists -Source $localStartError -Target (Join-Path $remoteState "last-start-error.txt")
        Sync-FileIfExists -Source $localStartInfo -Target (Join-Path $remoteState "adb-server-info.txt")
        Sync-FileIfExists -Source $localKnownHosts -Target (Join-Path $remoteState "mikrotik_known_hosts")
        Sync-FileIfExists -Source (Join-Path $localState "mikrotik-forward-info.txt") -Target (Join-Path $remoteState "mikrotik-forward-info.txt")
    } catch {
        # Local report remains authoritative if remote sync fails.
    }
}

function Get-PrimaryIpv4 {
    $route = Get-NetRoute -AddressFamily IPv4 -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1

    if ($null -eq $route) {
        return $null
    }

    return Get-NetIPAddress -AddressFamily IPv4 -InterfaceIndex $route.InterfaceIndex -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.IPAddress -notlike "169.254.*"
        } |
        Select-Object -First 1 -ExpandProperty IPAddress
}

function ConvertTo-IPv4Uint32 {
    param([string]$IpAddress)

    $bytes = ([System.Net.IPAddress]::Parse($IpAddress)).GetAddressBytes()
    [Array]::Reverse($bytes)
    return [BitConverter]::ToUInt32($bytes, 0)
}

function ConvertFrom-IPv4Uint32 {
    param([uint32]$Value)

    $bytes = [BitConverter]::GetBytes($Value)
    [Array]::Reverse($bytes)
    return ([System.Net.IPAddress]::new($bytes)).ToString()
}

function Get-IPv4CidrNetwork {
    param(
        [string]$IpAddress,
        [int]$PrefixLength
    )

    $ipValue = ConvertTo-IPv4Uint32 -IpAddress $IpAddress
    $mask = if ($PrefixLength -le 0) { [uint32]0 } else { [uint32]::MaxValue -shl (32 - $PrefixLength) }
    $network = $ipValue -band $mask
    return "$(ConvertFrom-IPv4Uint32 -Value $network)/$PrefixLength"
}

function Invoke-MikroTikFallback {
    if ([string]::IsNullOrWhiteSpace($routerHost) -or
        [string]::IsNullOrWhiteSpace($routerUser) -or
        [string]::IsNullOrWhiteSpace($routerWireGuardIp)) {
        throw "Brak ustawien routera dla fallback MikroTik."
    }

    $hostIp = Get-PrimaryIpv4
    if ([string]::IsNullOrWhiteSpace($hostIp)) {
        throw "Brak lokalnego IPv4 dla fallback MikroTik."
    }
    $routerWireGuardSubnet = Get-IPv4CidrNetwork -IpAddress $routerWireGuardIp -PrefixLength $routerWireGuardPrefixLength

    $sshExe = "$env:SystemRoot\System32\OpenSSH\ssh.exe"
    if (-not (Test-Path $sshExe)) {
        throw "Brak ssh.exe w OpenSSH Client."
    }

    & $sshExe -o BatchMode=yes -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=$localKnownHosts -i $localMikroTikKey -p $routerPort "$routerUser@$routerHost" '/ip firewall nat remove [find comment=ADB_WG_AUTO_NAT]' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Nie udalo sie usunac starego NAT na MikroTiku." }

    & $sshExe -o BatchMode=yes -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=$localKnownHosts -i $localMikroTikKey -p $routerPort "$routerUser@$routerHost" '/ip firewall filter remove [find comment=ADB_WG_AUTO_FORWARD]' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Nie udalo sie usunac starego FORWARD na MikroTiku." }

    & $sshExe -o BatchMode=yes -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=$localKnownHosts -i $localMikroTikKey -p $routerPort "$routerUser@$routerHost" "/ip firewall nat add chain=dstnat action=dst-nat protocol=tcp src-address=$routerWireGuardSubnet dst-address=$routerWireGuardIp dst-port=5037 to-addresses=$hostIp to-ports=5037 comment=`"ADB_WG_AUTO_NAT`" place-before=0" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Nie udalo sie dodac NAT na MikroTiku." }

    & $sshExe -o BatchMode=yes -o IdentitiesOnly=yes -o StrictHostKeyChecking=accept-new -o UserKnownHostsFile=$localKnownHosts -i $localMikroTikKey -p $routerPort "$routerUser@$routerHost" "/ip firewall filter add chain=forward action=accept protocol=tcp src-address=$routerWireGuardSubnet dst-address=$hostIp dst-port=5037 comment=`"ADB_WG_AUTO_FORWARD`" place-before=0" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Nie udalo sie dodac FORWARD na MikroTiku." }

    "Fallback MikroTik OK: $hostIp" | Set-Content -Path $localFallbackLog
    @(
        "RouterHost=$routerHost"
        "RouterPort=$routerPort"
        "RouterUser=$routerUser"
        "RouterKeyPath=$localMikroTikKey"
        "RouterWireGuardTargets=$routerWireGuardIp:5037"
        "TargetLanIp=$hostIp"
        "Port=5037"
    ) | Set-Content -Path (Join-Path $localState "mikrotik-forward-info.txt")
}

trap {
    $null = New-Item -ItemType Directory -Force -Path $localState
    ($_ | Out-String) | Set-Content -Path $localStartError
    try {
        Build-StartReport -ExitCode 1
    } catch {
    }
    try {
        Sync-ToRemote
    } catch {
    }
    exit 1
}

if (-not $Elevated) {
    $argumentList = @(
        "-NoLogo",
        "-ExecutionPolicy", "Bypass",
        "-File", $PSCommandPath
    )
    if (-not [string]::IsNullOrWhiteSpace($RemoteRoot)) {
        $argumentList += @("-RemoteRoot", $RemoteRoot)
    }
    $argumentList += "-Elevated"

    $child = Start-Process `
        -FilePath "powershell.exe" `
        -Verb RunAs `
        -ArgumentList $argumentList `
        -PassThru `
        -Wait

    Write-LocalReport @(
        "Kod wyjscia elevacji: $($child.ExitCode)"
        "Skrypt: $PSCommandPath"
        "RemoteRoot: $RemoteRoot"
    )
    exit $child.ExitCode
}

if (-not (Test-IsAdministrator)) {
    Write-LocalReport @("Podniesiona sesja nadal nie ma praw administratora.")
    exit 1
}

$null = New-Item -ItemType Directory -Force -Path $localState
foreach ($path in @($localStartReport, $localStartConsole, $localStartError, $localStartInfo, $localFallbackLog)) {
    Remove-Item -Force $path -ErrorAction SilentlyContinue
}

$innerStdout = Join-Path $localState "run-local-start.stdout.txt"
$innerStderr = Join-Path $localState "run-local-start.stderr.txt"
Remove-Item -Force $innerStdout, $innerStderr -ErrorAction SilentlyContinue

$inner = Start-Process `
    -FilePath "powershell.exe" `
    -ArgumentList @(
        "-NoLogo",
        "-ExecutionPolicy", "Bypass",
        "-File", $localStartScript,
        "-AdbPath", $localAdbPath,
        "-MikroTikKeyPath", $localMikroTikKey
    ) `
    -RedirectStandardOutput $innerStdout `
    -RedirectStandardError $innerStderr `
    -PassThru

if (-not $inner.WaitForExit(45000)) {
    Stop-Process -Id $inner.Id -Force -ErrorAction SilentlyContinue
    throw "Przekroczono czas oczekiwania na 1-Start-ADB-Server-Over-WireGuard.ps1"
}

$stdout = if (Test-Path $innerStdout) { (Get-Content -Raw -Path $innerStdout) } else { "" }
$stderr = if (Test-Path $innerStderr) { (Get-Content -Raw -Path $innerStderr) } else { "" }
if ($null -eq $stdout) { $stdout = "" }
if ($null -eq $stderr) { $stderr = "" }

$exitCode = $inner.ExitCode
$consoleOutput = @($stdout.Trim(), $stderr.Trim()) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
($consoleOutput -join ([Environment]::NewLine + [Environment]::NewLine)) | Set-Content -Path $localStartConsole

if ($consoleOutput -match "Nie udalo sie ustawic forwardu na MikroTiku") {
    try {
        Invoke-MikroTikFallback
        $patched = ($consoleOutput | Out-String) -replace 'Nie udalo sie ustawic forwardu na MikroTiku:.*(\r?\n)?', ''
        $patched = $patched.TrimEnd()
        if ($patched.Length -gt 0) {
            $patched += [Environment]::NewLine + [Environment]::NewLine
        }
        $patched += "MikroTik: fallback SSH ustawil forward TCP 5037 z $routerWireGuardIp." + [Environment]::NewLine + [Environment]::NewLine +
            "Adresy do zdalnego ADB przez MikroTik/WireGuard:" + [Environment]::NewLine +
            "$routerWireGuardIp:5037"
        $patched | Set-Content -Path $localStartConsole
        if (Test-Path $localStartError) {
            Remove-Item -Force $localStartError -ErrorAction SilentlyContinue
        }
    } catch {
        $_ | Out-String | Set-Content -Path $localStartError
    }
}

Build-StartReport -ExitCode $exitCode
Sync-ToRemote

Write-LocalReport @(
    "Kod wyjscia startu: $exitCode"
    "Skrypt: $PSCommandPath"
    "Pakiet: $packageRoot"
    "Lokalny raport startu: $localStartReport"
    $(if (-not [string]::IsNullOrWhiteSpace($remoteState)) { "Zdalny raport startu: $(Join-Path $remoteState 'last-start-report.txt')" })
    $(if (Test-Path $localFallbackLog) { "Fallback MikroTik log: $localFallbackLog" })
)

exit $exitCode
