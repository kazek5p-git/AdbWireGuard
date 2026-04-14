using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace AdbWireGuardGui;

internal static class BrokerTransportHelpers
{
    public static Uri BuildWebSocketUri(string serverUrl, string pathAndQuery)
    {
        var baseUri = new Uri(BrokerApiClient.NormalizeServerUrl(serverUrl), UriKind.Absolute);
        var builder = new UriBuilder(baseUri);
        builder.Scheme = builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        builder.Path = pathAndQuery.TrimStart('/');
        return builder.Uri;
    }

    public static async Task PumpTcpToWebSocketAsync(NetworkStream stream, WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        while (!cancellationToken.IsCancellationRequested &&
               socket.State == WebSocketState.Open)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await socket.SendAsync(
                buffer.AsMemory(0, read),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken);
        }
    }

    public static async Task PumpWebSocketToTcpAsync(WebSocket socket, NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        while (!cancellationToken.IsCancellationRequested &&
               socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            while (!result.EndOfMessage)
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            }

            await stream.FlushAsync(cancellationToken);
        }
    }

    public static async Task BridgeTcpAndWebSocketAsync(TcpClient client, ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = linkedCts.Token;
        using var stream = client.GetStream();

        var tcpToWsTask = PumpTcpToWebSocketAsync(stream, socket, linkedToken);
        var wsToTcpTask = PumpWebSocketToTcpAsync(socket, stream, linkedToken);

        var completed = await Task.WhenAny(tcpToWsTask, wsToTcpTask);
        linkedCts.Cancel();

        try
        {
            await completed;
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }

        try
        {
            await Task.WhenAll(tcpToWsTask, wsToTcpTask);
        }
        catch (OperationCanceledException)
        {
            // Ignore linked cancellation on shutdown.
        }
    }

    public static int GetAvailableLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
