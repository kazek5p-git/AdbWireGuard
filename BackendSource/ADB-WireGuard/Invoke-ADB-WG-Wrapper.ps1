[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$env:ADB_SERVER_SOCKET = $null

$sourceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$localPackage = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\package"
$localState = Join-Path $localPackage "state"
$localStartScript = Join-Path $localPackage "1-Start-ADB-Server-Over-WireGuard.ps1"
$localStopScript = Join-Path $localPackage "3-Stop-ADB-Server-Over-WireGuard.ps1"
$packageFiles = @(
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
    "README-PL.txt",
    "Run-ADB-WG-Task.ps1",
    "Run-ADB-WireGuard-Bootstrap.ps1",
    "Start-ADB-WG-Admin.bat",
    "Start-ADB-WG-Admin.ps1"
)
$routerAutomationEnabled = $env:ADB_WG_ENABLE_ROUTER_AUTO -match '^(1|true|yes)$'
$routerHost = if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_HOST)) { $env:ADB_WG_ROUTER_HOST.Trim() } else { "" }
$routerPort = if ($env:ADB_WG_ROUTER_PORT -match '^\d+$') { [int]$env:ADB_WG_ROUTER_PORT } else { 22 }
$routerUser = if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_USER)) { $env:ADB_WG_ROUTER_USER.Trim() } else { "admin" }
$routerWireGuardIp = if (-not [string]::IsNullOrWhiteSpace($env:ADB_WG_ROUTER_WG_IP)) { $env:ADB_WG_ROUTER_WG_IP.Trim() } else { "" }
$routerWireGuardPrefixLength = if ($env:ADB_WG_ROUTER_WG_PREFIX -match '^\d+$') { [int]$env:ADB_WG_ROUTER_WG_PREFIX } else { 24 }
$localAdbPath = Join-Path $localPackage "platform-tools\adb.exe"
$localMikroTikKey = Join-Path $localPackage "mikrotik\mikrotik_ed25519"
$localMikroTikPublicKey = Join-Path $localPackage "mikrotik\mikrotik_ed25519.pub"
$sourceMikroTikKey = Join-Path $sourceRoot "mikrotik\mikrotik_ed25519"
$sourceMikroTikPublicKey = Join-Path $sourceRoot "mikrotik\mikrotik_ed25519.pub"
$persistentMikroTikKey = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\mikrotik-key\mikrotik_ed25519"
$persistentMikroTikPublicKey = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\mikrotik-key\mikrotik_ed25519.pub"
$legacyRunnerMikroTikDirectory = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\runner\mikrotik"
$localInfo = Join-Path $localState "adb-server-info.txt"
$localError = Join-Path $localState "last-start-error.txt"
$localReport = Join-Path $localState "last-start-report.txt"
$localWrapperForwardState = Join-Path $localState "wrapper-fallback-forward-info.txt"
$localRunStdout = Join-Path $localState "run-local-start.stdout.txt"
$localRunStderr = Join-Path $localState "run-local-start.stderr.txt"
$remoteState = Join-Path $sourceRoot "state"
$remoteInfo = Join-Path $remoteState "adb-server-info.txt"
$remoteError = Join-Path $remoteState "last-start-error.txt"
$remoteReport = Join-Path $remoteState "last-start-report.txt"
$remoteWrapper = Join-Path $remoteState "5-wrapper-report.txt"
$remoteStatus = Join-Path $remoteState "last-start-status.txt"

function Copy-IfExists {
    param([string]$Source, [string]$Target)
    if (Test-Path $Source) {
        $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Target)
        $sourceItem = Get-Item -LiteralPath $Source -ErrorAction SilentlyContinue
        $targetItem = Get-Item -LiteralPath $Target -ErrorAction SilentlyContinue
        if ($null -ne $sourceItem -and $null -ne $targetItem) {
            if ([string]::Equals($sourceItem.FullName, $targetItem.FullName, [System.StringComparison]::OrdinalIgnoreCase)) {
                return
            }
        }
        Copy-Item -Force -Path $Source -Destination $Target
    }
}

