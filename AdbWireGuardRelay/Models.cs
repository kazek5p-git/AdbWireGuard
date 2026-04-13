namespace AdbWireGuardRelay;

public sealed record CreateSessionRequest(string? DeviceName, int? RequestedTtlMinutes);

public sealed record CreateSessionResponse(
    Guid SessionId,
    string PairCode,
    string HostConnectToken,
    string HostResumeToken,
    DateTimeOffset ExpiresAtUtc,
    int TtlSeconds,
    int ReconnectGraceSeconds,
    int HeartbeatIntervalSeconds);

public sealed record ClaimSessionRequest(string PairCode, string? ClientName);

public sealed record ClaimSessionResponse(
    Guid SessionId,
    string ClientConnectToken,
    string ClientResumeToken,
    DateTimeOffset ExpiresAtUtc,
    int ReconnectGraceSeconds,
    int HeartbeatIntervalSeconds);

public sealed record HeartbeatRequest(string Role, string ResumeToken);

public sealed record SessionStatusResponse(
    Guid SessionId,
    string DeviceName,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    bool HostConnected,
    bool ClientConnected,
    bool RelayStarted,
    int ClaimAttempts,
    DateTimeOffset? HostLastSeenUtc,
    DateTimeOffset? ClientLastSeenUtc,
    DateTimeOffset? HostReconnectDeadlineUtc,
    DateTimeOffset? ClientReconnectDeadlineUtc);

public sealed record ErrorResponse(string Error);

internal enum RelaySessionStatus
{
    PendingHost,
    WaitingForClient,
    Active,
    WaitingForHostReconnect,
    WaitingForClientReconnect,
    WaitingForReconnect,
    Closed,
    Expired
}
