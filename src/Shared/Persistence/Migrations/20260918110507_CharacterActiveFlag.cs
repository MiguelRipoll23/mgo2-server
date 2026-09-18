using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CharacterActiveFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The one thing the scaffolding cannot express: PostgreSQL refuses to cast an
            // integer column to boolean implicitly, so the conversion needs a USING clause
            // and the provider has no way to write one. The default is dropped first because
            // it is the integer 1, which does not survive the type change.
            migrationBuilder.Sql(
                """
                ALTER TABLE characters ALTER COLUMN active DROP DEFAULT;
                ALTER TABLE characters ALTER COLUMN active TYPE boolean USING (active <> 0);
                ALTER TABLE characters ALTER COLUMN active SET DEFAULT true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The reverse needs the same clause the upward conversion did.
            migrationBuilder.Sql(
                """
                ALTER TABLE characters ALTER COLUMN active DROP DEFAULT;
                ALTER TABLE characters ALTER COLUMN active TYPE integer USING (active::int);
                ALTER TABLE characters ALTER COLUMN active SET DEFAULT 1;
                """);
        }
    }
}
