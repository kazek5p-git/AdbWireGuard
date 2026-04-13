namespace AdbWireGuardRelay;

public sealed class RelayCleanupService : BackgroundService
{
    private readonly RelaySessionStore _sessionStore;
    private readonly RelayOptions _options;
    private readonly ILogger<RelayCleanupService> _logger;

    public RelayCleanupService(RelaySessionStore sessionStore, RelayOptions options, ILogger<RelayCleanupService> logger)
    {
        _sessionStore = sessionStore;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = _sessionStore.CleanupExpiredSessions();
                if (removed.Count > 0)
                {
                    _logger.LogInformation("Cleanup removed {Count} expired relay sessions", removed.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Relay cleanup failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.CleanupIntervalSeconds), stoppingToken);
        }
    }
}
