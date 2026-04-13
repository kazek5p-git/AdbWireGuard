namespace AdbWireGuardRelay;

public sealed record CreateSessionRequest(string? DeviceName, int? RequestedTtlMinutes);

public sealed record CreateSessionResponse(
    Guid SessionId,
    string PairCode,
    string HostConnectToken,
    DateTimeOffset ExpiresAtUtc,
    int TtlSeconds);

public sealed record ClaimSessionRequest(string PairCode, string? ClientName);

public sealed record ClaimSessionResponse(
    Guid SessionId,
    string ClientConnectToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record SessionStatusResponse(
    Guid SessionId,
    string DeviceName,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    bool HostConnected,
    bool ClientConnected,
    bool RelayStarted,
    int ClaimAttempts);

public sealed record ErrorResponse(string Error);

internal enum RelaySessionStatus
{
    PendingHost,
    WaitingForClient,
    Active,
    Closed,
    Expired
}
