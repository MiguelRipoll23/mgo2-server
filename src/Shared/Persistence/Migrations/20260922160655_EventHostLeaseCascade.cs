using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventHostLeaseCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_host_leases_games_game_id",
                table: "event_host_leases");

            migrationBuilder.AddForeignKey(
                name: "FK_event_host_leases_games_game_id",
                table: "event_host_leases",
                column: "game_id",
                principalTable: "games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_host_leases_games_game_id",
                table: "event_host_leases");

            migrationBuilder.AddForeignKey(
                name: "FK_event_host_leases_games_game_id",
                table: "event_host_leases",
                column: "game_id",
                principalTable: "games",
                principalColumn: "id");
        }
    }
}
