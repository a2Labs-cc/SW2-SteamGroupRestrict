using SteamRestrict.Config;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace SteamRestrict.Services;

public sealed class WarningTimerService
{
    private readonly ISwiftlyCore _core;
    private readonly SteamRestrictConfig _config;
    private readonly ILogger? _logger;

    private readonly Dictionary<int, CancellationTokenSource> _timers = new();
    private readonly Dictionary<int, int> _remainingSeconds = new();

    public WarningTimerService(ISwiftlyCore core, SteamRestrictConfig config, ILogger? logger = null)
    {
        _core = core;
        _config = config;
        _logger = logger;
    }

    public void Cancel(int playerId)
    {
        if (_timers.TryGetValue(playerId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _timers.Remove(playerId);
        }

        _remainingSeconds.Remove(playerId);
    }

    public void CancelAll()
    {
        foreach (var cts in _timers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _timers.Clear();
        _remainingSeconds.Clear();
    }

    public void StartPrivateProfileCountdown(IPlayer player)
    {
        Cancel(player.PlayerID);

        if (_config.LogProfileInformations)
        {
            _logger?.LogInformation("SteamRestrict private profile countdown start: PlayerId={PlayerId} SteamId={SteamId} WarningTime={WarningTime} PrintSeconds={PrintSeconds}",
                player.PlayerID, player.SteamID, _config.PrivateProfileWarningTime, _config.PrivateProfileWarningPrintSeconds);
        }

        _remainingSeconds[player.PlayerID] = _config.PrivateProfileWarningTime;

        _core.Scheduler.NextTick(() =>
        {
            var p0 = _core.PlayerManager.GetPlayer(player.PlayerID);
            if (p0 is null)
            {
                return;
            }

            if (_remainingSeconds.TryGetValue(player.PlayerID, out var secs0))
            {
                p0.SendAlert($"Kicked in {secs0} seconds");
            }
        });

        var cts = _core.Scheduler.RepeatBySeconds(1.0f, () =>
        {
            var p = _core.PlayerManager.GetPlayer(player.PlayerID);
            if (p is null)
            {
                Cancel(player.PlayerID);
                return;
            }

            if (!_remainingSeconds.TryGetValue(player.PlayerID, out var secs))
            {
                Cancel(player.PlayerID);
                return;
            }

            secs--;
            _remainingSeconds[player.PlayerID] = secs;

            _core.Scheduler.NextTick(() =>
            {
                var pMsg = _core.PlayerManager.GetPlayer(player.PlayerID);
                if (pMsg is null)
                {
                    return;
                }
                pMsg.SendAlert($"Kicked in {Math.Max(0, secs)} seconds");
            });

            if (secs <= 0)
            {
                _core.Scheduler.NextTick(() =>
                {
                    var pKick = _core.PlayerManager.GetPlayer(player.PlayerID);
                    if (pKick is null)
                    {
                        return;
                    }
                    if (_config.LogProfileInformations)
                    {
                        _logger?.LogInformation("SteamRestrict private profile countdown: kicking PlayerId={PlayerId}", player.PlayerID);
                    }
                    pKick.Kick("You have been kicked for not meeting the minimum requirements.", ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                });
                Cancel(player.PlayerID);
            }
        });

        _timers[player.PlayerID] = cts;
        _core.Scheduler.StopOnMapChange(cts);
    }
}
