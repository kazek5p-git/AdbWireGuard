[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$stdoutPath = Join-Path $PSScriptRoot "relay-test-stdout.txt"
$stderrPath = Join-Path $PSScriptRoot "relay-test-stderr.txt"

Remove-Item $stdoutPath -ErrorAction SilentlyContinue
Remove-Item $stderrPath -ErrorAction SilentlyContinue

$env:ADBWG_RELAY_HOST_TOKENS = "test-host-token"
$process = Start-Process dotnet `
    -WorkingDirectory $PSScriptRoot `
    -ArgumentList @("run", "-c", "Release", "--no-build", "--urls", "http://127.0.0.1:5127") `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

Start-Sleep -Seconds 4

try
{
    $health = Invoke-RestMethod -Uri "http://127.0.0.1:5127/healthz"
    $session = Invoke-RestMethod `
        -Uri "http://127.0.0.1:5127/api/v1/relay/sessions" `
        -Method Post `
        -Headers @{ Authorization = "Bearer test-host-token" } `
        -ContentType "application/json" `
        -Body (@{ deviceName = "Pixel test"; requestedTtlMinutes = 5 } | ConvertTo-Json)
    $claim = Invoke-RestMethod `
        -Uri "http://127.0.0.1:5127/api/v1/relay/claim" `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ pairCode = $session.pairCode; clientName = "Client test" } | ConvertTo-Json)
    $hostHeartbeat = Invoke-RestMethod `
        -Uri ("http://127.0.0.1:5127/api/v1/relay/sessions/{0}/heartbeat" -f $session.sessionId) `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ role = "host"; resumeToken = $session.hostResumeToken } | ConvertTo-Json)
    $clientHeartbeat = Invoke-RestMethod `
        -Uri ("http://127.0.0.1:5127/api/v1/relay/sessions/{0}/heartbeat" -f $session.sessionId) `
        -Method Post `
        -ContentType "application/json" `
        -Body (@{ role = "client"; resumeToken = $claim.clientResumeToken } | ConvertTo-Json)
    $status = Invoke-RestMethod `
        -Uri ("http://127.0.0.1:5127/api/v1/relay/sessions/{0}" -f $session.sessionId) `
        -Headers @{ Authorization = "Bearer test-host-token" }

    [pscustomobject]@{
        health = $health
        session = $session
        claim = $claim
        hostHeartbeat = $hostHeartbeat
        clientHeartbeat = $clientHeartbeat
        status = $status
    } | ConvertTo-Json -Depth 8
}
finally
{
    if ($process -and -not $process.HasExited)
    {
        Stop-Process -Id $process.Id -Force
    }
}
