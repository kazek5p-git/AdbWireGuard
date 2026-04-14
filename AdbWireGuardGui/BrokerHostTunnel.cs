using System.Net.Sockets;
using System.Net.WebSockets;

namespace AdbWireGuardGui;

internal sealed class BrokerHostTunnel : IAsyncDisposable
{
    private readonly BrokerApiClient _apiClient;
    private readonly string _serverUrl;
    private readonly Guid _sessionId;
    private readonly string _connectToken;
    private readonly string _resumeToken;
    private readonly int _heartbeatIntervalSeconds;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;
    private Task? _heartbeatTask;
    private volatile bool _hasConnected;

    public BrokerHostTunnel(
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
    }

    public void Start()
    {
        _heartbeatTask ??= Task.Run(() => RunHeartbeatLoopAsync(_cts.Token));
        _runTask ??= Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", 5037, cancellationToken);

                using var socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(Math.Max(10, _heartbeatIntervalSeconds));

                var token = _hasConnected ? _resumeToken : _connectToken;
                var uri = BrokerTransportHelpers.BuildWebSocketUri(_serverUrl, $"ws/host/{_sessionId}?token={Uri.EscapeDataString(token)}");
                await socket.ConnectAsync(uri, cancellationToken);

                _hasConnected = true;
                _log("Host połączenia kodem: połączono z serwerem pośrednim i lokalnym ADB.");
                await BrokerTransportHelpers.BridgeTcpAndWebSocketAsync(tcpClient, socket, cancellationToken);

                _log("Host połączenia kodem: połączenie zostało rozłączone, trwa ponowienie.");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"Host połączenia kodem: błąd połączenia: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
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
                await _apiClient.RecordHeartbeatAsync(_serverUrl, _sessionId, "host", _resumeToken, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"Host połączenia kodem: heartbeat nie powiódł się: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_runTask is not null)
        {
            try
            {
                await _runTask;
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
