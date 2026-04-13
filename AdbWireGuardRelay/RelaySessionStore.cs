using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace AdbWireGuardRelay;

public sealed class RelaySessionStore
{
    private readonly RelayOptions _options;
    private readonly ILogger<RelaySessionStore> _logger;
    private readonly ConcurrentDictionary<Guid, RelaySession> _sessions = new();

    public RelaySessionStore(RelayOptions options, ILogger<RelaySessionStore> logger)
    {
        _options = options;
        _logger = logger;
    }

    public CreateSessionResponse CreateSession(string hostToken, string? deviceName, int? requestedTtlMinutes)
    {
        var activeSessionsForHost = _sessions.Values.Count(session =>
            session.HostToken == hostToken &&
            session.Status is RelaySessionStatus.PendingHost or RelaySessionStatus.WaitingForClient or RelaySessionStatus.Active &&
            session.ExpiresAtUtc > DateTimeOffset.UtcNow);

        if (activeSessionsForHost >= _options.MaxSessionsPerHost)
        {
            throw new InvalidOperationException("Host ma juz maksymalna liczbe aktywnych sesji.");
        }

        var ttlMinutes = requestedTtlMinutes.GetValueOrDefault(_options.DefaultSessionTtlMinutes);
        ttlMinutes = Math.Clamp(ttlMinutes, 1, _options.MaxSessionTtlMinutes);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes);

