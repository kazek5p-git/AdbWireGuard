namespace AdbWireGuardRelay;

public sealed record BrokerOptions
{
    public const string PrimarySectionName = "Broker";
    public const string LegacySectionName = "Relay";

    public List<string> HostTokens { get; init; } = [];
    public int DefaultSessionTtlMinutes { get; init; } = 5;
    public int MaxSessionTtlMinutes { get; init; } = 15;
    public int CleanupIntervalSeconds { get; init; } = 30;
    public int MaxSessionsPerHost { get; init; } = 3;
    public int MaxPendingClaimAttempts { get; init; } = 10;
    public int ReconnectGraceSeconds { get; init; } = 90;
    public int HeartbeatIntervalSeconds { get; init; } = 15;
    public int HeartbeatStaleSeconds { get; init; } = 45;

    public static BrokerOptions FromConfiguration(IConfiguration configuration)
    {
        var options =
            configuration.GetSection(PrimarySectionName).Get<BrokerOptions>() ??
            configuration.GetSection(LegacySectionName).Get<BrokerOptions>() ??
            new BrokerOptions();

        var envTokens = configuration["ADBWG_BROKER_HOST_TOKENS"];
        if (string.IsNullOrWhiteSpace(envTokens))
        {
            envTokens = configuration["ADBWG_RELAY_HOST_TOKENS"];
        }
        if (!string.IsNullOrWhiteSpace(envTokens))
        {
            options = options with
            {
                HostTokens = envTokens
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            };
        }

        if (options.DefaultSessionTtlMinutes < 1)
        {
            options = options with { DefaultSessionTtlMinutes = 5 };
        }

        if (options.MaxSessionTtlMinutes < options.DefaultSessionTtlMinutes)
        {
            options = options with { MaxSessionTtlMinutes = Math.Max(options.DefaultSessionTtlMinutes, 15) };
        }

        if (options.CleanupIntervalSeconds < 5)
        {
            options = options with { CleanupIntervalSeconds = 30 };
        }

        if (options.MaxSessionsPerHost < 1)
        {
            options = options with { MaxSessionsPerHost = 3 };
        }

        if (options.MaxPendingClaimAttempts < 1)
        {
            options = options with { MaxPendingClaimAttempts = 10 };
        }

        if (options.ReconnectGraceSeconds < 5)
        {
            options = options with { ReconnectGraceSeconds = 90 };
        }

        if (options.HeartbeatIntervalSeconds < 5)
        {
            options = options with { HeartbeatIntervalSeconds = 15 };
        }

        if (options.HeartbeatStaleSeconds < options.HeartbeatIntervalSeconds)
        {
            options = options with { HeartbeatStaleSeconds = Math.Max(options.HeartbeatIntervalSeconds * 3, 45) };
        }

        return options;
    }
}
