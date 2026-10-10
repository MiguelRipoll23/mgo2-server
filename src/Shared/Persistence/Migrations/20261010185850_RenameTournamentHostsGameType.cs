using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTournamentHostsGameType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "lobby_game_types",
                keyColumn: "id",
                keyValue: 6,
                column: "name",
                value: "Tournament Hosts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "lobby_game_types",
                keyColumn: "id",
                keyValue: 6,
                column: "name",
                value: "Unknown");
        }
    }
}