        var session = new RelaySession(
            Id: Guid.NewGuid(),
            HostToken: hostToken,
            DeviceName: string.IsNullOrWhiteSpace(deviceName) ? "Android host" : deviceName.Trim(),
            PairCode: CreatePairCode(),
            HostConnectToken: CreateSecretToken(),
            ExpiresAtUtc: expiresAt);

        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException("Nie udalo sie utworzyc sesji relay.");
        }

        _logger.LogInformation("Created relay session {SessionId} for {DeviceName}", session.Id, session.DeviceName);

        return new CreateSessionResponse(
            session.Id,
            session.PairCode,
            session.HostConnectToken,
            session.ExpiresAtUtc,
            (int)(session.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds);
    }

    public ClaimSessionResponse ClaimSession(string pairCode, string? clientName)
    {
        var session = _sessions.Values.FirstOrDefault(candidate =>
            candidate.Status is RelaySessionStatus.PendingHost or RelaySessionStatus.WaitingForClient &&
            candidate.ExpiresAtUtc > DateTimeOffset.UtcNow &&
            string.Equals(candidate.PairCode, NormalizePairCode(pairCode), StringComparison.Ordinal));

        if (session is null)
        {
            throw new InvalidOperationException("Nie znaleziono aktywnej sesji dla podanego kodu.");
        }

        lock (session.SyncRoot)
        {
            EnsureSessionUsable(session);

            session.ClaimAttempts++;
            if (session.ClaimAttempts > _options.MaxPendingClaimAttempts)
            {
                session.Status = RelaySessionStatus.Expired;
                throw new InvalidOperationException("Sesja relay zostala zablokowana po zbyt wielu probach polaczenia.");
            }

            if (!string.IsNullOrWhiteSpace(session.ClientConnectToken))
            {
                throw new InvalidOperationException("Kod zostal juz wykorzystany.");
            }

            session.ClientName = string.IsNullOrWhiteSpace(clientName) ? "Client" : clientName.Trim();
            session.ClientConnectToken = CreateSecretToken();
            if (session.HostSocket is null || session.HostSocket.State != WebSocketState.Open)
            {
                session.Status = RelaySessionStatus.PendingHost;
            }
            else
            {
                session.Status = RelaySessionStatus.WaitingForClient;
            }

            _logger.LogInformation("Claimed relay session {SessionId} by {ClientName}", session.Id, session.ClientName);

            return new ClaimSessionResponse(session.Id, session.ClientConnectToken, session.ExpiresAtUtc);
        }
    }

    public SessionStatusResponse GetSessionStatus(Guid sessionId, string hostToken)
    {
        var session = GetSessionForHost(sessionId, hostToken);
        lock (session.SyncRoot)
        {
            return ToStatusResponse(session);
        }
    }

    public void CloseSession(Guid sessionId, string hostToken)
    {
        var session = GetSessionForHost(sessionId, hostToken);
        lock (session.SyncRoot)
        {
            session.Status = RelaySessionStatus.Closed;
        }

        _ = CloseSocketsAndRemoveAsync(session, "Closed by host");
    }

    public void AttachHost(Guid sessionId, string connectToken, WebSocket socket)
    {
        var session = GetSession(sessionId);
        lock (session.SyncRoot)
        {
            EnsureSessionUsable(session);
            if (!string.Equals(session.HostConnectToken, connectToken, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Nieprawidlowy token hosta.");
            }

            if (session.HostSocket is not null && session.HostSocket.State == WebSocketState.Open)
            {
                throw new InvalidOperationException("Host jest juz podlaczony do tej sesji.");
            }

            session.HostSocket = socket;
            session.Status = string.IsNullOrWhiteSpace(session.ClientConnectToken)
                ? RelaySessionStatus.PendingHost
                : RelaySessionStatus.WaitingForClient;
            TryStartRelay(session);
        }
    }

    public void AttachClient(Guid sessionId, string connectToken, WebSocket socket)
    {
        var session = GetSession(sessionId);
        lock (session.SyncRoot)
        {
            EnsureSessionUsable(session);
            if (string.IsNullOrWhiteSpace(session.ClientConnectToken) ||
                !string.Equals(session.ClientConnectToken, connectToken, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Nieprawidlowy token klienta.");
            }

            if (session.ClientSocket is not null && session.ClientSocket.State == WebSocketState.Open)
            {
                throw new InvalidOperationException("Klient jest juz podlaczony do tej sesji.");
            }

            session.ClientSocket = socket;
            session.Status = RelaySessionStatus.WaitingForClient;
            TryStartRelay(session);
        }
    }

    public IReadOnlyList<Guid> CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _sessions.Values
            .Where(session => session.ExpiresAtUtc <= now || session.Status is RelaySessionStatus.Closed or RelaySessionStatus.Expired)
            .Select(session => session.Id)
            .ToList();

        foreach (var sessionId in expired)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                _ = CloseSocketsAndRemoveAsync(session, "Expired");
            }
        }

        return expired;
    }

    private RelaySession GetSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Nie znaleziono sesji relay.");
        }

        return session;
    }

    private RelaySession GetSessionForHost(Guid sessionId, string hostToken)
    {
        var session = GetSession(sessionId);
        if (!string.Equals(session.HostToken, hostToken, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Ten host nie ma dostepu do wskazanej sesji.");
        }

        return session;
    }

    private void EnsureSessionUsable(RelaySession session)
    {
        if (session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            session.Status = RelaySessionStatus.Expired;
            throw new InvalidOperationException("Sesja relay wygasla.");
        }

        if (session.Status is RelaySessionStatus.Closed or RelaySessionStatus.Expired)
        {
            throw new InvalidOperationException("Sesja relay jest juz zamknieta.");
        }
    }

    private void TryStartRelay(RelaySession session)
    {
        if (session.RelayStarted)
        {
            return;
        }

        if (session.HostSocket?.State == WebSocketState.Open &&
            session.ClientSocket?.State == WebSocketState.Open)
        {
            session.RelayStarted = true;
            session.Status = RelaySessionStatus.Active;
            _ = Task.Run(() => RelayAsync(session));
        }
    }

    private async Task RelayAsync(RelaySession session)
    {
        var hostSocket = session.HostSocket!;
        var clientSocket = session.ClientSocket!;
        _logger.LogInformation("Relay started for session {SessionId}", session.Id);

        try
        {
            await Task.WhenAny(
                RelayOneWayAsync(hostSocket, clientSocket, session.Id, "host->client"),
                RelayOneWayAsync(clientSocket, hostSocket, session.Id, "client->host"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Relay failed for session {SessionId}", session.Id);
        }
        finally
        {
            await CloseSocketsAndRemoveAsync(session, "Relay finished");
        }
    }

    private async Task RelayOneWayAsync(WebSocket source, WebSocket destination, Guid sessionId, string direction)
    {
        var buffer = new byte[64 * 1024];
        while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
        {
            var result = await source.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("WebSocket close frame in {Direction} for session {SessionId}", direction, sessionId);
                break;
            }

            await destination.SendAsync(
                buffer.AsMemory(0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                CancellationToken.None);
        }
    }

    private async Task CloseSocketsAndRemoveAsync(RelaySession session, string reason)
    {
        if (_sessions.TryRemove(session.Id, out _))
        {
            _logger.LogInformation("Closing relay session {SessionId}: {Reason}", session.Id, reason);
        }

        session.Status = RelaySessionStatus.Closed;
        await CloseSocketIfOpenAsync(session.HostSocket);
        await CloseSocketIfOpenAsync(session.ClientSocket);
    }

    private static async Task CloseSocketIfOpenAsync(WebSocket? socket)
    {
        if (socket is null)
        {
            return;
        }

        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session finished", CancellationToken.None);
            }
        }
        catch
        {
            // Ignore cleanup exceptions.
        }
    }

    private static string CreatePairCode()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }

    private static string CreateSecretToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string NormalizePairCode(string pairCode)
    {
        return pairCode.Trim().Replace("-", string.Empty).ToUpperInvariant();
    }

    private static SessionStatusResponse ToStatusResponse(RelaySession session) =>
        new(
            session.Id,
            session.DeviceName,
            session.Status.ToString(),
            session.ExpiresAtUtc,
            session.HostSocket?.State == WebSocketState.Open,
            session.ClientSocket?.State == WebSocketState.Open,
            session.RelayStarted,
            session.ClaimAttempts);

    private sealed class RelaySession
    {
        public RelaySession(Guid Id, string HostToken, string DeviceName, string PairCode, string HostConnectToken, DateTimeOffset ExpiresAtUtc)
        {
            this.Id = Id;
            this.HostToken = HostToken;
            this.DeviceName = DeviceName;
            this.PairCode = PairCode;
            this.HostConnectToken = HostConnectToken;
            this.ExpiresAtUtc = ExpiresAtUtc;
        }

        public Guid Id { get; }
        public string HostToken { get; }
        public string DeviceName { get; }
        public string PairCode { get; }
        public string HostConnectToken { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public string? ClientConnectToken { get; set; }
        public string? ClientName { get; set; }
        public RelaySessionStatus Status { get; set; } = RelaySessionStatus.PendingHost;
        public int ClaimAttempts { get; set; }
        public bool RelayStarted { get; set; }
        public WebSocket? HostSocket { get; set; }
        public WebSocket? ClientSocket { get; set; }
        public object SyncRoot { get; } = new();
    }
}