function Ensure-LocalMikroTikKey {
    param(
        [string]$LocalKeyPath,
        [string]$LocalPublicKeyPath
    )

    if (Test-Path $LocalKeyPath) {
        return $LocalKeyPath
    }

    $candidatePairs = [System.Collections.Generic.List[hashtable]]::new()
    $candidatePairs.Add(@{ Private = $sourceMikroTikKey; Public = $sourceMikroTikPublicKey })
    $candidatePairs.Add(@{ Private = $persistentMikroTikKey; Public = $persistentMikroTikPublicKey })

    foreach ($directory in @(
        (Join-Path $sourceRoot "mikrotik"),
        (Join-Path $env:LOCALAPPDATA "ADB-WireGuard\mikrotik-key"),
        $legacyRunnerMikroTikDirectory
    )) {
        if (-not (Test-Path $directory)) {
            continue
        }

        $privateCandidate = Get-ChildItem -Path $directory -File -Filter "mikrotik_*" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike "*.pub" } |
            Sort-Object Name |
            Select-Object -First 1
        $publicCandidate = Get-ChildItem -Path $directory -File -Filter "mikrotik_*.pub" -ErrorAction SilentlyContinue |
            Sort-Object Name |
            Select-Object -First 1

        if ($null -ne $privateCandidate) {
            $candidatePairs.Add(@{
                Private = $privateCandidate.FullName
                Public = if ($null -ne $publicCandidate) { $publicCandidate.FullName } else { "" }
            })
        }
    }

    foreach ($candidate in $candidatePairs) {
        if (-not (Test-Path $candidate.Private)) {
            continue
        }

        $null = New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LocalKeyPath)
        Copy-Item -Force -Path $candidate.Private -Destination $LocalKeyPath
        Copy-IfExists -Source $candidate.Public -Target $LocalPublicKeyPath
        return $candidate.Private
    }

    return ""
}

function Write-WrapperReport {
    param([string[]]$Lines)
    $null = New-Item -ItemType Directory -Force -Path $remoteState
    $Lines | Set-Content -Path $remoteWrapper
}

function Write-StartStatus {
    param(
        [string]$Text
    )

    $null = New-Item -ItemType Directory -Force -Path $remoteState
    $Text | Set-Content -Path $remoteStatus
}

function Speak-Status {
    param(
        [string]$Text,
        [bool]$Success
    )

    $enableVoice = $env:ADB_WG_ENABLE_VOICE
    $enableSound = $env:ADB_WG_ENABLE_SOUND
    $voiceEnabled = -not ($enableVoice -match '^(0|false|no)$')
    $soundEnabled = -not ($enableSound -match '^(0|false|no)$')

    if (-not $voiceEnabled -and -not $soundEnabled) {
        return
    }

    try {
        if ($soundEnabled) {
            if ($Success) {
                [System.Media.SystemSounds]::Asterisk.Play()
            } else {
                [System.Media.SystemSounds]::Hand.Play()
            }
            Start-Sleep -Milliseconds 350
        }

        if ($voiceEnabled) {
            $voice = New-Object -ComObject SAPI.SpVoice
            $voice.Rate = 4
            $null = $voice.Speak($Text)
        }
    } catch {
        # Voice feedback is optional.
    }
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

function Test-FirewallRulePresent {
    param([int]$LocalPort = 5037)

    try {
        $ruleName = "ADB over WireGuard $LocalPort"
        $rules = @(Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)
        foreach ($rule in $rules) {
            if (-not $rule.Enabled -or $rule.Direction -ne "Inbound" -or $rule.Action -ne "Allow") {
                continue
            }

            $profile = [int]$rule.Profile
            $coversDomain = ($profile -band 1) -ne 0
            $coversPrivate = ($profile -band 2) -ne 0
            $coversPublic = ($profile -band 4) -ne 0
            if ($coversDomain -and $coversPrivate -and $coversPublic) {
                return $true
            }
        }

        return $false
    } catch {
        return $false
    }
}

function Build-LocalStartReport {
    param([int]$ExitCode)

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("===== STATUS =====")
    $lines.Add("EXITCODE: $ExitCode")
    $lines.Add("")

    if (Test-Path $localError) {
        $lines.Add("===== ERROR =====")
        $lines.Add((Get-Content -Raw -Path $localError))
    } elseif (Test-Path $localInfo) {
        $lines.Add("===== INFO =====")
        $lines.Add((Get-Content -Raw -Path $localInfo))
    } else {
        $lines.Add("Brak pliku statusu. Skrypt nic nie zapisal.")
    }

    $lines | Set-Content -Path $localReport
}

function Invoke-SshMikroTik {
    param(
        [string]$KeyPath,
        [string]$KnownHostsPath,
        [string]$Command
    )

    $sshExe = "$env:SystemRoot\System32\OpenSSH\ssh.exe"
    $stdout = Join-Path $env:TEMP ("adbwg-mt-{0}-out.txt" -f $PID)
    $stderr = Join-Path $env:TEMP ("adbwg-mt-{0}-err.txt" -f $PID)
    Remove-Item -Force $stdout, $stderr -ErrorAction SilentlyContinue

    $proc = Start-Process -FilePath $sshExe -ArgumentList @(
        "-o","BatchMode=yes",
        "-o","IdentitiesOnly=yes",
        "-o","StrictHostKeyChecking=accept-new",
        "-o","UserKnownHostsFile=$KnownHostsPath",
        "-i",$KeyPath,
        "-p","$routerPort",
        "$routerUser@$routerHost",
        $Command
    ) -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr

    $out = if (Test-Path $stdout) { Get-Content -Raw -Path $stdout } else { "" }
    $err = if (Test-Path $stderr) { Get-Content -Raw -Path $stderr } else { "" }
    return [pscustomobject]@{
        ExitCode = $proc.ExitCode
        StdOut = if ($null -eq $out) { "" } else { $out }
        StdErr = if ($null -eq $err) { "" } else { $err }
    }
}

function Test-TcpReachable {
    param(
        [string]$TargetHost,
        [int]$Port,
        [int]$TimeoutMs = 3000
    )

    $client = [System.Net.Sockets.TcpClient]::new()
    try {
        $async = $client.BeginConnect($TargetHost, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs)) {
            return $false
        }
        $client.EndConnect($async)
        return $true
    } catch {
        return $false
    } finally {
        $client.Dispose()
    }
}

