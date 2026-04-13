[CmdletBinding()]
param(
    [int]$Port = 5037,
    [string]$AdbPath = "adb.exe",
    [switch]$SkipFirewallRule,
    [switch]$SkipMikroTikForward,
    [string]$MikroTikHost = "",
    [int]$MikroTikPort = 22,
    [string]$MikroTikUser = "admin",
    [string]$MikroTikKeyPath = "",
    [string]$MikroTikWireGuardIp = "",
    [int]$MikroTikWireGuardPrefixLength = 24
)

$ErrorActionPreference = "Stop"
$env:ADB_SERVER_SOCKET = $null
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$stateDir = Join-Path $scriptRoot "state"
$null = New-Item -ItemType Directory -Force -Path $stateDir
$startupErrorLog = Join-Path $stateDir "last-start-error.txt"
Remove-Item -Force $startupErrorLog -ErrorAction SilentlyContinue

trap {
    $_ | Out-String | Set-Content -Path $startupErrorLog
    exit 1
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

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

if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_WG_IP)) {
    $MikroTikWireGuardIp = $env:ADB_WG_ROUTER_WG_IP.Trim()
}

if ($env:ADB_WG_ROUTER_WG_PREFIX -match '^\d+$') {
    $MikroTikWireGuardPrefixLength = [int]$env:ADB_WG_ROUTER_WG_PREFIX
}

function Resolve-AdbPath {
    param(
        [string]$PreferredPath,
        [string]$ScriptRoot
    )

    function Add-CandidatePath {
        param(
            [System.Collections.Generic.List[string]]$List,
            [string]$BasePath,
            [string]$ChildPath = ""
        )

        if ([string]::IsNullOrWhiteSpace($BasePath)) {
            return
        }

        if ([string]::IsNullOrWhiteSpace($ChildPath)) {
            $List.Add($BasePath)
        } else {
            $List.Add((Join-Path $BasePath $ChildPath))
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath) -and (Test-Path $PreferredPath)) {
        return (Resolve-Path $PreferredPath).Path
    }

    $command = Get-Command $PreferredPath -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = [System.Collections.Generic.List[string]]::new()
    Add-CandidatePath -List $candidates -BasePath $ScriptRoot -ChildPath "platform-tools\adb.exe"
    Add-CandidatePath -List $candidates -BasePath $env:LOCALAPPDATA -ChildPath "Android\Sdk\platform-tools\adb.exe"
    Add-CandidatePath -List $candidates -BasePath $env:USERPROFILE -ChildPath "AppData\Local\Android\Sdk\platform-tools\adb.exe"
    Add-CandidatePath -List $candidates -BasePath $env:ANDROID_SDK_ROOT -ChildPath "platform-tools\adb.exe"
    Add-CandidatePath -List $candidates -BasePath $env:ANDROID_HOME -ChildPath "platform-tools\adb.exe"
    Add-CandidatePath -List $candidates -BasePath "C:\Android\platform-tools\adb.exe"
    Add-CandidatePath -List $candidates -BasePath "C:\platform-tools\adb.exe"

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Nie znaleziono adb.exe. Zainstaluj Android platform-tools, wrzuc adb.exe do PATH, poloz je w folderze platform-tools obok tego skryptu albo uruchom skrypt z -AdbPath."
}

