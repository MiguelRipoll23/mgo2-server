using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropUnreadCharacterColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_characters_lobbies_lobby_id",
                table: "characters");

            migrationBuilder.DropIndex(
                name: "IX_characters_lobby_id",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "host_score",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "host_votes",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "lobby_id",
                table: "characters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "host_score",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "host_votes",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "lobby_id",
                table: "characters",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_lobby_id",
                table: "characters",
                column: "lobby_id");

            migrationBuilder.AddForeignKey(
                name: "FK_characters_lobbies_lobby_id",
                table: "characters",
                column: "lobby_id",
                principalTable: "lobbies",
                principalColumn: "id");
        }
    }
}
