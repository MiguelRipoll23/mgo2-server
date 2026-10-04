using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRoomSettingsBlobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "common",
                table: "games");

            migrationBuilder.DropColumn(
                name: "rules",
                table: "games");

            // A data statement, and no DDL beside it: a gameplay server has no
            // client on the other end to measure a latency from, so its room
            // reports the value the captured dedicated host reported rather than
            // the zero an unreported field carries. The rooms it opened before
            // this migration still carry that zero and nothing rewrites them
            // afterwards — only a room being created is stamped — so they are
            // backfilled here. A room a player hosts is left alone: its latency
            // is measured by that player's client and reported by it.
            migrationBuilder.Sql(
                """
                UPDATE games
                SET ping = 100
                WHERE name LIKE 'server%'
                  AND ping <> 100;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "common",
                table: "games",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "rules",
                table: "games",
                type: "text",
                nullable: false,
                defaultValue: "{}");
        }
    }
}
