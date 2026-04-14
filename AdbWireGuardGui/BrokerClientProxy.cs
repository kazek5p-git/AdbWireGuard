using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace AdbWireGuardGui;

internal sealed class BrokerClientProxy : IAsyncDisposable
{
    private readonly BrokerApiClient _apiClient;
    private readonly string _serverUrl;
    private readonly Guid _sessionId;
    private readonly string _connectToken;
    private readonly string _resumeToken;
    private readonly int _heartbeatIntervalSeconds;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _cts = new();
    private readonly TcpListener _listener;
    private Task? _acceptLoopTask;
    private Task? _heartbeatTask;
    private volatile bool _hasConnected;

    public BrokerClientProxy(
        BrokerApiClient apiClient,
        string serverUrl,
        Guid sessionId,
        string connectToken,
        string resumeToken,
        int heartbeatIntervalSeconds,
        Action<string> log)
    {
        _apiClient = apiClient;
        _serverUrl = serverUrl;
        _sessionId = sessionId;
        _connectToken = connectToken;
        _resumeToken = resumeToken;
        _heartbeatIntervalSeconds = Math.Max(heartbeatIntervalSeconds, 10);
        _log = log;
        LocalPort = BrokerTransportHelpers.GetAvailableLoopbackPort();
        _listener = new TcpListener(IPAddress.Loopback, LocalPort);
    }

    public int LocalPort { get; }

    public void Start()
    {
        _listener.Start();
        _heartbeatTask ??= Task.Run(() => RunHeartbeatLoopAsync(_cts.Token));
        _acceptLoopTask ??= Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        _log($"Klient połączenia kodem: lokalny port loopback {LocalPort} jest gotowy.");

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? localClient = null;
            try
            {
                localClient = await _listener.AcceptTcpClientAsync(cancellationToken);
                _log("Klient połączenia kodem: przyjęto lokalne połączenie ADB.");

                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(10, _heartbeatIntervalSeconds));

                var token = _hasConnected ? _resumeToken : _connectToken;
                var uri = BrokerTransportHelpers.BuildWebSocketUri(_serverUrl, $"ws/client/{_sessionId}?token={Uri.EscapeDataString(token)}");
                await socket.ConnectAsync(uri, cancellationToken);

                _hasConnected = true;
                _log("Klient połączenia kodem: połączono z serwerem pośrednim.");
                await BrokerTransportHelpers.BridgeTcpAndWebSocketAsync(localClient, socket, cancellationToken);

                _log("Klient połączenia kodem: połączenie ADB zostało zamknięte.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"Klient połączenia kodem: błąd połączenia: {ex.Message}");
                try
                {
                    localClient?.Dispose();
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_heartbeatIntervalSeconds), cancellationToken);
                await _apiClient.RecordHeartbeatAsync(_serverUrl, _sessionId, "client", _resumeToken, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"Klient połączenia kodem: heartbeat nie powiódł się: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignore cleanup errors
        }

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        if (_heartbeatTask is not null)
        {
            try
            {
                await _heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        _cts.Dispose();
    }
}
