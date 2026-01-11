namespace SteamRestrict.Config;

public sealed class SteamRestrictConfig
{
    public bool LogProfileInformations { get; set; } = true;

    public string ChatPrefix { get; set; } = "[SteamRestrict]";

    public string ChatPrefixColor { get; set; } = "[red]";

    public string SteamWebAPI { get; set; } = "Get your API key from https://steamcommunity.com/dev/apikey";

    public int MinimumCS2Level { get; set; } = -1;

    public int MinimumHour { get; set; } = -1;

    public int MinimumSteamLevel { get; set; } = -1;

    public int MinimumSteamAccountAgeInDays { get; set; } = -1;

    public bool BlockPrivateProfile { get; set; } = false;

    public bool BlockTradeBanned { get; set; } = false;

    public bool BlockVACBanned { get; set; } = false;

    public string SteamGroupID { get; set; } = "";

    public bool BlockGameBanned { get; set; } = false;

    public int PrivateProfileWarningTime { get; set; } = 20;

    public int PrivateProfileWarningPrintSeconds { get; set; } = 3;
}
