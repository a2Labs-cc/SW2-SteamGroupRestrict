using Microsoft.Extensions.Logging;
using SteamRestrict.Config;
using SteamRestrict.Database;
using SwiftlyS2.Shared;

namespace SteamRestrict.Services;

public class CacheCleanupService
{
    private readonly ISwiftlyCore _core;
    private readonly SteamRestrictConfig _config;
    private readonly PlayerProfileRepository _repository;
    private readonly ILogger? _logger;
    private Timer? _cleanupTimer;

    public CacheCleanupService(
        ISwiftlyCore core,
        SteamRestrictConfig config,
        PlayerProfileRepository repository,
        ILogger? logger = null)
    {
        _core = core;
        _config = config;
        _repository = repository;
        _logger = logger;
    }

    public void StartPeriodicCleanup()
    {
        if (_config.CacheExpirationDays <= 0)
        {
            if (_config.LogProfileInformations)
            {
                _logger?.LogInformation("SteamRestrict cache cleanup disabled (CacheExpirationDays <= 0)");
            }
            return;
        }

        var cleanupInterval = TimeSpan.FromHours(24);
        
        _cleanupTimer = new Timer(
            async _ => await PerformCleanupAsync(),
            null,
            TimeSpan.FromMinutes(5),
            cleanupInterval
        );

        if (_config.LogProfileInformations)
        {
            _logger?.LogInformation("SteamRestrict cache cleanup scheduled every {Interval} hours, expiration: {Days} days", 
                cleanupInterval.TotalHours, _config.CacheExpirationDays);
        }
    }

    public void Stop()
    {
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;
    }

    private async Task PerformCleanupAsync()
    {
        try
        {
            var maxAge = TimeSpan.FromDays(_config.CacheExpirationDays);
            var deletedCount = await _repository.DeleteOldEntriesAsync(maxAge);

            if (deletedCount > 0)
            {
                if (_config.LogProfileInformations)
                {
                    _logger?.LogInformation("SteamRestrict cache cleanup completed: deleted {Count} entries older than {Days} days", 
                        deletedCount, _config.CacheExpirationDays);
                }
            }
            else
            {
                _logger?.LogDebug("SteamRestrict cache cleanup completed: no old entries to delete");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict cache cleanup failed");
        }
    }

    public async Task RunCleanupNowAsync()
    {
        await PerformCleanupAsync();
    }
}
