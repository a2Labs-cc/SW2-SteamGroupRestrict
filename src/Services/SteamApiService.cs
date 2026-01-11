using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SteamRestrict.Config;

namespace SteamRestrict.Services;

public sealed class SteamApiService
{
    private readonly HttpClient _httpClient;
    private readonly SteamRestrictConfig _config;
    private readonly ILogger? _logger;

    public SteamApiService(HttpClient httpClient, SteamRestrictConfig config, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task PopulateSteamUserInfoAsync(string steamId64, SteamUserInfo userInfo, CancellationToken cancellationToken = default)
    {
        userInfo.CS2Playtime = await FetchCs2PlaytimeMinutesAsync(steamId64, userInfo, cancellationToken) / 60;
        userInfo.SteamLevel = await FetchSteamLevelAsync(steamId64, cancellationToken);
        await FetchProfilePrivacyAndAgeAsync(steamId64, userInfo, cancellationToken);
        await FetchTradeAndVacBanStatusAsync(steamId64, userInfo, cancellationToken);
        await FetchSteamGroupMembershipAsync(steamId64, userInfo, cancellationToken);
    }

    private async Task<int> FetchCs2PlaytimeMinutesAsync(string steamId64, SteamUserInfo userInfo, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={_config.SteamWebAPI}&steamid={steamId64}&include_played_free_games=1&format=json";
            var json = await GetApiResponseAsync(url, cancellationToken);
            if (json is null)
            {
                _logger?.LogWarning("SteamRestrict could not fetch owned games for {SteamId}; treating game details as private/unknown", steamId64);
                userInfo.IsGameDetailsPrivate = true;
                return 0;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var response))
            {
                userInfo.IsGameDetailsPrivate = true;
                _logger?.LogWarning("SteamRestrict owned games response missing 'response' for {SteamId}; treating game details as private", steamId64);
                return 0;
            }

            if (!response.TryGetProperty("games", out var games) || games.ValueKind != JsonValueKind.Array)
            {
                userInfo.IsGameDetailsPrivate = true;
                _logger?.LogInformation("SteamRestrict owned games response missing 'games' for {SteamId}; likely private game details", steamId64);
                return 0;
            }

            if (games.GetArrayLength() == 0)
            {
                userInfo.IsGameDetailsPrivate = true;
                _logger?.LogInformation("SteamRestrict owned games list empty for {SteamId}; likely private game details", steamId64);
                return 0;
            }

            userInfo.IsGameDetailsPrivate = false;

            foreach (var game in games.EnumerateArray())
            {
                if (game.TryGetProperty("appid", out var appId) && appId.ValueKind == JsonValueKind.Number && appId.GetInt32() == 730)
                {
                    if (game.TryGetProperty("playtime_forever", out var playtime) && playtime.ValueKind == JsonValueKind.Number)
                    {
                        return playtime.GetInt32();
                    }

                    _logger?.LogInformation("SteamRestrict CS2 app found but playtime_forever missing/invalid for {SteamId}", steamId64);
                    return 0;
                }
            }

            _logger?.LogInformation("SteamRestrict CS2 appid 730 not found in owned games for {SteamId}; returning 0 minutes", steamId64);
            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict error fetching CS2 playtime for {SteamId}", steamId64);
            userInfo.IsGameDetailsPrivate = true;
            return 0;
        }
    }

    private async Task<int> FetchSteamLevelAsync(string steamId64, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={_config.SteamWebAPI}&steamid={steamId64}";
            var json = await GetApiResponseAsync(url, cancellationToken);
            if (json is null) return 0;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var response)) return 0;
            if (!response.TryGetProperty("player_level", out var level) || level.ValueKind != JsonValueKind.Number) return 0;
            return level.GetInt32();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict error fetching Steam level for {SteamId}", steamId64);
            return 0;
        }
    }

    private async Task FetchProfilePrivacyAndAgeAsync(string steamId64, SteamUserInfo userInfo, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={_config.SteamWebAPI}&steamids={steamId64}";
            var json = await GetApiResponseAsync(url, cancellationToken);
            if (json is null)
            {
                userInfo.IsPrivate = true;
                userInfo.IsGameDetailsPrivate = true;
                return;
            }

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var response))
            {
                userInfo.IsPrivate = true;
                userInfo.IsGameDetailsPrivate = true;
                return;
            }
            if (!response.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array)
            {
                userInfo.IsPrivate = true;
                userInfo.IsGameDetailsPrivate = true;
                return;
            }
            var enumerator = players.EnumerateArray();
            if (!enumerator.MoveNext())
            {
                userInfo.IsPrivate = true;
                userInfo.IsGameDetailsPrivate = true;
                return;
            }

            var player = enumerator.Current;

            if (player.TryGetProperty("communityvisibilitystate", out var visibility) && visibility.ValueKind == JsonValueKind.Number)
            {
                var visibilityValue = visibility.GetInt32();
                userInfo.IsPrivate = visibilityValue != 3;
                userInfo.IsGameDetailsPrivate = userInfo.IsPrivate;
            }
            else
            {
                _logger?.LogWarning("SteamRestrict visibility property not found or invalid for {SteamId}", steamId64);
                userInfo.IsPrivate = true;
                userInfo.IsGameDetailsPrivate = true;
            }

            if (player.TryGetProperty("timecreated", out var timeCreated) && timeCreated.ValueKind == JsonValueKind.Number)
            {
                var seconds = timeCreated.GetInt64();
                userInfo.SteamAccountAge = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            }
            else
            {
                userInfo.SteamAccountAge = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict error fetching profile privacy and age for {SteamId}", steamId64);
            userInfo.IsPrivate = true;
            userInfo.IsGameDetailsPrivate = true;
            userInfo.SteamAccountAge = DateTime.UtcNow;
        }
    }

    private async Task FetchTradeAndVacBanStatusAsync(string steamId64, SteamUserInfo userInfo, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key={_config.SteamWebAPI}&steamids={steamId64}";
            var json = await GetApiResponseAsync(url, cancellationToken);
            if (json is null) return;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array) return;
            var enumerator = players.EnumerateArray();
            if (!enumerator.MoveNext()) return;

            var player = enumerator.Current;
            if (player.TryGetProperty("EconomyBan", out var economyBan) && economyBan.ValueKind == JsonValueKind.String)
            {
                var economyBanValue = economyBan.GetString();
                userInfo.IsTradeBanned = !string.IsNullOrEmpty(economyBanValue) && !economyBanValue.Equals("none", StringComparison.OrdinalIgnoreCase);
            }

            if (player.TryGetProperty("VACBanned", out var vacBanned) && (vacBanned.ValueKind == JsonValueKind.True || vacBanned.ValueKind == JsonValueKind.False))
            {
                userInfo.IsVACBanned = vacBanned.GetBoolean();
            }

            if (player.TryGetProperty("NumberOfGameBans", out var numberOfGameBans) && numberOfGameBans.ValueKind == JsonValueKind.Number)
            {
                userInfo.IsGameBanned = numberOfGameBans.GetInt32() > 0;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict error fetching ban status for {SteamId}", steamId64);
        }
    }

    private async Task FetchSteamGroupMembershipAsync(string steamId64, SteamUserInfo userInfo, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.SteamGroupID))
            {
                userInfo.IsInSteamGroup = true;
                return;
            }

            var url = $"https://api.steampowered.com/ISteamUser/GetUserGroupList/v1/?key={_config.SteamWebAPI}&steamid={steamId64}";
            var json = await GetApiResponseAsync(url, cancellationToken);
            if (json is null)
            {
                userInfo.IsInSteamGroup = false;
                return;
            }

            userInfo.IsInSteamGroup = false;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var response)) return;
            if (!response.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array) return;

            foreach (var group in groups.EnumerateArray())
            {
                if (group.TryGetProperty("gid", out var gid) && gid.ValueKind == JsonValueKind.String)
                {
                    if (gid.GetString() == _config.SteamGroupID)
                    {
                        userInfo.IsInSteamGroup = true;
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SteamRestrict error fetching Steam group membership for {SteamId}", steamId64);
            userInfo.IsInSteamGroup = false;
        }
    }

    private async Task<string?> GetApiResponseAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("SteamRestrict Steam API returned non-success status {StatusCode} for {Url}", (int)response.StatusCode, url);
                return null;
            }
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "SteamRestrict Steam API request failed for {Url}", url);
            return null;
        }
    }
}
