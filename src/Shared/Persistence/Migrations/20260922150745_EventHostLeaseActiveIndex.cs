using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventHostLeaseActiveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_host_leases_game_id",
                table: "event_host_leases");

            migrationBuilder.CreateIndex(
                name: "IX_event_host_leases_game_id",
                table: "event_host_leases",
                column: "game_id",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_host_leases_game_id",
                table: "event_host_leases");

            migrationBuilder.CreateIndex(
                name: "IX_event_host_leases_game_id",
                table: "event_host_leases",
                column: "game_id",
                unique: true);
        }
    }
}
