using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventMatchActiveTeamIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_event_matches_first_team_id",
                table: "event_matches",
                column: "first_team_id",
                unique: true,
                filter: "state in (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_event_matches_lobby_id",
                table: "event_matches",
                column: "lobby_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_matches_second_team_id",
                table: "event_matches",
                column: "second_team_id",
                unique: true,
                filter: "state in (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_matches_first_team_id",
                table: "event_matches");

            migrationBuilder.DropIndex(
                name: "IX_event_matches_lobby_id",
                table: "event_matches");

            migrationBuilder.DropIndex(
                name: "IX_event_matches_second_team_id",
                table: "event_matches");
        }
    }
}
