using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace AdbWireGuardRelay;

public sealed class SessionBroker
{
    private readonly BrokerOptions _options;
    private readonly ILogger<SessionBroker> _logger;
    private readonly ConcurrentDictionary<Guid, BrokerSession> _sessions = new();

    public SessionBroker(BrokerOptions options, ILogger<SessionBroker> logger)
    {
        _options = options;
        _logger = logger;
    }

    public CreateSessionResponse CreateSession(string hostToken, string? deviceName, int? requestedTtlMinutes)
    {
        var activeSessionsForHost = _sessions.Values.Count(session =>
            session.HostToken == hostToken &&
            session.Status is SessionLifecycleStatus.PendingHost or SessionLifecycleStatus.WaitingForClient or SessionLifecycleStatus.Active &&
            session.ExpiresAtUtc > DateTimeOffset.UtcNow);

        if (activeSessionsForHost >= _options.MaxSessionsPerHost)
        {
            throw new InvalidOperationException("Host ma juz maksymalna liczbe aktywnych sesji.");
        }

        var ttlMinutes = requestedTtlMinutes.GetValueOrDefault(_options.DefaultSessionTtlMinutes);
        ttlMinutes = Math.Clamp(ttlMinutes, 1, _options.MaxSessionTtlMinutes);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes);

        var session = new BrokerSession(
            Id: Guid.NewGuid(),
            HostToken: hostToken,
            DeviceName: string.IsNullOrWhiteSpace(deviceName) ? "Android host" : deviceName.Trim(),
            PairCode: CreatePairCode(),
            HostConnectToken: CreateSecretToken(),
            HostResumeToken: CreateSecretToken(),
            ExpiresAtUtc: expiresAt);

        if (!_sessions.TryAdd(session.Id, session))
        {
            throw new InvalidOperationException("Nie udało się utworzyć sesji pośredniej.");
        }

        _logger.LogInformation("Created broker session {SessionId} for {DeviceName}", session.Id, session.DeviceName);

        return new CreateSessionResponse(
            session.Id,
            session.PairCode,
            session.HostConnectToken,
            session.HostResumeToken,
            session.ExpiresAtUtc,
            (int)(session.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds,
            _options.ReconnectGraceSeconds,
            _options.HeartbeatIntervalSeconds);
    }

