using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CharacterLoginTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "last_login_time",
                table: "characters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "previous_login_time",
                table: "characters",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_login_time",
                table: "characters");

            migrationBuilder.DropColumn(
                name: "previous_login_time",
                table: "characters");
        }
    }
}