function Wait-TcpReachable {
    param(
        [string]$TargetHost,
        [int]$Port,
        [int]$Attempts = 15,
        [int]$DelaySeconds = 2
    )

    for ($i = 0; $i -lt $Attempts; $i++) {
        if (Test-TcpReachable -TargetHost $TargetHost -Port $Port) {
            return $true
        }
        Start-Sleep -Seconds $DelaySeconds
    }

    return $false
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

function Test-MikroTikWrapperForwardExists {
    param(
        [string]$KeyPath,
        [string]$KnownHostsPath,
        [string]$RouterWireGuardIp,
        [string]$TargetLanIp
    )

    $natResult = Invoke-SshMikroTik -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command '/ip firewall nat print terse'
    $filterResult = Invoke-SshMikroTik -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command '/ip firewall filter print terse'

    $natText = ($natResult.StdOut + [Environment]::NewLine + $natResult.StdErr)
    $filterText = ($filterResult.StdOut + [Environment]::NewLine + $filterResult.StdErr)

    $natOk = $natResult.ExitCode -eq 0 -and
        $natText -match 'ADB_WG_AUTO_NAT' -and
        $natText -match 'dst-port=5037' -and
        $natText -match [regex]::Escape("dst-address=$RouterWireGuardIp") -and
        $natText -match [regex]::Escape("to-addresses=$TargetLanIp")

    $filterOk = $filterResult.ExitCode -eq 0 -and
        $filterText -match 'ADB_WG_AUTO_FORWARD' -and
        $filterText -match 'dst-port=5037' -and
        $filterText -match [regex]::Escape("dst-address=$TargetLanIp")

    return ($natOk -and $filterOk)
}

function Get-MikroTikWrapperBindingInfo {
    param(
        [string]$KeyPath,
        [string]$KnownHostsPath,
        [string]$RouterWireGuardIp
    )

    $result = Invoke-SshMikroTik -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -Command '/ip address print terse where interface~"wg"'
    if ($result.ExitCode -ne 0) {
        return [pscustomobject]@{
            Interface = ""
            Source = "fallback-no-interface"
        }
    }

    $rows = ($result.StdOut + [Environment]::NewLine + $result.StdErr) -split "`r?`n"
    foreach ($row in $rows) {
        if ($row -notmatch [regex]::Escape("address=$RouterWireGuardIp/")) {
            continue
        }

        $interfaceMatch = [regex]::Match($row, 'interface=([^\s]+)')
        return [pscustomobject]@{
            Interface = if ($interfaceMatch.Success) { $interfaceMatch.Groups[1].Value } else { "" }
            Source = "router-query"
        }
    }

    return [pscustomobject]@{
        Interface = ""
        Source = "router-query-no-match"
    }
}

function Invoke-MikroTikWrapperFallback {
    param([string]$TargetLanIp)

    if (-not $routerAutomationEnabled -or
        [string]::IsNullOrWhiteSpace($routerHost) -or
        [string]::IsNullOrWhiteSpace($routerUser) -or
        [string]::IsNullOrWhiteSpace($routerWireGuardIp)) {
        return [pscustomobject]@{
            Verified = $false
            RouterWireGuardIp = $routerWireGuardIp
            TargetLanIp = $TargetLanIp
            Skipped = $true
        }
    }

    $baseDir = Join-Path $env:LOCALAPPDATA "ADB-WireGuard\wrapper-fallback"
    $null = New-Item -ItemType Directory -Force -Path $baseDir
    $keyPath = Join-Path $baseDir ("mikrotik-{0}.key" -f $PID)
    $knownHostsPath = Join-Path $baseDir "known_hosts"
    Copy-Item -Force -Path $localMikroTikKey -Destination $keyPath
    & icacls $keyPath /inheritance:r /grant:r "${env:USERNAME}:(R)" /c | Out-Null
    $bindingInfo = Get-MikroTikWrapperBindingInfo -KeyPath $keyPath -KnownHostsPath $knownHostsPath -RouterWireGuardIp $routerWireGuardIp
    $routerWireGuardSubnet = Get-IPv4CidrNetwork -IpAddress $routerWireGuardIp -PrefixLength $routerWireGuardPrefixLength

    $commands = @(
        "/ip firewall nat remove [find comment=ADB_WG_AUTO_NAT]",
        "/ip firewall filter remove [find comment=ADB_WG_AUTO_FORWARD]",
        "/ip firewall nat add chain=dstnat action=dst-nat protocol=tcp src-address=$routerWireGuardSubnet dst-address=$routerWireGuardIp dst-port=5037 to-addresses=$TargetLanIp to-ports=5037 comment=ADB_WG_AUTO_NAT",
        "/ip firewall filter add chain=forward action=accept protocol=tcp src-address=$routerWireGuardSubnet dst-address=$TargetLanIp dst-port=5037 comment=ADB_WG_AUTO_FORWARD"
    )

    if (-not [string]::IsNullOrWhiteSpace($bindingInfo.Interface)) {
        $commands[2] += " in-interface=$($bindingInfo.Interface)"
        $commands[3] += " in-interface=$($bindingInfo.Interface)"
    }

    foreach ($command in $commands) {
        $result = Invoke-SshMikroTik -KeyPath $keyPath -KnownHostsPath $knownHostsPath -Command $command
        if ($result.ExitCode -ne 0 -and $command -like "/ip firewall * add*") {
            $message = ($result.StdOut + $result.StdErr)
            if ($message -notmatch ('Connection closed by ' + [regex]::Escape($routerHost) + ' port ' + $routerPort)) {
                throw "MikroTik fallback add failed: $command`n$($result.StdOut)$($result.StdErr)"
            }
        }
    }

    $verified = Test-MikroTikWrapperForwardExists -KeyPath $keyPath -KnownHostsPath $knownHostsPath -RouterWireGuardIp $routerWireGuardIp -TargetLanIp $TargetLanIp
    return [pscustomobject]@{
        Verified = $verified
        RouterWireGuardIp = $routerWireGuardIp
        TargetLanIp = $TargetLanIp
    }
}

try {
    $null = New-Item -ItemType Directory -Force -Path $localPackage
    $null = New-Item -ItemType Directory -Force -Path $localState
    $null = New-Item -ItemType Directory -Force -Path $remoteState

    if (-not ([string]::Equals($sourceRoot, $localPackage, [System.StringComparison]::OrdinalIgnoreCase))) {
        foreach ($packageFile in $packageFiles) {
            $sourcePath = Join-Path $sourceRoot $packageFile
            $destinationPath = Join-Path $localPackage $packageFile
            Copy-Item -Force -Path $sourcePath -Destination $destinationPath
        }

        $sourcePlatformTools = Join-Path $sourceRoot "platform-tools"
        $destinationPlatformTools = Join-Path $localPackage "platform-tools"
        robocopy $sourcePlatformTools $destinationPlatformTools /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null

        $sourceMikroTik = Join-Path $sourceRoot "mikrotik"
        $destinationMikroTik = Join-Path $localPackage "mikrotik"
        robocopy $sourceMikroTik $destinationMikroTik /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
    }

    $resolvedKeySource = Ensure-LocalMikroTikKey -LocalKeyPath $localMikroTikKey -LocalPublicKeyPath $localMikroTikPublicKey

    Remove-Item -Force $localInfo, $localError, $localReport, $localRunStdout, $localRunStderr, $remoteInfo, $remoteError, $remoteReport, $remoteWrapper, $remoteStatus -ErrorAction SilentlyContinue

    Write-WrapperReport @(
        "Wrapper start: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        "Source root: $sourceRoot"
        "Local package: $localPackage"
        "Local start script: $localStartScript"
        "Local start script exists: $(Test-Path $localStartScript)"
        "Local adb exists: $(Test-Path $localAdbPath)"
        "Local key exists: $(Test-Path $localMikroTikKey)"
        "Resolved key source: $(if ([string]::IsNullOrWhiteSpace($resolvedKeySource)) { '<missing>' } else { $resolvedKeySource })"
    )

    $needsElevation = -not (Test-FirewallRulePresent -LocalPort 5037)

    try {
        if ($needsElevation) {
            $startArguments = @(
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", $localStartScript,
                "-AdbPath", $localAdbPath,
                "-MikroTikKeyPath", $localMikroTikKey
            )
            if ($routerAutomationEnabled -and
                -not [string]::IsNullOrWhiteSpace($routerHost) -and
                -not [string]::IsNullOrWhiteSpace($routerUser) -and
                -not [string]::IsNullOrWhiteSpace($routerWireGuardIp)) {
                $startArguments += @(
                    "-MikroTikHost", $routerHost,
                    "-MikroTikPort", "$routerPort",
                    "-MikroTikUser", $routerUser,
                    "-MikroTikWireGuardIp", $routerWireGuardIp,
                    "-MikroTikWireGuardPrefixLength", "$routerWireGuardPrefixLength"
                )
            } else {
                $startArguments += "-SkipMikroTikForward"
            }

            $proc = Start-Process `
                -FilePath "powershell.exe" `
                -Verb RunAs `
                -WindowStyle Hidden `
                -ArgumentList $startArguments `
                -PassThru
        } else {
            $startArguments = @(
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", $localStartScript,
                "-AdbPath", $localAdbPath,
                "-MikroTikKeyPath", $localMikroTikKey
            )
            if ($routerAutomationEnabled -and
                -not [string]::IsNullOrWhiteSpace($routerHost) -and
                -not [string]::IsNullOrWhiteSpace($routerUser) -and
                -not [string]::IsNullOrWhiteSpace($routerWireGuardIp)) {
                $startArguments += @(
                    "-MikroTikHost", $routerHost,
                    "-MikroTikPort", "$routerPort",
                    "-MikroTikUser", $routerUser,
                    "-MikroTikWireGuardIp", $routerWireGuardIp,
                    "-MikroTikWireGuardPrefixLength", "$routerWireGuardPrefixLength"
                )
            } else {
                $startArguments += "-SkipMikroTikForward"
            }

            $proc = Invoke-HiddenProcess `
                -FilePath "powershell.exe" `
                -ArgumentList $startArguments
        }
    } catch {
        @(
            "Wrapper exception during UAC launch:"
            ($_ | Out-String)
        ) | Set-Content -Path $remoteWrapper
        Write-StartStatus -Text "BLAD: Start ADB over WireGuard nie udal sie."
        Speak-Status -Text "Start ADB over WireGuard nie udal sie." -Success $false
        exit 1
    }

    $deadline = (Get-Date).AddSeconds(90)
    $status = "timeout"
    do {
        Start-Sleep -Seconds 2
        if (Test-Path $localError) {
            $status = "error"
            break
        }
        if (Test-Path $localInfo) {
            $status = "info"
            break
        }
    } while ((Get-Date) -lt $deadline)

    $exitCode = 1
    if ($status -eq "info") {
        $exitCode = 0
    }

    if ($status -eq "info") {
        $infoText = Get-Content -Raw -Path $localInfo
        if ($routerAutomationEnabled -and -not [string]::IsNullOrWhiteSpace($routerWireGuardIp) -and $infoText -match "Nie udalo sie ustawic forwardu na MikroTiku") {
            $targetMatch = [regex]::Match($infoText, 'Lokalny adres komputera z Pixelem:\s*([0-9.]+)')
            if ($targetMatch.Success) {
                $targetLanIp = $targetMatch.Groups[1].Value
                $cachedState = Read-StateFileMap -Path $localWrapperForwardState
                $cacheMatches = (
                    $cachedState["RouterHost"] -eq $routerHost -and
                    $cachedState["RouterPort"] -eq "$routerPort" -and
                    $cachedState["RouterUser"] -eq $routerUser -and
                    $cachedState["RouterWireGuardTargets"] -eq "$routerWireGuardIp:5037" -and
                    $cachedState["TargetLanIp"] -eq $targetLanIp -and
                    $cachedState["Port"] -eq "5037"
                )

                $cachedForwardReachable = $false
                if ($cacheMatches) {
                    $cachedForwardReachable = Wait-TcpReachable -TargetHost $routerWireGuardIp -Port 5037
                }

                $fallbackResult = $null
                if (-not $cacheMatches -or -not $cachedForwardReachable) {
                    $fallbackResult = Invoke-MikroTikWrapperFallback -TargetLanIp $targetLanIp
                    if ($fallbackResult.Verified) {
                        @(
                            "RouterHost=$routerHost"
                            "RouterPort=$routerPort"
                            "RouterUser=$routerUser"
                            "RouterWireGuardTargets=$routerWireGuardIp:5037"
                            "TargetLanIp=$targetLanIp"
                            "Port=5037"
                        ) | Set-Content -Path $localWrapperForwardState
                    }
                }

                $patched = $infoText -replace 'Nie udalo sie ustawic forwardu na MikroTiku:.*(\r?\n)+', ''
                $patched = $patched -replace ('(?m)^Connection closed by ' + [regex]::Escape($routerHost) + ' port ' + $routerPort + '\r?\n?'), ''
                $patched = $patched -replace 'Brak automatycznie wykrytych adresow routera WG\.', "$routerWireGuardIp:5037"
                if ($cacheMatches -and $cachedForwardReachable) {
                    $patched = $patched.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + "MikroTik: forward TCP 5037 z $routerWireGuardIp jest aktywny." + [Environment]::NewLine
                } elseif ($fallbackResult -and -not $fallbackResult.Verified) {
                    $patched = $patched.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + "MikroTik: serwer dziala lokalnie. Jesli klient nie polaczy sie z $routerWireGuardIp`:5037, sprawdz reguly NAT na routerze." + [Environment]::NewLine
                } elseif ($cacheMatches) {
                    $patched = $patched.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + "MikroTik: forward TCP 5037 z $routerWireGuardIp zostal odswiezony." + [Environment]::NewLine
                } else {
                    $patched = $patched.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + "MikroTik: forward TCP 5037 z $routerWireGuardIp zostal ustawiony." + [Environment]::NewLine
                }
                $patched | Set-Content -Path $localInfo
            }
        }
    }

    Build-LocalStartReport -ExitCode $exitCode
    Copy-IfExists -Source $localInfo -Target $remoteInfo
    Copy-IfExists -Source $localError -Target $remoteError
    Copy-IfExists -Source $localReport -Target $remoteReport

    @(
        "Wrapper start: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        "Source root: $sourceRoot"
        "Local package: $localPackage"
        "Local start script: $localStartScript"
        "Elevation needed: $needsElevation"
        "Launched PID: $($proc.Id)"
        "Observed status: $status"
        "Local report exists: $(Test-Path $localReport)"
        "Remote report exists: $(Test-Path $remoteReport)"
        "Remote info exists: $(Test-Path $remoteInfo)"
        "Remote error exists: $(Test-Path $remoteError)"
        "Wrapper end: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ) | Set-Content -Path $remoteWrapper

    if ($exitCode -eq 0) {
        Write-StartStatus -Text "OK: ADB over WireGuard uruchomiony."
        Speak-Status -Text "ADB over WireGuard uruchomiony." -Success $true
    } else {
        Write-StartStatus -Text "BLAD: Start ADB over WireGuard nie udal sie."
        Speak-Status -Text "Start ADB over WireGuard nie udal sie." -Success $false
    }

    exit $exitCode
} catch {
    @(
        "Wrapper exception:"
        ($_ | Out-String)
    ) | Set-Content -Path $remoteWrapper
    Write-StartStatus -Text "BLAD: Start ADB over WireGuard nie udal sie."
    Speak-Status -Text "Start ADB over WireGuard nie udal sie." -Success $false
    exit 1
}