function Convert-ToProcessArgumentString {
    param(
        [string[]]$Arguments
    )

    return [string]::Join(" ", @(
        foreach ($argument in $Arguments) {
            if ($null -eq $argument) {
                '""'
                continue
            }

            if ($argument -notmatch '[\s"]') {
                $argument
                continue
            }

            '"' + (($argument -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
        }
    ))
}

function Invoke-HiddenProcess {
    param(
        [string]$FilePath,
        [string[]]$ArgumentList,
        [string]$StandardOutputPath = "",
        [string]$StandardErrorPath = "",
        [switch]$Wait
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = Convert-ToProcessArgumentString -Arguments $ArgumentList
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = -not [string]::IsNullOrWhiteSpace($StandardOutputPath)
    $startInfo.RedirectStandardError = -not [string]::IsNullOrWhiteSpace($StandardErrorPath)

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()

    if ($Wait) {
        $stdout = if ($startInfo.RedirectStandardOutput) { $process.StandardOutput.ReadToEnd() } else { "" }
        $stderr = if ($startInfo.RedirectStandardError) { $process.StandardError.ReadToEnd() } else { "" }
        $process.WaitForExit()

        if ($startInfo.RedirectStandardOutput) {
            [System.IO.File]::WriteAllText($StandardOutputPath, $stdout)
        }

        if ($startInfo.RedirectStandardError) {
            [System.IO.File]::WriteAllText($StandardErrorPath, $stderr)
        }
    }

    return $process
}

function Get-AdbDeviceSnapshot {
    param(
        [string]$AdbExe,
        [int]$RetryCount = 6,
        [int]$DelayMilliseconds = 800
    )

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        $stdoutPath = Join-Path $stateDir ("adb-devices-{0}-{1}.stdout.log" -f $PID, $attempt)
        $stderrPath = Join-Path $stateDir ("adb-devices-{0}-{1}.stderr.log" -f $PID, $attempt)
        Remove-Item -Force $stdoutPath, $stderrPath -ErrorAction SilentlyContinue

        $process = Invoke-HiddenProcess `
            -FilePath $AdbExe `
            -ArgumentList @("devices", "-l") `
            -StandardOutputPath $stdoutPath `
            -StandardErrorPath $stderrPath `
            -Wait

        $stdout = if (Test-Path $stdoutPath) { Get-Content -Path $stdoutPath } else { @() }
        $stderr = if (Test-Path $stderrPath) { Get-Content -Path $stderrPath } else { @() }
        $output = @($stdout + $stderr)

        if ($process.ExitCode -eq 0) {
            $allDevices = @(
                $output |
                    Where-Object { $_ -match "^\S+\s+device($|\s)" } |
                    ForEach-Object { ($_ -split "\s+")[0] }
            )

            $ignoredDevices = @(
                $allDevices |
                    Where-Object { $_ -match "^emulator-\d+$" }
            )

            $shareableDevices = @(
                $allDevices |
                    Where-Object { $_ -notmatch "^emulator-\d+$" }
            )

            if ($allDevices.Count -gt 0) {
                return [pscustomobject]@{
                    AllDeviceIds = $allDevices
                    IgnoredDeviceIds = $ignoredDevices
                    ShareableDeviceIds = $shareableDevices
                }
            }
        }

        if ($attempt -lt $RetryCount) {
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }

    return [pscustomobject]@{
        AllDeviceIds = @()
        IgnoredDeviceIds = @()
        ShareableDeviceIds = @()
    }
}

function Get-WireGuardIps {
    $allIPv4 = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.IPAddress -notlike "169.254.*" -and
            $_.PrefixOrigin -ne "WellKnown"
        }

    $wireGuard = $allIPv4 | Where-Object {
        $_.InterfaceAlias -match "WireGuard|wg" -or
        $_.InterfaceAlias -match "tun"
    }

    if ($wireGuard) {
        return $wireGuard
    }

    return $allIPv4
}

function Get-PrimaryIpv4Address {
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
        Sort-Object SkipAsSource |
        Select-Object -First 1
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

    return $PreferredPath
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

    $destinationPath = Join-Path $keyDir ("mikrotik_ed25519-{0}.tmp" -f ([guid]::NewGuid().ToString("N")))
    Remove-Item -Force $destinationPath -ErrorAction SilentlyContinue
    Copy-Item -Force -Path $SourcePath -Destination $destinationPath

    & icacls $destinationPath /inheritance:r /grant:r "${env:USERNAME}:(R)" /c | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Nie udalo sie ustawic uprawnien do klucza SSH."
    }

    return $destinationPath
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

function Read-StateFileMap {
    param(
        [string]$Path
    )

    $result = @{}
    if (-not (Test-Path $Path)) {
        return $result
    }

    foreach ($line in (Get-Content -Path $Path)) {
        if ($line -match '^([^=]+)=(.*)$') {
            $result[$matches[1]] = $matches[2]
        }
    }

    return $result
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

    $stdoutPath = Join-Path $stateDir ("mikrotik-{0}-stdout.log" -f $PID)
    $stderrPath = Join-Path $stateDir ("mikrotik-{0}-stderr.log" -f $PID)
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

    $stdout = if (Test-Path $stdoutPath) { (Get-Content -Raw -Path $stdoutPath) } else { "" }
    $stderr = if (Test-Path $stderrPath) { (Get-Content -Raw -Path $stderrPath) } else { "" }
    if ($null -eq $stdout) { $stdout = "" }
    if ($null -eq $stderr) { $stderr = "" }

    if ($process.ExitCode -ne 0) {
        $details = @($stdout.Trim(), $stderr.Trim()) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        throw "Polecenie MikroTik nie powiodlo sie: $Command`n$($details -join [Environment]::NewLine)"
    }

    return (@($stdout.Trim(), $stderr.Trim()) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [Environment]::NewLine
}

function Get-MikroTikWireGuardBindings {
    param(
        [string]$SshExe,
        [string]$HostAddress,
        [int]$HostPort,
        [string]$UserName,
        [string]$KeyPath,
        [string]$KnownHostsPath
    )

    $rows = Invoke-MikroTikCommand `
        -SshExe $SshExe `
        -HostAddress $HostAddress `
        -HostPort $HostPort `
        -UserName $UserName `
        -KeyPath $KeyPath `
        -KnownHostsPath $KnownHostsPath `
        -Command '/ip address print terse where interface~"wg"'
    if ([string]::IsNullOrWhiteSpace($rows)) {
        return @()
    }

    return @(
        foreach ($row in ($rows -split "`r?`n")) {
            $match = [regex]::Match($row, 'address=([0-9.]+)/(\d+)')
            if (-not $match.Success) {
                continue
            }

            [pscustomobject]@{
                Address = $match.Groups[1].Value
                PrefixLength = [int]$match.Groups[2].Value
            }
        }
    )
}

function Ensure-MikroTikAdbForward {
    param(
        [string]$SshExe,
        [string]$HostAddress,
        [int]$HostPort,
        [string]$UserName,
        [string]$KeyPath,
        [string]$KnownHostsPath,
        [string]$RouterWireGuardIp,
        [string]$RouterWireGuardSubnet,
        [string]$TargetLanIp,
        [int]$ExternalPort,
        [int]$InternalPort
    )

    $natComment = "ADB_WG_AUTO_NAT"
    $filterComment = "ADB_WG_AUTO_FORWARD"

    Invoke-MikroTikCommand -SshExe $SshExe -HostAddress $HostAddress -HostPort $HostPort -UserName $UserName -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command "/ip firewall nat remove [find comment=$natComment]"
    Invoke-MikroTikCommand -SshExe $SshExe -HostAddress $HostAddress -HostPort $HostPort -UserName $UserName -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command "/ip firewall filter remove [find comment=$filterComment]"

    Invoke-MikroTikCommand -SshExe $SshExe -HostAddress $HostAddress -HostPort $HostPort -UserName $UserName -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command "/ip firewall nat add chain=dstnat action=dst-nat protocol=tcp src-address=$RouterWireGuardSubnet dst-address=$RouterWireGuardIp dst-port=$ExternalPort to-addresses=$TargetLanIp to-ports=$InternalPort comment=`"$natComment`" place-before=0"
    Invoke-MikroTikCommand -SshExe $SshExe -HostAddress $HostAddress -HostPort $HostPort -UserName $UserName -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command "/ip firewall filter add chain=forward action=accept protocol=tcp src-address=$RouterWireGuardSubnet dst-address=$TargetLanIp dst-port=$InternalPort comment=`"$filterComment`" place-before=0"
}

function Ensure-FirewallRule {
    param([int]$LocalPort)

    $ruleName = "ADB over WireGuard $LocalPort"
    $existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
    if ($existing) {
        return $ruleName
    }

    New-NetFirewallRule `
        -DisplayName $ruleName `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort $LocalPort `
        -Profile Private | Out-Null

    return $ruleName
}

$knownHostsPath = Join-Path $stateDir "mikrotik_known_hosts"

$adbExe = Resolve-AdbPath -PreferredPath $AdbPath -ScriptRoot $scriptRoot
$resolvedMikroTikKeyPath = Resolve-MikroTikKeySourcePath -PreferredPath $MikroTikKeyPath -ScriptRoot $scriptRoot
$mikroTikForwardRequested = -not $SkipMikroTikForward
$mikroTikForwardConfigured = $mikroTikForwardRequested -and
    -not [string]::IsNullOrWhiteSpace($MikroTikHost) -and
    -not [string]::IsNullOrWhiteSpace($MikroTikUser) -and
    -not [string]::IsNullOrWhiteSpace($MikroTikWireGuardIp)
$sshExe = $null
$preparedMikroTikKeyPath = ""

if ($mikroTikForwardConfigured) {
    $sshExe = Resolve-SshPath
    $preparedMikroTikKeyPath = Prepare-MikroTikKeyFile -SourcePath $resolvedMikroTikKeyPath
} else {
    $SkipMikroTikForward = $true
}

$deviceSnapshot = Get-AdbDeviceSnapshot -AdbExe $adbExe
$devices = @($deviceSnapshot.ShareableDeviceIds)
$ignoredDevices = @($deviceSnapshot.IgnoredDeviceIds)
if ($devices.Count -eq 0) {
    if ($ignoredDevices.Count -gt 0) {
        throw "Wykryto tylko emulator ADB ($($ignoredDevices -join ', ')). Podlacz Pixel po USB i zaakceptuj debugowanie na telefonie."
    }

    throw "Nie wykryto zadnego urzadzenia ADB w stanie 'device'. Podlacz Pixel po USB i zaakceptuj debugowanie."
}

$pidFile = Join-Path $stateDir "adb-server.pid"
$stdoutLog = Join-Path $stateDir "adb-server.stdout.log"
$stderrLog = Join-Path $stateDir "adb-server.stderr.log"
$infoFile = Join-Path $stateDir "adb-server-info.txt"
$mikrotikStateFile = Join-Path $stateDir "mikrotik-forward-info.txt"

if (Test-Path $pidFile) {
    $oldPid = Get-Content -Raw -Path $pidFile -ErrorAction SilentlyContinue
    if ($oldPid -match "^\d+$") {
        $oldProcess = Get-Process -Id ([int]$oldPid) -ErrorAction SilentlyContinue
        if ($oldProcess) {
            try {
                Stop-Process -Id $oldProcess.Id -Force -ErrorAction Stop
                Start-Sleep -Seconds 1
            } catch {
                # Old adb may belong to another session/user. Continue with adb kill-server below.
            }
        }
    }
    Remove-Item -Force $pidFile -ErrorAction SilentlyContinue
}

try {
    $null = & $adbExe kill-server 2>$null
} catch {
    # Ignore stale server shutdown noise before starting the local daemon.
}

$process = Start-Process `
    -FilePath $adbExe `
    -ArgumentList @("-a", "nodaemon", "server", "start") `
    -RedirectStandardOutput $stdoutLog `
    -RedirectStandardError $stderrLog `
    -PassThru `
    -WindowStyle Hidden

Start-Sleep -Seconds 2

if ($process.HasExited) {
    $stderr = if (Test-Path $stderrLog) { Get-Content -Raw -Path $stderrLog } else { "" }
    throw "Zdalny adb server nie wystartowal. Szczegoly:`n$stderr"
}

$process.Id | Set-Content -Path $pidFile -NoNewline

$firewallRule = ""
$firewallWarning = ""
$isAdministrator = Test-IsAdministrator
if (-not $SkipFirewallRule) {
    try {
        $firewallRule = Ensure-FirewallRule -LocalPort $Port
    } catch {
        if (-not $isAdministrator) {
            $firewallRule = "Nie udalo sie dodac reguly firewall automatycznie: brak uprawnien administratora. Uruchom skrypt jako administrator."
            $firewallWarning = "UWAGA: bez reguly Windows Firewall zdalne ADB przez WireGuard bedzie zablokowane."
        } else {
            $firewallRule = "Nie udalo sie dodac reguly firewall automatycznie: $($_.Exception.Message)"
            $firewallWarning = "UWAGA: zdalne ADB przez WireGuard moze byc zablokowane, dopoki regula firewall nie zostanie dodana."
        }
    }
}

$primaryIpInfo = Get-PrimaryIpv4Address
$localLanIp = if ($null -ne $primaryIpInfo) { $primaryIpInfo.IPAddress } else { "" }

$mikrotikSummary = if ($mikroTikForwardRequested) {
    "Pominieto konfiguracje MikroTika. Uzupelnij ustawienia routera i adres WireGuard, jesli chcesz automatyczny forward."
} else {
    "Pominieto konfiguracje MikroTika."
}
$connectTargets = @()

if (-not $SkipMikroTikForward -and -not [string]::IsNullOrWhiteSpace($localLanIp)) {
    try {
        $wgBindings = @()
        if (-not [string]::IsNullOrWhiteSpace($MikroTikWireGuardIp)) {
            $wgBindings = @(
                [pscustomobject]@{
                    Address = $MikroTikWireGuardIp
                    PrefixLength = $MikroTikWireGuardPrefixLength
                }
            )
        } else {
            $wgBindings = @(Get-MikroTikWireGuardBindings -SshExe $sshExe -HostAddress $MikroTikHost -HostPort $MikroTikPort -UserName $MikroTikUser -KeyPath $preparedMikroTikKeyPath -KnownHostsPath $knownHostsPath)
        }
        if ($wgBindings.Count -gt 0) {
            $primaryBinding = $wgBindings[0]
            $routerWireGuardSubnet = Get-IPv4CidrNetwork -IpAddress $primaryBinding.Address -PrefixLength $primaryBinding.PrefixLength
            $connectTargets = @($wgBindings | ForEach-Object { "$($_.Address):$Port" })
            $cachedState = Read-StateFileMap -Path $mikrotikStateFile
            $cacheMatches = (
                $cachedState["RouterHost"] -eq $MikroTikHost -and
                $cachedState["RouterPort"] -eq "$MikroTikPort" -and
                $cachedState["RouterUser"] -eq $MikroTikUser -and
                $cachedState["RouterWireGuardTargets"] -eq ($connectTargets -join ',') -and
                $cachedState["TargetLanIp"] -eq $localLanIp -and
                $cachedState["Port"] -eq "$Port"
            )

            if ($cacheMatches) {
                $mikrotikSummary = "MikroTik: forward TCP $Port z $($primaryBinding.Address) -> ${localLanIp}:$Port juz byl ustawiony."
            } else {
                Ensure-MikroTikAdbForward `
                    -SshExe $sshExe `
                    -HostAddress $MikroTikHost `
                    -HostPort $MikroTikPort `
                    -UserName $MikroTikUser `
                    -KeyPath $preparedMikroTikKeyPath `
                    -KnownHostsPath $knownHostsPath `
                    -RouterWireGuardIp $primaryBinding.Address `
                    -RouterWireGuardSubnet $routerWireGuardSubnet `
                    -TargetLanIp $localLanIp `
                    -ExternalPort $Port `
                    -InternalPort $Port

                $mikrotikSummary = "MikroTik: forward TCP $Port z $($primaryBinding.Address) -> ${localLanIp}:$Port zostal ustawiony."
                @(
                    "RouterHost=$MikroTikHost"
                    "RouterPort=$MikroTikPort"
                    "RouterUser=$MikroTikUser"
                    "RouterKeyPath=$preparedMikroTikKeyPath"
                    "RouterWireGuardTargets=$($connectTargets -join ',')"
                    "TargetLanIp=$localLanIp"
                    "Port=$Port"
                ) | Set-Content -Path $mikrotikStateFile
            }

            @(
                "RouterHost=$MikroTikHost"
                "RouterPort=$MikroTikPort"
                "RouterUser=$MikroTikUser"
                "RouterKeyPath=$preparedMikroTikKeyPath"
                "RouterWireGuardTargets=$($connectTargets -join ',')"
                "TargetLanIp=$localLanIp"
                "Port=$Port"
            ) | Set-Content -Path $mikrotikStateFile
        } else {
            $mikrotikSummary = "Nie znaleziono adresu WireGuard na MikroTiku."
        }
    } catch {
        $mikrotikSummary = "Nie udalo sie ustawic forwardu na MikroTiku: $($_.Exception.Message)"
    }
}

$ipLines = Get-WireGuardIps | ForEach-Object {
    "$($_.IPAddress)  [$($_.InterfaceAlias)]"
}

$info = @(
    "ADB over WireGuard jest uruchomiony."
    ""
    "PID: $($process.Id)"
    "Port: $Port"
    "ADB: $adbExe"
    "Urzadzenia: $($devices -join ', ')"
    $(if ($ignoredDevices.Count -gt 0) { "Pominiete emulatory przy sprawdzaniu gotowosci: $($ignoredDevices -join ', ')" })
    "Lokalny adres komputera z Pixelem: $localLanIp"
    ""
    "Adresy do uzycia po drugiej stronie:"
    $ipLines
    ""
    $mikrotikSummary
    ""
    "Adresy do zdalnego ADB przez MikroTik/WireGuard:"
    $(if ($connectTargets.Count -gt 0) { $connectTargets } else { "Brak automatycznie wykrytych adresow routera WG." })
    ""
    "Przyklad po stronie zdalnej:"
    "powershell -ExecutionPolicy Bypass -File `"$scriptRoot\2-Run-Remote-ADB-Command.ps1`" -ServerHost <ADRES_SERWERA> -AdbCommand `"devices`""
    ""
    "Zatrzymanie:"
    "powershell -ExecutionPolicy Bypass -File `"$scriptRoot\3-Stop-ADB-Server-Over-WireGuard.ps1`""
    ""
    "Firewall: $firewallRule"
    $(if (-not [string]::IsNullOrWhiteSpace($firewallWarning)) { $firewallWarning })
)

$info | Set-Content -Path $infoFile
$info | ForEach-Object { Write-Host $_ }
