using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dommel;

namespace SteamRestrict.Database.Models;

[Table("steamrestrict_players")]
public class PlayerProfile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("steam_id")]
    public long SteamId { get; set; }

    [Column("last_checked_at")]
    public DateTime LastCheckedAt { get; set; }

    [Column("account_created_at")]
    public DateTime AccountCreatedAt { get; set; }

    [Column("steam_level")]
    public int SteamLevel { get; set; }

    [Column("cs2_level")]
    public int Cs2Level { get; set; }

    [Column("cs2_playtime_hours")]
    public int Cs2PlaytimeHours { get; set; }

    [Column("is_profile_private")]
    public bool IsProfilePrivate { get; set; }

    [Column("is_game_details_private")]
    public bool IsGameDetailsPrivate { get; set; }

    [Column("is_trade_banned")]
    public bool IsTradeBanned { get; set; }

    [Column("is_vac_banned")]
    public bool IsVacBanned { get; set; }

    [Column("is_game_banned")]
    public bool IsGameBanned { get; set; }

    [Column("is_in_steam_group")]
    public bool IsInSteamGroup { get; set; }

    [Column("validation_passed")]
    public bool ValidationPassed { get; set; }

    [Column("violation_type")]
    public string? ViolationType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
