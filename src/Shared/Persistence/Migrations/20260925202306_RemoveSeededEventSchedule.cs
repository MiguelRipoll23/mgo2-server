using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeededEventSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The event the previous migration seeded is withdrawn. It stood in
            // for the row an operator schedules an event with, and the
            // reference's own schema notes that its event baseline "does not
            // insert example events or modify runtime event rows": a schedule is
            // runtime data, so a migration is the wrong place to leave one. The
            // statement names the exact row and the exact mode the seed wrote, so
            // an event scheduled since under a different mode is not touched.
            migrationBuilder.Sql(
                """
                DELETE FROM event_schedules
                WHERE id = 1 AND lobby_subtype = 10 AND publish_start = 0
                  AND publish_end = 0 AND team_capacity = 8;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A schedule is data an operator owns, so putting the placeholder
            // back would invent an event rather than restore one. The Down is
            // deliberately empty: it is the honest statement that there is
            // nothing here to undo.
        }
    }
}
