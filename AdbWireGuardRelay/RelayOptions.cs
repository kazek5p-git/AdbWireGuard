namespace AdbWireGuardRelay;

public sealed record RelayOptions
{
    public const string SectionName = "Relay";

    public List<string> HostTokens { get; init; } = [];
    public int DefaultSessionTtlMinutes { get; init; } = 5;
    public int MaxSessionTtlMinutes { get; init; } = 15;
    public int CleanupIntervalSeconds { get; init; } = 30;
    public int MaxSessionsPerHost { get; init; } = 3;
    public int MaxPendingClaimAttempts { get; init; } = 10;

    public static RelayOptions FromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<RelayOptions>() ?? new RelayOptions();
        var envTokens = configuration["ADBWG_RELAY_HOST_TOKENS"];
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

        return options;
    }
}
