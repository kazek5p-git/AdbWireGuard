namespace AdbWireGuardRelay;

public sealed class SessionCleanupService : BackgroundService
{
    private readonly SessionBroker _sessionBroker;
    private readonly BrokerOptions _options;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(SessionBroker sessionBroker, BrokerOptions options, ILogger<SessionCleanupService> logger)
    {
        _sessionBroker = sessionBroker;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = _sessionBroker.CleanupExpiredSessions();
                if (removed.Count > 0)
                {
                    _logger.LogInformation("Cleanup removed {Count} expired broker sessions", removed.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broker cleanup failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.CleanupIntervalSeconds), stoppingToken);
        }
    }
}
