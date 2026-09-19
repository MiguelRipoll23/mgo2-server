using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StatisticsFromRoundReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_stats");

            migrationBuilder.DropColumn(
                name: "emblem_wip",
                table: "clans");

            migrationBuilder.DropColumn(
                name: "instructor_skill_awarded_at",
                table: "characters_instructors");

            migrationBuilder.RenameColumn(
                name: "notice_time",
                table: "clans",
                newName: "notice_at");

            migrationBuilder.RenameColumn(
                name: "last_login_time",
                table: "characters",
                newName: "last_seen_at");

            migrationBuilder.RenameColumn(
                name: "creation_time",
                table: "characters",
                newName: "created_at");

            migrationBuilder.AddColumn<short>(
                name: "consecutive_kills",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "deaths",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "headshot_deaths",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "headshot_kills",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "headshot_stuns",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "headshot_stuns_received",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "kills",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "lock_deaths",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "lock_kills",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "lock_stuns",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "lock_stuns_received",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "rule",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "score",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "stuns",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "stuns_received",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "wins",
                table: "round_reports",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "emblem_at",
                table: "clans",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consecutive_kills",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "deaths",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "headshot_deaths",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "headshot_kills",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "headshot_stuns",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "headshot_stuns_received",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "kills",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "lock_deaths",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "lock_kills",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "lock_stuns",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "lock_stuns_received",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "rule",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "score",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "stuns",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "stuns_received",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "wins",
                table: "round_reports");

            migrationBuilder.DropColumn(
                name: "emblem_at",
                table: "clans");

            migrationBuilder.RenameColumn(
                name: "notice_at",
                table: "clans",
                newName: "notice_time");

            migrationBuilder.RenameColumn(
                name: "last_seen_at",
                table: "characters",
                newName: "last_login_time");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "characters",
                newName: "creation_time");

            migrationBuilder.AddColumn<byte[]>(
                name: "emblem_wip",
                table: "clans",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "instructor_skill_awarded_at",
                table: "characters_instructors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "character_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    stats_base = table.Column<string>(type: "text", nullable: true),
                    bases_captured = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bases_destroyed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bomb_disarms = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_bomb = table.Column<string>(type: "text", nullable: true),
                    boosts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    box_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    box_uses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_cap = table.Column<string>(type: "text", nullable: true),
                    catapult = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chat = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_headshots = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_tdm = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cqc_given = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cqc_taken = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_dm = table.Column<string>(type: "text", nullable: true),
                    deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_dedi = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    evg_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    falls = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_stuns_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_instructor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    knife_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    knife_stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lock_deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_stuns_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    melee = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    melee_rec = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    points_assist = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    points_base = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    race_checkpoints = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_race = table.Column<string>(type: "text", nullable: true),
                    radio = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    res_defend = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    res_first_grab = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    res_gako_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_res = table.Column<string>(type: "text", nullable: true),
                    gako_defended = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gako_first = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gako_saved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rolls = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rounds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    salutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    scans = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_scap = table.Column<string>(type: "text", nullable: true),
                    score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_sdm = table.Column<string>(type: "text", nullable: true),
                    sdm_survivals = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    self_spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_holdups = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_injured = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    kills_snake = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_self_spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_tags_spawned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_tags_taken = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_snake = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    wins_snake = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_sne = table.Column<string>(type: "text", nullable: true),
                    sop_destab = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_student = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stuns_friendly = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stuns_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suicides = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_tdm = table.Column<string>(type: "text", nullable: true),
                    team_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tsne_grab1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tsne_grab2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_tsne = table.Column<string>(type: "text", nullable: true),
                    time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    trained_soldiers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_training = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    trapped = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    wakeups = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    wins = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    withdrawals = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_stats", x => x.id);
                    table.ForeignKey(
                        name: "FK_character_stats_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_stats_character_id",
                table: "character_stats",
                column: "character_id",
                unique: true);
        }
    }
}