    public ClaimSessionResponse ClaimSession(string pairCode, string? clientName)
    {
        var session = _sessions.Values.FirstOrDefault(candidate =>
            candidate.Status is SessionLifecycleStatus.PendingHost or SessionLifecycleStatus.WaitingForClient &&
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
                session.Status = SessionLifecycleStatus.Expired;
                throw new InvalidOperationException("Sesja pośrednia została zablokowana po zbyt wielu próbach połączenia.");
            }

            if (!string.IsNullOrWhiteSpace(session.ClientConnectToken))
            {
                throw new InvalidOperationException("Kod zostal juz wykorzystany.");
            }

            session.ClientName = string.IsNullOrWhiteSpace(clientName) ? "Client" : clientName.Trim();
            session.ClientConnectToken = CreateSecretToken();
            session.ClientResumeToken = CreateSecretToken();
            session.ClientLastSeenUtc = DateTimeOffset.UtcNow;
            session.ClientReconnectDeadlineUtc = null;
            session.Status = GetComputedStatus(session);

            _logger.LogInformation("Claimed broker session {SessionId} by {ClientName}", session.Id, session.ClientName);

            return new ClaimSessionResponse(
                session.Id,
                session.ClientConnectToken,
                session.ClientResumeToken,
                session.ExpiresAtUtc,
                _options.ReconnectGraceSeconds,
                _options.HeartbeatIntervalSeconds);
        }
    }

    public SessionStatusResponse RecordHeartbeat(Guid sessionId, string role, string resumeToken)
    {
        var session = GetSession(sessionId);
        lock (session.SyncRoot)
        {
            EnsureSessionUsable(session);
            var now = DateTimeOffset.UtcNow;
            if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(session.HostResumeToken, resumeToken, StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException("Nieprawidlowy token hosta do heartbeat.");
                }

                session.HostLastSeenUtc = now;
                session.HostReconnectDeadlineUtc = session.HostSocket?.State == WebSocketState.Open
                    ? null
                    : now.AddSeconds(_options.ReconnectGraceSeconds);
            }
            else if (string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(session.ClientResumeToken) ||
                    !string.Equals(session.ClientResumeToken, resumeToken, StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException("Nieprawidlowy token klienta do heartbeat.");
                }

                session.ClientLastSeenUtc = now;
                session.ClientReconnectDeadlineUtc = session.ClientSocket?.State == WebSocketState.Open
                    ? null
                    : now.AddSeconds(_options.ReconnectGraceSeconds);
            }
            else
            {
                throw new InvalidOperationException("Nieprawidlowa rola heartbeat.");
            }

            session.Status = GetComputedStatus(session);
            return ToStatusResponse(session);
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
            session.Status = SessionLifecycleStatus.Closed;
        }

        _ = CloseSocketsAndRemoveAsync(session, "Closed by host");
    }

    public void AttachHost(Guid sessionId, string connectToken, WebSocket socket)
    {
        var session = GetSession(sessionId);
        lock (session.SyncRoot)
        {
            EnsureSessionUsable(session);
            if (!IsRoleTokenValid(session.HostConnectToken, session.HostResumeToken, connectToken))
            {
                throw new UnauthorizedAccessException("Nieprawidlowy token hosta.");
            }

            if (session.HostSocket is not null && session.HostSocket.State == WebSocketState.Open)
            {
                throw new InvalidOperationException("Host jest juz podlaczony do tej sesji.");
            }

            session.HostSocket = socket;
            session.HostLastSeenUtc = DateTimeOffset.UtcNow;
            session.HostReconnectDeadlineUtc = null;
            session.Status = GetComputedStatus(session);
            TryStartBridge(session);
        }
    }

    public void AttachClient(Guid sessionId, string connectToken, WebSocket socket)
    {
        var session = GetSession(sessionId);
        lock (session.SyncRoot)
        {
            EnsureSessionUsable(session);
            if (string.IsNullOrWhiteSpace(session.ClientConnectToken) ||
                !IsRoleTokenValid(session.ClientConnectToken, session.ClientResumeToken, connectToken))
            {
                throw new UnauthorizedAccessException("Nieprawidlowy token klienta.");
            }

            if (session.ClientSocket is not null && session.ClientSocket.State == WebSocketState.Open)
            {
                throw new InvalidOperationException("Klient jest juz podlaczony do tej sesji.");
            }

            session.ClientSocket = socket;
            session.ClientLastSeenUtc = DateTimeOffset.UtcNow;
            session.ClientReconnectDeadlineUtc = null;
            session.Status = GetComputedStatus(session);
            TryStartBridge(session);
        }
    }

    public void MarkHostDisconnected(Guid sessionId, WebSocket socket)
    {
        MarkRoleDisconnected(sessionId, socket, isHost: true);
    }

    public void MarkClientDisconnected(Guid sessionId, WebSocket socket)
    {
        MarkRoleDisconnected(sessionId, socket, isHost: false);
    }

    public IReadOnlyList<Guid> CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = _sessions.Values
            .Where(session => ShouldRemoveSession(session, now))
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

    private BrokerSession GetSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException("Nie znaleziono sesji pośredniej.");
        }

        return session;
    }

    private BrokerSession GetSessionForHost(Guid sessionId, string hostToken)
    {
        var session = GetSession(sessionId);
        if (!string.Equals(session.HostToken, hostToken, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Ten host nie ma dostepu do wskazanej sesji.");
        }

        return session;
    }

    private void EnsureSessionUsable(BrokerSession session)
    {
        if (session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            session.Status = SessionLifecycleStatus.Expired;
            throw new InvalidOperationException("Sesja pośrednia wygasła.");
        }

        if (session.Status is SessionLifecycleStatus.Closed or SessionLifecycleStatus.Expired)
        {
            throw new InvalidOperationException("Sesja pośrednia jest już zamknięta.");
        }
    }

    private void TryStartBridge(BrokerSession session)
    {
        if (session.BridgeStarted)
        {
            return;
        }

        if (session.HostSocket?.State == WebSocketState.Open &&
            session.ClientSocket?.State == WebSocketState.Open)
        {
            session.BridgeStarted = true;
            session.Status = SessionLifecycleStatus.Active;
            var hostSocket = session.HostSocket;
            var clientSocket = session.ClientSocket;
            _ = Task.Run(() => BridgeAsync(session, hostSocket, clientSocket));
        }
    }

    private async Task BridgeAsync(BrokerSession session, WebSocket hostSocket, WebSocket clientSocket)
    {
        _logger.LogInformation("Bridge started for session {SessionId}", session.Id);

        try
        {
            await Task.WhenAny(
                PipeOneWayAsync(hostSocket, clientSocket, session.Id, "host->client"),
                PipeOneWayAsync(clientSocket, hostSocket, session.Id, "client->host"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bridge failed for session {SessionId}", session.Id);
        }
        finally
        {
            lock (session.SyncRoot)
            {
                if (ReferenceEquals(session.HostSocket, hostSocket) &&
                    ReferenceEquals(session.ClientSocket, clientSocket))
                {
                    session.BridgeStarted = false;
                    session.Status = GetComputedStatus(session);
                }
            }
        }
    }

    private async Task PipeOneWayAsync(WebSocket source, WebSocket destination, Guid sessionId, string direction)
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

             if (_sessions.TryGetValue(sessionId, out var session))
             {
                 lock (session.SyncRoot)
                 {
                     var now = DateTimeOffset.UtcNow;
                     if (ReferenceEquals(source, session.HostSocket))
                     {
                         session.HostLastSeenUtc = now;
                     }
                     else if (ReferenceEquals(source, session.ClientSocket))
                     {
                         session.ClientLastSeenUtc = now;
                     }
                 }
             }

            await destination.SendAsync(
                buffer.AsMemory(0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                CancellationToken.None);
        }
    }

    private async Task CloseSocketsAndRemoveAsync(BrokerSession session, string reason)
    {
        if (_sessions.TryRemove(session.Id, out _))
        {
            _logger.LogInformation("Closing broker session {SessionId}: {Reason}", session.Id, reason);
        }

        session.Status = SessionLifecycleStatus.Closed;
        session.BridgeStarted = false;
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

    private bool ShouldRemoveSession(BrokerSession session, DateTimeOffset now)
    {
        lock (session.SyncRoot)
        {
            if (session.Status is SessionLifecycleStatus.Closed or SessionLifecycleStatus.Expired)
            {
                return true;
            }

            if (session.ExpiresAtUtc <= now)
            {
                session.Status = SessionLifecycleStatus.Expired;
                return true;
            }

            if (session.HostReconnectDeadlineUtc.HasValue &&
                session.HostReconnectDeadlineUtc.Value <= now &&
                session.HostSocket?.State != WebSocketState.Open)
            {
                session.Status = SessionLifecycleStatus.Expired;
                return true;
            }

            if (session.ClientReconnectDeadlineUtc.HasValue &&
                session.ClientReconnectDeadlineUtc.Value <= now &&
                session.ClientSocket?.State != WebSocketState.Open)
            {
                session.Status = SessionLifecycleStatus.Expired;
                return true;
            }

            if (session.HostLastSeenUtc.HasValue &&
                session.HostSocket?.State != WebSocketState.Open &&
                (now - session.HostLastSeenUtc.Value).TotalSeconds > _options.HeartbeatStaleSeconds &&
                string.IsNullOrWhiteSpace(session.ClientConnectToken))
            {
                session.Status = SessionLifecycleStatus.Expired;
                return true;
            }

            return false;
        }
    }

    private void MarkRoleDisconnected(Guid sessionId, WebSocket socket, bool isHost)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return;
        }

        lock (session.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            if (isHost)
            {
                if (!ReferenceEquals(session.HostSocket, socket))
                {
                    return;
                }

                session.HostSocket = null;
                session.HostLastSeenUtc = now;
                session.HostReconnectDeadlineUtc = now.AddSeconds(_options.ReconnectGraceSeconds);
            }
            else
            {
                if (!ReferenceEquals(session.ClientSocket, socket))
                {
                    return;
                }

                session.ClientSocket = null;
                session.ClientLastSeenUtc = now;
                session.ClientReconnectDeadlineUtc = now.AddSeconds(_options.ReconnectGraceSeconds);
            }

            session.BridgeStarted = false;
            session.Status = GetComputedStatus(session);
        }
    }

    private static bool IsRoleTokenValid(string? connectToken, string? resumeToken, string presentedToken)
    {
        return (!string.IsNullOrWhiteSpace(connectToken) && string.Equals(connectToken, presentedToken, StringComparison.Ordinal)) ||
               (!string.IsNullOrWhiteSpace(resumeToken) && string.Equals(resumeToken, presentedToken, StringComparison.Ordinal));
    }

    private static SessionLifecycleStatus GetComputedStatus(BrokerSession session)
    {
        if (session.Status is SessionLifecycleStatus.Closed or SessionLifecycleStatus.Expired)
        {
            return session.Status;
        }

        var hostConnected = session.HostSocket?.State == WebSocketState.Open;
        var clientConnected = session.ClientSocket?.State == WebSocketState.Open;
        var clientClaimed = !string.IsNullOrWhiteSpace(session.ClientConnectToken);

        if (hostConnected && clientConnected)
        {
            return SessionLifecycleStatus.Active;
        }

        if (!clientClaimed)
        {
            return hostConnected ? SessionLifecycleStatus.WaitingForClient : SessionLifecycleStatus.PendingHost;
        }

        if (hostConnected && !clientConnected)
        {
            return SessionLifecycleStatus.WaitingForClientReconnect;
        }

        if (!hostConnected && clientConnected)
        {
            return SessionLifecycleStatus.WaitingForHostReconnect;
        }

        return SessionLifecycleStatus.WaitingForReconnect;
    }

    private static SessionStatusResponse ToStatusResponse(BrokerSession session) =>
        new(
            session.Id,
            session.DeviceName,
            GetComputedStatus(session).ToString(),
            session.ExpiresAtUtc,
            session.HostSocket?.State == WebSocketState.Open,
            session.ClientSocket?.State == WebSocketState.Open,
            session.BridgeStarted,
            session.ClaimAttempts,
            session.HostLastSeenUtc,
            session.ClientLastSeenUtc,
            session.HostReconnectDeadlineUtc,
            session.ClientReconnectDeadlineUtc);

    private sealed class BrokerSession
    {
        public BrokerSession(Guid Id, string HostToken, string DeviceName, string PairCode, string HostConnectToken, string HostResumeToken, DateTimeOffset ExpiresAtUtc)
        {
            this.Id = Id;
            this.HostToken = HostToken;
            this.DeviceName = DeviceName;
            this.PairCode = PairCode;
            this.HostConnectToken = HostConnectToken;
            this.HostResumeToken = HostResumeToken;
            this.ExpiresAtUtc = ExpiresAtUtc;
            this.HostLastSeenUtc = DateTimeOffset.UtcNow;
        }

        public Guid Id { get; }
        public string HostToken { get; }
        public string DeviceName { get; }
        public string PairCode { get; }
        public string HostConnectToken { get; }
        public string HostResumeToken { get; }
        public DateTimeOffset ExpiresAtUtc { get; }
        public string? ClientConnectToken { get; set; }
        public string? ClientResumeToken { get; set; }
        public string? ClientName { get; set; }
        public SessionLifecycleStatus Status { get; set; } = SessionLifecycleStatus.PendingHost;
        public int ClaimAttempts { get; set; }
        public bool BridgeStarted { get; set; }
        public WebSocket? HostSocket { get; set; }
        public WebSocket? ClientSocket { get; set; }
        public DateTimeOffset? HostLastSeenUtc { get; set; }
        public DateTimeOffset? ClientLastSeenUtc { get; set; }
        public DateTimeOffset? HostReconnectDeadlineUtc { get; set; }
        public DateTimeOffset? ClientReconnectDeadlineUtc { get; set; }
        public object SyncRoot { get; } = new();
    }
}
