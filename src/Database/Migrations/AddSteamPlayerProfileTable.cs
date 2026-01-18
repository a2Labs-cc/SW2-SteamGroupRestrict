using FluentMigrator;

namespace SteamRestrict.Database.Migrations;

[Migration(1737201600)]
public class AddSteamPlayerProfileTable : Migration
{
    public override void Up()
    {
        Create.Table("steamrestrict_players")
            .WithColumn("steam_id").AsInt64().NotNullable().PrimaryKey()
            .WithColumn("last_checked_at").AsDateTime().NotNullable()
            .WithColumn("account_created_at").AsDateTime().NotNullable()
            .WithColumn("steam_level").AsInt32().NotNullable()
            .WithColumn("cs2_level").AsInt32().NotNullable()
            .WithColumn("cs2_playtime_hours").AsInt32().NotNullable()
            .WithColumn("is_profile_private").AsBoolean().NotNullable()
            .WithColumn("is_game_details_private").AsBoolean().NotNullable()
            .WithColumn("is_trade_banned").AsBoolean().NotNullable()
            .WithColumn("is_vac_banned").AsBoolean().NotNullable()
            .WithColumn("is_game_banned").AsBoolean().NotNullable()
            .WithColumn("is_in_steam_group").AsBoolean().NotNullable()
            .WithColumn("validation_passed").AsBoolean().NotNullable()
            .WithColumn("violation_type").AsString(50).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Index("idx_steamrestrict_players_last_checked")
            .OnTable("steamrestrict_players")
            .OnColumn("last_checked_at");
    }

    public override void Down()
    {
        Delete.Table("steamrestrict_players");
    }
}
