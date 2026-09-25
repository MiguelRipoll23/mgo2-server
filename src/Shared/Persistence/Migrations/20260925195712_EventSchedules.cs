using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lobby_subtype = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    publish_start = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    publish_end = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    team_capacity = table.Column<int>(type: "integer", nullable: false, defaultValue: 8)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_schedules_lobby_subtype",
                table: "event_schedules",
                column: "lobby_subtype");

            // The one event the event screens have always advertised, so that a
            // deployment which has not scheduled anything still has the event
            // its clients are already being told about. A data statement and not
            // a schema one: the model owns the table, and this is the single row
            // that makes the table answer for the event the server already had.
            // An open-ended window is deliberate — the daily hours an operator
            // wants are a publish_start and publish_end on the row, and a
            // deployment that states neither has an event that never closes.
            migrationBuilder.InsertData(
                table: "event_schedules",
                columns: new[] { "id", "lobby_subtype", "enabled", "publish_start", "publish_end", "team_capacity" },
                values: new object[] { 1, 10, true, 0L, 0L, 8 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_schedules");
        }
    }
}
