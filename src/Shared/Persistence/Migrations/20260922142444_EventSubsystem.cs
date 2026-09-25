using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EventSubsystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_host_leases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    match_id = table.Column<int>(type: "integer", nullable: false),
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    active_state_id = table.Column<int>(type: "integer", nullable: false),
                    active_state_sequence = table.Column<int>(type: "integer", nullable: false),
                    lobby_id = table.Column<int>(type: "integer", nullable: false),
                    lobby_subtype = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    leased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_host_leases", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_host_leases_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "event_matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_team_id = table.Column<int>(type: "integer", nullable: false),
                    second_team_id = table.Column<int>(type: "integer", nullable: false),
                    match_type = table.Column<int>(type: "integer", nullable: false),
                    lobby_id = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    winner_team_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_round_rewards",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    match_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    team_id = table.Column<int>(type: "integer", nullable: false),
                    reward = table.Column<int>(type: "integer", nullable: false),
                    is_participation = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_round_rewards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_teams",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_character_id = table.Column<int>(type: "integer", nullable: false),
                    lobby_id = table.Column<int>(type: "integer", nullable: false),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    match_type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    comment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    flag_bits = table.Column<int>(type: "integer", nullable: false),
                    password = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    consecutive_wins = table.Column<int>(type: "integer", nullable: false),
                    paid_reward = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tournament_brackets",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_round = table.Column<int>(type: "integer", nullable: false),
                    champion_team_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_brackets", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "tournament_registrations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    team_id = table.Column<int>(type: "integer", nullable: true),
                    slot_index = table.Column<int>(type: "integer", nullable: false),
                    reserved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_registrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tournament_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    match_id = table.Column<int>(type: "integer", nullable: false),
                    round_index = table.Column<int>(type: "integer", nullable: false),
                    first_team_id = table.Column<int>(type: "integer", nullable: false),
                    second_team_id = table.Column<int>(type: "integer", nullable: false),
                    winner_team_id = table.Column<int>(type: "integer", nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_team_members",
                columns: table => new
                {
                    team_id = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    experience = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_team_members", x => new { x.team_id, x.slot });
                    table.ForeignKey(
                        name: "FK_event_team_members_event_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "event_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournament_seeds",
                columns: table => new
                {
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    seed_index = table.Column<int>(type: "integer", nullable: false),
                    team_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_seeds", x => new { x.event_id, x.seed_index });
                    table.ForeignKey(
                        name: "FK_tournament_seeds_tournament_brackets_event_id",
                        column: x => x.event_id,
                        principalTable: "tournament_brackets",
                        principalColumn: "event_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_host_leases_game_id",
                table: "event_host_leases",
                column: "game_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_host_leases_match_id",
                table: "event_host_leases",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_round_rewards_match_id_character_id",
                table: "event_round_rewards",
                columns: new[] { "match_id", "character_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_team_members_team_id_character_id",
                table: "event_team_members",
                columns: new[] { "team_id", "character_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_teams_lobby_id_match_type",
                table: "event_teams",
                columns: new[] { "lobby_id", "match_type" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_registrations_event_id_character_id",
                table: "tournament_registrations",
                columns: new[] { "event_id", "character_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_results_event_id_match_id",
                table: "tournament_results",
                columns: new[] { "event_id", "match_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_seeds_event_id_team_id",
                table: "tournament_seeds",
                columns: new[] { "event_id", "team_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_host_leases");

            migrationBuilder.DropTable(
                name: "event_matches");

            migrationBuilder.DropTable(
                name: "event_round_rewards");

            migrationBuilder.DropTable(
                name: "event_team_members");

            migrationBuilder.DropTable(
                name: "tournament_registrations");

            migrationBuilder.DropTable(
                name: "tournament_results");

            migrationBuilder.DropTable(
                name: "tournament_seeds");

            migrationBuilder.DropTable(
                name: "event_teams");

            migrationBuilder.DropTable(
                name: "tournament_brackets");
        }
    }
}
