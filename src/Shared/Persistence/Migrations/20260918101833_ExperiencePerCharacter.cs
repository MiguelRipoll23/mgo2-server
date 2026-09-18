using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <summary>
    /// Experience belongs to the character rather than to the account.
    /// <para>
    /// The two pools were a leftover of the account-level model: nothing in the server wrote
    /// them, and the character header served the main pool for every character — so an account's
    /// alts shared one figure, and none of them was ever the total a round report records against
    /// the character. The character's column already exists, so this migration moves the data over
    /// and drops the pools; the packet that reads it is documented with the payload builder that
    /// writes it, not here.
    /// </para>
    /// <para>
    /// The move never lowers a total that was already stored, and the check constraint states the
    /// ceiling the reported total cannot exceed.
    /// </para>
    /// </summary>
    public partial class ExperiencePerCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill before dropping anything. The stored total becomes the larger of what the
            // character had recorded and the pool that was being served, so a round-earned total
            // cannot be wiped by a pool that was left at zero, and a level that was on screen
            // cannot go down. Each character reads the pool its identity maps to: the designated
            // main character main_exp, the others alt_exp. The header served main_exp to every
            // character, so an account whose alternate pool was set now shows that value on its
            // alts rather than the main character's.
            migrationBuilder.Sql(
                """
                UPDATE characters AS c
                SET experience = GREATEST(
                        c.experience,
                        LEAST(
                            65535,
                            GREATEST(
                                0,
                                COALESCE(
                                    CASE WHEN u.main_character_id = c.id THEN u.main_exp ELSE u.alt_exp END,
                                    0))))
                FROM users AS u
                WHERE u.id = c.user_id;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "characters_experience_range",
                table: "characters",
                sql: "experience BETWEEN 0 AND 65535");

            migrationBuilder.DropColumn(
                name: "alt_exp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "main_exp",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "characters_experience_range",
                table: "characters");

            migrationBuilder.AddColumn<int>(
                name: "alt_exp",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "main_exp",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // The pools cannot be rebuilt exactly: the model they come from let several characters
            // share one, so where they did, the largest total is the only figure a single column
            // can hold.
            migrationBuilder.Sql(
                """
                UPDATE users AS u
                SET main_exp = COALESCE(held.main_exp, 0),
                    alt_exp = COALESCE(held.alt_exp, 0)
                FROM (
                    SELECT c.user_id,
                           MAX(c.experience) FILTER (WHERE u.main_character_id = c.id) AS main_exp,
                           MAX(c.experience) FILTER (WHERE u.main_character_id IS DISTINCT FROM c.id) AS alt_exp
                    FROM characters AS c
                    JOIN users AS u ON u.id = c.user_id
                    GROUP BY c.user_id
                ) AS held
                WHERE held.user_id = u.id;
                """);
        }
    }
}
