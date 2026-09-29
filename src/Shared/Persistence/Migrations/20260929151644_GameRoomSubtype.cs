using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GameRoomSubtype : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lobby_subtype",
                table: "games",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lobby_subtype",
                table: "games");
        }
    }
}
