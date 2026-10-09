using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSurvivalHostsGameType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "lobby_game_types",
                keyColumn: "id",
                keyValue: 5,
                column: "name",
                value: "Survival Hosts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "lobby_game_types",
                keyColumn: "id",
                keyValue: 5,
                column: "name",
                value: "Unknown");
        }
    }
}
