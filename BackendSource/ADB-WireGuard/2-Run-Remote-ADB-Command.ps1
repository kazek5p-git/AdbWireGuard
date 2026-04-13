[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerHost,
    [int]$Port = 5037,
    [string]$AdbPath = "adb.exe",
    [string]$AdbCommand = "devices",
    [int]$StartupWaitSeconds = 8,
    [int]$RetryCount = 2,
    [int]$RetryDelaySeconds = 1
)

$ErrorActionPreference = "Stop"

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

function Test-TcpPort {
    param(
        [string]$TargetHost,
        [int]$TargetPort,
        [int]$TimeoutMs = 1000
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $asyncResult = $client.BeginConnect($TargetHost, $TargetPort, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
            return $false
        }

        $client.EndConnect($asyncResult)
        return $true
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

function Test-TransientAdbConnectionFailure {
    param(
        [string[]]$OutputLines
    )

    if ($null -eq $OutputLines -or $OutputLines.Count -eq 0) {
        return $false
    }

    $joined = ($OutputLines -join "`n")
    return (
        $joined -match 'cannot connect to daemon' -or
        $joined -match 'failed to connect' -or
        $joined -match 'actively refused' -or
        $joined -match 'No connection could be made' -or
        $joined -match 'timed out'
    )
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$adbExe = Resolve-AdbPath -PreferredPath $AdbPath -ScriptRoot $scriptRoot
$env:ADB_SERVER_SOCKET = "tcp:$($ServerHost):$($Port)"
$adbArgs = [string[]]@()
if (-not [string]::IsNullOrWhiteSpace($AdbCommand)) {
    $adbArgs = @([regex]::Matches($AdbCommand, '"[^"]+"|\S+') | ForEach-Object {
        $_.Value.Trim('"')
    })
}
if ($adbArgs.Count -eq 0) {
    $adbArgs = [string[]]@("devices")
}

Write-Host "Uzywam zdalnego adb server: $env:ADB_SERVER_SOCKET"
Write-Host "Polecenie: adb $($adbArgs -join ' ')"

for ($attempt = 1; $attempt -le $StartupWaitSeconds; $attempt++) {
    if (Test-TcpPort -TargetHost $ServerHost -TargetPort $Port) {
        break
    }

    if ($attempt -eq $StartupWaitSeconds) {
        throw "Nie mozna nawiazac polaczenia z $ServerHost`:$Port po $StartupWaitSeconds s."
    }

    Start-Sleep -Seconds 1
}

$lastOutput = @()
$lastExitCode = 1

for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
    $output = @(& $adbExe @adbArgs 2>&1)
    $exitCode = $LASTEXITCODE
    $lastOutput = $output
    $lastExitCode = $exitCode

    if ($exitCode -eq 0) {
        foreach ($line in $output) {
            Write-Host $line
        }
        exit 0
    }

    if ($attempt -lt $RetryCount -and (Test-TransientAdbConnectionFailure -OutputLines $output)) {
        Start-Sleep -Seconds $RetryDelaySeconds
        continue
    }

    break
}

if (Test-TransientAdbConnectionFailure -OutputLines $lastOutput) {
    throw "Nie mozna nawiazac polaczenia ze zdalnym adb pod $ServerHost`:$Port."
}

foreach ($line in $lastOutput) {
    if (-not [string]::IsNullOrWhiteSpace($line)) {
        Write-Host $line
    }
}

exit $lastExitCode
