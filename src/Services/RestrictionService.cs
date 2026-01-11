using SteamRestrict.Config;
using SwiftlyS2.Shared.Players;

namespace SteamRestrict.Services;

public enum ViolationType
{
    CS2Level,
    Hours,
    SteamLevel,
    AccountAge,
    PrivateProfile,
    TradeBanned,
    GameBanned,
    SteamGroup,
    VACBanned
}

public sealed class RestrictionService
{
    private readonly SteamRestrictConfig _config;

    public RestrictionService(SteamRestrictConfig config)
    {
        _config = config;
    }

    public ViolationType? IsRestrictionViolated(SteamUserInfo userInfo)
    {
        if (_config.BlockPrivateProfile && (userInfo.IsPrivate || userInfo.IsGameDetailsPrivate))
            return ViolationType.PrivateProfile;

        if (_config.MinimumCS2Level != -1 && userInfo.CS2Level < _config.MinimumCS2Level)
            return ViolationType.CS2Level;

        if (!userInfo.IsGameDetailsPrivate && _config.MinimumHour != -1 && userInfo.CS2Playtime < _config.MinimumHour)
            return ViolationType.Hours;

        if (_config.MinimumSteamLevel != -1 && userInfo.SteamLevel < _config.MinimumSteamLevel)
            return ViolationType.SteamLevel;

        if (_config.MinimumSteamAccountAgeInDays != -1 && (DateTime.UtcNow - userInfo.SteamAccountAge).TotalDays < _config.MinimumSteamAccountAgeInDays)
            return ViolationType.AccountAge;

        if (_config.BlockTradeBanned && userInfo.IsTradeBanned)
            return ViolationType.TradeBanned;

        if (_config.BlockGameBanned && userInfo.IsGameBanned)
            return ViolationType.GameBanned;

        if (!string.IsNullOrEmpty(_config.SteamGroupID) && !userInfo.IsInSteamGroup)
            return ViolationType.SteamGroup;

        if (_config.BlockVACBanned && userInfo.IsVACBanned)
            return ViolationType.VACBanned;

        return null;
    }

    public bool ShouldWarnPrivateProfile(SteamUserInfo userInfo)
    {
        if (!_config.BlockPrivateProfile)
        {
            return false;
        }

        if (_config.PrivateProfileWarningTime <= 0)
        {
            return false;
        }

        return userInfo.IsPrivate || userInfo.IsGameDetailsPrivate;
    }
}
