using System.Net.Http.Json;
using System.Text.Json;

namespace AdbWireGuardGui;

internal sealed class BrokerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BrokerHealthResponse> GetHealthAsync(string serverUrl, CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        var response = await client.GetAsync("healthz", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BrokerHealthResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer pośredni zwrócił pusty health check.");
    }

    public async Task<BrokerCreateSessionResponse> CreateSessionAsync(
        string serverUrl,
        string hostToken,
        string deviceName,
        int requestedTtlMinutes,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hostToken);

        var response = await client.PostAsJsonAsync(
            "api/v1/relay/sessions",
            new BrokerCreateSessionRequest(deviceName, requestedTtlMinutes),
            JsonOptions,
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BrokerCreateSessionResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer pośredni zwrócił pusty wynik tworzenia sesji.");
    }

    public async Task<BrokerClaimSessionResponse> ClaimSessionAsync(
        string serverUrl,
        string pairCode,
        string clientName,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        var response = await client.PostAsJsonAsync(
            "api/v1/relay/claim",
            new BrokerClaimSessionRequest(pairCode, clientName),
            JsonOptions,
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BrokerClaimSessionResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer pośredni zwrócił pusty wynik dołączenia do sesji.");
    }

    public async Task<BrokerSessionStatusResponse> GetSessionStatusAsync(
        string serverUrl,
        Guid sessionId,
        string hostToken,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hostToken);

        var response = await client.GetAsync($"api/v1/relay/sessions/{sessionId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BrokerSessionStatusResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer pośredni zwrócił pusty status sesji.");
    }

    public async Task<BrokerSessionStatusResponse> RecordHeartbeatAsync(
        string serverUrl,
        Guid sessionId,
        string role,
        string resumeToken,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        var response = await client.PostAsJsonAsync(
            $"api/v1/relay/sessions/{sessionId}/heartbeat",
            new BrokerHeartbeatRequest(role, resumeToken),
            JsonOptions,
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BrokerSessionStatusResponse>(JsonOptions, cancellationToken))
            ?? throw new InvalidOperationException("Serwer pośredni zwrócił pusty wynik heartbeat.");
    }

    public async Task CloseSessionAsync(
        string serverUrl,
        Guid sessionId,
        string hostToken,
        CancellationToken cancellationToken)
    {
        using var client = CreateClient(serverUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", hostToken);

        var response = await client.DeleteAsync($"api/v1/relay/sessions/{sessionId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    private static HttpClient CreateClient(string serverUrl)
    {
        var normalized = NormalizeServerUrl(serverUrl);
        return new HttpClient
        {
            BaseAddress = new Uri(normalized, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    internal static string NormalizeServerUrl(string serverUrl)
    {
        var value = serverUrl.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        return value.TrimEnd('/') + "/";
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<BrokerErrorResponse>(JsonOptions, cancellationToken);
            message = error?.Error;
        }
        catch
        {
            // Fallback below.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }

        throw new InvalidOperationException(message.Trim());
    }
}

internal sealed record BrokerHealthResponse(bool Ok, string Service, int HostTokensConfigured);

internal sealed record BrokerCreateSessionRequest(string DeviceName, int RequestedTtlMinutes);

internal sealed record BrokerCreateSessionResponse(
    Guid SessionId,
    string PairCode,
    string HostConnectToken,
    string HostResumeToken,
    DateTimeOffset ExpiresAtUtc,
    int TtlSeconds,
    int ReconnectGraceSeconds,
    int HeartbeatIntervalSeconds);

internal sealed record BrokerClaimSessionRequest(string PairCode, string ClientName);

internal sealed record BrokerClaimSessionResponse(
    Guid SessionId,
    string ClientConnectToken,
    string ClientResumeToken,
    DateTimeOffset ExpiresAtUtc,
    int ReconnectGraceSeconds,
    int HeartbeatIntervalSeconds);

internal sealed record BrokerHeartbeatRequest(string Role, string ResumeToken);

internal sealed record BrokerSessionStatusResponse(
    Guid SessionId,
    string DeviceName,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    bool HostConnected,
    bool ClientConnected,
    bool BridgeStarted,
    int ClaimAttempts,
    DateTimeOffset? HostLastSeenUtc,
    DateTimeOffset? ClientLastSeenUtc,
    DateTimeOffset? HostReconnectDeadlineUtc,
    DateTimeOffset? ClientReconnectDeadlineUtc);

internal sealed record BrokerErrorResponse(string Error);
