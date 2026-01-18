using System.Data;
using Dommel;
using Microsoft.Extensions.Logging;
using SteamRestrict.Database.Models;
using SteamRestrict.Services;

namespace SteamRestrict.Database;

public class PlayerProfileRepository
{
    private readonly IDbConnection _connection;
    private readonly ILogger? _logger;

    public PlayerProfileRepository(IDbConnection connection, ILogger? logger = null)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task<PlayerProfile?> GetBySteamIdAsync(long steamId)
    {
        try
        {
            return await _connection.GetAsync<PlayerProfile>(steamId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict failed to get player profile for SteamId={SteamId}", steamId);
            return null;
        }
    }

    public async Task<bool> UpsertAsync(PlayerProfile profile)
    {
        try
        {
            var existing = await GetBySteamIdAsync(profile.SteamId);
            
            if (existing != null)
            {
                profile.CreatedAt = existing.CreatedAt;
                profile.UpdatedAt = DateTime.UtcNow;
                await _connection.UpdateAsync(profile);
            }
            else
            {
                profile.CreatedAt = DateTime.UtcNow;
                profile.UpdatedAt = DateTime.UtcNow;
                await _connection.InsertAsync(profile);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict failed to upsert player profile for SteamId={SteamId}", profile.SteamId);
            return false;
        }
    }

    public async Task<int> DeleteOldEntriesAsync(TimeSpan maxAge)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow - maxAge;
            var oldProfiles = (await _connection.GetAllAsync<PlayerProfile>())
                .Where(p => p.LastCheckedAt < cutoffDate)
                .ToList();

            var deletedCount = 0;
            foreach (var profile in oldProfiles)
            {
                if (await _connection.DeleteAsync(profile))
                {
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                _logger?.LogDebug("SteamRestrict deleted {Count} old player profile entries", deletedCount);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict failed to delete old player profile entries");
            return 0;
        }
    }

    public async Task<List<PlayerProfile>> GetAllAsync()
    {
        try
        {
            var profiles = await _connection.GetAllAsync<PlayerProfile>();
            return profiles.ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict failed to get all player profiles");
            return new List<PlayerProfile>();
        }
    }

    public static PlayerProfile FromSteamUserInfo(long steamId, SteamUserInfo userInfo, ViolationType? violationType)
    {
        return new PlayerProfile
        {
            SteamId = steamId,
            LastCheckedAt = DateTime.UtcNow,
            AccountCreatedAt = userInfo.SteamAccountAge,
            SteamLevel = userInfo.SteamLevel,
            Cs2Level = userInfo.CS2Level,
            Cs2PlaytimeHours = userInfo.CS2Playtime,
            IsProfilePrivate = userInfo.IsPrivate,
            IsGameDetailsPrivate = userInfo.IsGameDetailsPrivate,
            IsTradeBanned = userInfo.IsTradeBanned,
            IsVacBanned = userInfo.IsVACBanned,
            IsGameBanned = userInfo.IsGameBanned,
            IsInSteamGroup = userInfo.IsInSteamGroup,
            ValidationPassed = !violationType.HasValue,
            ViolationType = violationType?.ToString()
        };
    }

    public static void ToSteamUserInfo(PlayerProfile profile, SteamUserInfo userInfo)
    {
        userInfo.SteamAccountAge = profile.AccountCreatedAt;
        userInfo.SteamLevel = profile.SteamLevel;
        userInfo.CS2Level = profile.Cs2Level;
        userInfo.CS2Playtime = profile.Cs2PlaytimeHours;
        userInfo.IsPrivate = profile.IsProfilePrivate;
        userInfo.IsGameDetailsPrivate = profile.IsGameDetailsPrivate;
        userInfo.IsTradeBanned = profile.IsTradeBanned;
        userInfo.IsVACBanned = profile.IsVacBanned;
        userInfo.IsGameBanned = profile.IsGameBanned;
        userInfo.IsInSteamGroup = profile.IsInSteamGroup;
    }
}
