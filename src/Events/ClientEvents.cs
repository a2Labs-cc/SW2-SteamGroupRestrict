using Microsoft.Extensions.Logging;
using SteamRestrict.Config;
using SteamRestrict.Database;
using SteamRestrict.Services;
using System.Linq;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace SteamRestrict.Events;

public sealed class ClientEvents
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly SteamRestrictConfig _config;
    private readonly SteamApiService _steamApi;
    private readonly RestrictionService _restriction;
    private readonly WarningTimerService _warnings;
    private readonly Dictionary<int, CancellationTokenSource> _validationTokens = new();
    private readonly PlayerProfileRepository? _repository;

    public ClientEvents(
        ISwiftlyCore core,
        ILogger logger,
        SteamRestrictConfig config,
        SteamApiService steamApi,
        RestrictionService restriction,
        WarningTimerService warnings)
    {
        _core = core;
        _logger = logger;
        _config = config;
        _steamApi = steamApi;
        _restriction = restriction;
        _warnings = warnings;

        try
        {
            var connection = _core.Database.GetConnection(_config.DatabaseConnectionString);
            _repository = new PlayerProfileRepository(connection, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SteamRestrict failed to initialize database repository, caching disabled");
        }
    }

    public void Register()
    {
        _core.Event.OnClientDisconnected += (@event) =>
        {
            _warnings.Cancel(@event.PlayerId);
            if (_validationTokens.TryGetValue(@event.PlayerId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _validationTokens.Remove(@event.PlayerId);
            }
        };

        _core.Event.OnClientPutInServer += (@event) =>
        {
            if (string.IsNullOrEmpty(_config.SteamWebAPI))
            {
                _logger.LogWarning("SteamRestrict disabled: SteamWebAPI is empty");
                return;
            }

            var player = _core.PlayerManager.GetPlayer(@event.PlayerId);
            if (player is null)
            {
                _logger.LogWarning("SteamRestrict OnClientPutInServer: player not found for PlayerId={PlayerId}", @event.PlayerId);
                return;
            }

            if (player.IsFakeClient)
            {
                return;
            }

            var steamId = player.SteamID;
            if (steamId == 0)
            {
                _logger.LogWarning("SteamRestrict OnClientPutInServer: SteamID was 0 for PlayerId={PlayerId}", @event.PlayerId);
                return;
            }

            var controller = player.Controller;

            if (_config.LogProfileInformations)
            {
                _logger.LogInformation("SteamRestrict validating join: Name={Name} SteamId={SteamId}", controller.PlayerName, steamId);
            }
            
            var cts = new CancellationTokenSource();
            _validationTokens[@event.PlayerId] = cts;
            
            _ = Task.Run(async () =>
            {
                try
                {
                    var userInfo = new SteamUserInfo();
                    var usedCache = false;

                    if (_repository != null)
                    {
                        var cachedProfile = await _repository.GetBySteamIdAsync((long)steamId);
                        if (cachedProfile != null)
                        {
                            PlayerProfileRepository.ToSteamUserInfo(cachedProfile, userInfo);
                            usedCache = true;

                            if (_config.LogProfileInformations)
                            {
                                var cacheAge = DateTime.UtcNow - cachedProfile.LastCheckedAt;
                                _logger.LogInformation("SteamRestrict using cached data for SteamId={SteamId}, age={Age:F1}h", steamId, cacheAge.TotalHours);
                            }
                        }
                    }

                    if (!usedCache)
                    {
                        await _steamApi.PopulateSteamUserInfoAsync(steamId.ToString(), userInfo, cts.Token);
                    }

                    _core.Scheduler.NextWorldUpdate(() =>
                    {
                        var p = _core.PlayerManager.GetPlayer(@event.PlayerId);
                        if (p is null)
                        {
                            return;
                        }

                        var currentSteamId = p.SteamID;
                        if (currentSteamId == 0)
                        {
                            _logger?.LogWarning("SteamRestrict NextWorldUpdate: SteamID is 0 for PlayerId={PlayerId}", @event.PlayerId);
                            return;
                        }

                        var cs2Level = 0;
                        try
                        {
                            cs2Level = p.Controller.InventoryServices?.PersonaDataPublicLevel ?? 0;
                        }
                        catch
                        {
                            cs2Level = 0;
                        }

                        userInfo.CS2Level = Math.Max(0, cs2Level);

                        var violationType = _restriction.IsRestrictionViolated(userInfo);
                        if (violationType.HasValue)
                        {
                            string? violationDetails = violationType.Value switch
                            {
                                ViolationType.CS2Level => $"CS2Level={userInfo.CS2Level} Required={_config.MinimumCS2Level}",
                                ViolationType.Hours => $"Hours={userInfo.CS2Playtime} Required={_config.MinimumHour}",
                                ViolationType.SteamLevel => $"SteamLevel={userInfo.SteamLevel} Required={_config.MinimumSteamLevel}",
                                ViolationType.AccountAge => $"AccountAgeDays={(int)(DateTime.UtcNow - userInfo.SteamAccountAge).TotalDays} Required={_config.MinimumSteamAccountAgeInDays}",
                                _ => null
                            };

                            if (violationDetails is null)
                            {
                                _logger.LogWarning("SteamRestrict restriction violated: SteamId={SteamId} ViolationType={ViolationType}", currentSteamId, violationType.Value);
                            }
                            else
                            {
                                _logger.LogWarning("SteamRestrict restriction violated: SteamId={SteamId} ViolationType={ViolationType} Details={Details}", currentSteamId, violationType.Value, violationDetails);
                            }
                            if (_restriction.ShouldWarnPrivateProfile(userInfo))
                            {
                                if (_config.LogProfileInformations)
                                {
                                    _logger.LogInformation("SteamRestrict private profile warning started: SteamId={SteamId} WarningTime={WarningTime} PrintSeconds={PrintSeconds} IsPrivate={IsPrivate} IsGameDetailsPrivate={IsGameDetailsPrivate}",
                                        currentSteamId, _config.PrivateProfileWarningTime, _config.PrivateProfileWarningPrintSeconds, userInfo.IsPrivate, userInfo.IsGameDetailsPrivate);
                                }
                                var playerLoc = _core.Translation.GetPlayerLocalizer(p);
                                var prefix = FormatChatPrefix(_config.ChatPrefix, _config.ChatPrefixColor);
                                p.SendChat($"{prefix} {playerLoc["steamrestrict.private_profile_warning_chat", _config.PrivateProfileWarningTime]}".Colored());
                                _warnings.StartPrivateProfileCountdown(p);
                            }
                            else
                            {
                                p.Kick("You have been kicked for not meeting the minimum requirements.", SwiftlyS2.Shared.ProtobufDefinitions.ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                            }
                            return;
                        }

                        if (_config.LogProfileInformations)
                        {
                            _logger.LogInformation(
                                "SteamRestrict validation passed: SteamId={SteamId} CS2Level={CS2Level}/{MinCS2Level} Hours={Hours}/{MinHours} SteamLevel={SteamLevel}/{MinSteamLevel} Private={Private}",
                                currentSteamId,
                                userInfo.CS2Level,
                                _config.MinimumCS2Level,
                                userInfo.CS2Playtime,
                                _config.MinimumHour,
                                userInfo.SteamLevel,
                                _config.MinimumSteamLevel,
                                userInfo.IsPrivate || userInfo.IsGameDetailsPrivate);
                        }

                        if (_repository != null && !usedCache)
                        {
                            var profile = PlayerProfileRepository.FromSteamUserInfo((long)currentSteamId, userInfo, violationType);
                            
                            if (_config.LogProfileInformations)
                            {
                                _logger.LogInformation("SteamRestrict preparing to save profile for SteamId={SteamId}", currentSteamId);
                            }

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _repository.UpsertAsync(profile);
                                    if (_config.LogProfileInformations)
                                    {
                                        _logger.LogInformation("SteamRestrict saved profile to cache for SteamId={SteamId}", currentSteamId);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "SteamRestrict failed to save profile to cache for SteamId={SteamId}", currentSteamId);
                                }
                            });
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    if (_config.LogProfileInformations)
                    {
                        _logger.LogInformation("SteamRestrict validation cancelled: PlayerId={PlayerId} SteamId={SteamId}", @event.PlayerId, steamId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SteamRestrict validation task crashed: PlayerId={PlayerId} SteamId={SteamId}", @event.PlayerId, steamId);
                }
                finally
                {
                    if (_validationTokens.ContainsKey(@event.PlayerId))
                    {
                        _validationTokens[@event.PlayerId].Dispose();
                        _validationTokens.Remove(@event.PlayerId);
                    }
                }
            }, cts.Token);
        };
    }

    private static string FormatChatPrefix(string prefix, string? color)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            return FormatWithDefaultColor(prefix);
        }

        var c = color.Trim();
        if (c.Length == 0)
        {
            return FormatWithDefaultColor(prefix);
        }

        if (c.StartsWith("[", StringComparison.Ordinal) && c.EndsWith("]", StringComparison.Ordinal))
        {
            return $"{c}{prefix}[/]".Colored();
        }

        if (!c.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && c.Length != 1)
        {
            var named = $"[{c}]";
            return $"{named}{prefix}[/]".Colored();
        }

        var reset = (char)1;

        if (c.Length == 1)
        {
            var legacy = $"{c[0]}{prefix}{reset}";
            return legacy.Contains('[', StringComparison.Ordinal) ? legacy.Colored() : legacy;
        }

        var raw = c.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? c[2..] : c;
        if (raw.Length == 2 && raw.All(Uri.IsHexDigit) && byte.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            var legacy = $"{(char)b}{prefix}{reset}";
            return legacy.Contains('[', StringComparison.Ordinal) ? legacy.Colored() : legacy;
        }

        return prefix.Contains('[', StringComparison.Ordinal) ? prefix.Colored() : prefix;
    }

    private static string FormatWithDefaultColor(string prefix)
    {
        if (prefix.Contains('[', StringComparison.Ordinal))
        {
            return prefix.Colored();
        }
        return $"[red]{prefix}[/]".Colored();
    }
}
