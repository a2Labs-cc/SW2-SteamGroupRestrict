namespace SteamRestrict.Services;

public sealed class SteamUserInfo
{
    public DateTime SteamAccountAge { get; set; }
    public int SteamLevel { get; set; }
    public int CS2Level { get; set; }
    public int CS2Playtime { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsGameDetailsPrivate { get; set; }
    public bool HasPrime { get; set; }
    public bool IsTradeBanned { get; set; }
    public bool IsVACBanned { get; set; }
    public bool IsGameBanned { get; set; }
    public bool IsInSteamGroup { get; set; }
}
