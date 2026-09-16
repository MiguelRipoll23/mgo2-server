using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lobby_game_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lobby_game_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "news",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    important = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    time = table.Column<int>(type: "integer", nullable: false),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_news", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lobbies",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    subtype_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    players_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    beginner_only = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    expansion_only = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    no_headshots = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    replays_only = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lobbies", x => x.id);
                    table.ForeignKey(
                        name: "FK_lobbies_lobby_game_types_subtype_id",
                        column: x => x.subtype_id,
                        principalTable: "lobby_game_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "character_connections",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    public_ip = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    public_port = table.Column<int>(type: "integer", nullable: false),
                    private_ip = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    private_port = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_connections", x => x.character_id);
                });

            migrationBuilder.CreateTable(
                name: "character_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    wins = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rounds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stuns_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stuns_friendly = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    headshot_stuns_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lock_stuns_received = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_deaths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_headshots = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consecutive_tdm = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    self_spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_self_spotted = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suicides = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    salutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    radio = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chat = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cqc_given = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cqc_taken = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rolls = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    catapult = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    falls = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    trapped = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    melee = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    melee_rec = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    box_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    box_uses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bases_captured = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bases_destroyed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sop_destab = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gako_saved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gako_defended = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gako_first = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    res_defend = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    res_gako_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    res_first_grab = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bomb_disarms = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sdm_survivals = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    race_checkpoints = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    wins_snake = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    kills_snake = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_holdups = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_tags_spawned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_tags_taken = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    snake_injured = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tsne_grab1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tsne_grab2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    knife_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    knife_stuns = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    boosts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    scans = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    evg_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    wakeups = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    team_kills = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    withdrawals = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    points_assist = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    points_base = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    trained_soldiers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_training = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_instructor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_student = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_snake = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_dedi = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stats_dm = table.Column<string>(type: "text", nullable: true),
                    stats_tdm = table.Column<string>(type: "text", nullable: true),
                    stats_res = table.Column<string>(type: "text", nullable: true),
                    stats_cap = table.Column<string>(type: "text", nullable: true),
                    stats_base = table.Column<string>(type: "text", nullable: true),
                    stats_bomb = table.Column<string>(type: "text", nullable: true),
                    stats_sne = table.Column<string>(type: "text", nullable: true),
                    stats_tsne = table.Column<string>(type: "text", nullable: true),
                    stats_sdm = table.Column<string>(type: "text", nullable: true),
                    stats_scap = table.Column<string>(type: "text", nullable: true),
                    stats_race = table.Column<string>(type: "text", nullable: true),
                    last_updated = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_stats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "character_training_times",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    training_mode_seconds = table.Column<long>(type: "bigint", nullable: false),
                    instructor_seconds = table.Column<long>(type: "bigint", nullable: false),
                    student_seconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_training_times", x => x.character_id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    old_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    comment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    host_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    host_votes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gameplay_options = table.Column<string>(type: "text", nullable: true),
                    creation_time = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    active = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    lobby_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_lobbies_lobby_id",
                        column: x => x.lobby_id,
                        principalTable: "lobbies",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "characters_appearance",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    gender = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    face = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    voice = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pitch = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    head = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    head_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    upper = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    upper_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lower = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lower_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chest = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chest_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    waist = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    waist_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    hands = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    hands_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    feet = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    feet_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory1_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory2_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    face_paint = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_appearance", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_appearance_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "characters_chatmacros",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    idx = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    text = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_chatmacros", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_chatmacros_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "characters_equipped_skills",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    skill_1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_equipped_skills", x => x.character_id);
                    table.ForeignKey(
                        name: "FK_characters_equipped_skills_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "characters_friends",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_friends", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_friends_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_characters_friends_characters_target_id",
                        column: x => x.target_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "characters_hostsettings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    settings = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_hostsettings", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_hostsettings_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "characters_instructors",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    instructor_character_id = table.Column<int>(type: "integer", nullable: false),
                    instructor_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    graduated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    instructor_skill_awarded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_instructors", x => x.character_id);
                    table.ForeignKey(
                        name: "FK_characters_instructors_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_characters_instructors_characters_instructor_character_id",
                        column: x => x.instructor_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "characters_sets_gear",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    idx = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false, defaultValue: ""),
                    stages = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    face = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    head = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    head_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    upper = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    upper_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lower = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lower_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chest = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chest_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    waist = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    waist_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    hands = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    hands_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    feet = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    feet_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory1_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accessory2_color = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    face_paint = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_sets_gear", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_sets_gear_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "characters_sets_skills",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    idx = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false, defaultValue: ""),
                    modes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skill_4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level_4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_sets_skills", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_sets_skills_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "characters_titles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    unlocked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_titles", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_titles_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_master_mail",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sender_character_id = table.Column<int>(type: "integer", nullable: true),
                    sender_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: ""),
                    subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    body = table.Column<string>(type: "character varying(708)", maxLength: 708, nullable: false, defaultValue: ""),
                    sent_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_master_mail", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_master_mail_characters_sender_character_id",
                        column: x => x.sender_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    host_id = table.Column<int>(type: "integer", nullable: false),
                    lobby_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: ""),
                    comment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    max_players = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    current_game = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    games = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    stance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ping = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    common = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    rules = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.id);
                    table.ForeignKey(
                        name: "FK_games_characters_host_id",
                        column: x => x.host_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_games_lobbies_lobby_id",
                        column: x => x.lobby_id,
                        principalTable: "lobbies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "host_reviews",
                columns: table => new
                {
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    voter_character_id = table.Column<int>(type: "integer", nullable: false),
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    host_character_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_host_reviews", x => new { x.game_id, x.voter_character_id });
                    table.ForeignKey(
                        name: "FK_host_reviews_characters_host_character_id",
                        column: x => x.host_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_host_reviews_characters_voter_character_id",
                        column: x => x.voter_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "instructor_reviews",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    instructor_character_id = table.Column<int>(type: "integer", nullable: false),
                    student_character_id = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    recognised = table.Column<bool>(type: "boolean", nullable: false),
                    answer_byte = table.Column<short>(type: "smallint", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_instructor_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_instructor_reviews_characters_instructor_character_id",
                        column: x => x.instructor_character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_instructor_reviews_characters_student_character_id",
                        column: x => x.student_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "mail",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sender_character_id = table.Column<int>(type: "integer", nullable: true),
                    recipient_character_id = table.Column<int>(type: "integer", nullable: true),
                    sender_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: ""),
                    recipient_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: ""),
                    subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    body = table.Column<string>(type: "character varying(708)", maxLength: 708, nullable: false, defaultValue: ""),
                    recipient_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    recipient_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sender_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sender_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sent_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail", x => x.id);
                    table.ForeignKey(
                        name: "FK_mail_characters_recipient_character_id",
                        column: x => x.recipient_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_mail_characters_sender_character_id",
                        column: x => x.sender_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    display_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    banned_until = table.Column<int>(type: "integer", nullable: true),
                    ban_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    slots = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    current_character_id = table.Column<int>(type: "integer", nullable: true),
                    main_character_id = table.Column<int>(type: "integer", nullable: true),
                    main_exp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    alt_exp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_characters_current_character_id",
                        column: x => x.current_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_users_characters_main_character_id",
                        column: x => x.main_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "game_players",
                columns: table => new
                {
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    team = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ping = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_players", x => new { x.game_id, x.character_id });
                    table.ForeignKey(
                        name: "FK_game_players_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_players_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_rounds",
                columns: table => new
                {
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_rounds", x => new { x.game_id, x.character_id });
                    table.ForeignKey(
                        name: "FK_game_rounds_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_rounds_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "round_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    host_character_id = table.Column<int>(type: "integer", nullable: false),
                    target_character_id = table.Column<int>(type: "integer", nullable: false),
                    team_win = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    aborted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    lobby_subtype = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_round_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_round_reports_characters_host_character_id",
                        column: x => x.host_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_round_reports_characters_target_character_id",
                        column: x => x.target_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_round_reports_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "round_weapon_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    weapon_id = table.Column<short>(type: "smallint", nullable: false),
                    value_a = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    value_b = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    value_c = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_round_weapon_stats", x => x.id);
                    table.ForeignKey(
                        name: "FK_round_weapon_stats_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_round_weapon_stats_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clan_applications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clan_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_applications", x => x.id);
                    table.ForeignKey(
                        name: "FK_clan_applications_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clans",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    leader_id = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    notice = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false, defaultValue: ""),
                    notice_time = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    notice_writer_id = table.Column<int>(type: "integer", nullable: true),
                    emblem_editor_id = table.Column<int>(type: "integer", nullable: true),
                    emblem = table.Column<byte[]>(type: "bytea", nullable: true),
                    emblem_wip = table.Column<byte[]>(type: "bytea", nullable: true),
                    open = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clans_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clan_id = table.Column<int>(type: "integer", nullable: false),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clans_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_clans_members_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_clans_members_clans_clan_id",
                        column: x => x.clan_id,
                        principalTable: "clans",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "lobby_game_types",
                columns: new[] { "id", "game_id", "name" },
                values: new object[,]
                {
                    { 0, 0, "None" },
                    { 1, 1, "Free Battle" },
                    { 2, 2, "Automatching" },
                    { 3, 3, "Tournament" },
                    { 4, 4, "Survival" },
                    { 5, 5, "Unknown" },
                    { 6, 6, "Unknown" },
                    { 7, 7, "Basic Training" },
                    { 8, 8, "Combat Training" },
                    { 10, 10, "Tournament Registration" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_stats_character_id",
                table: "character_stats",
                column: "character_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_lobby_id",
                table: "characters",
                column: "lobby_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_name",
                table: "characters",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_user_id",
                table: "characters",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_appearance_character_id",
                table: "characters_appearance",
                column: "character_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_chatmacros_character_id",
                table: "characters_chatmacros",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_friends_character_id_target_id",
                table: "characters_friends",
                columns: new[] { "character_id", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_characters_friends_target_id",
                table: "characters_friends",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_hostsettings_character_id",
                table: "characters_hostsettings",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_instructors_instructor_character_id",
                table: "characters_instructors",
                column: "instructor_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_sets_gear_character_id",
                table: "characters_sets_gear",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_sets_skills_character_id",
                table: "characters_sets_skills",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_titles_character_id_rank",
                table: "characters_titles",
                columns: new[] { "character_id", "rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clan_applications_character_id",
                table: "clan_applications",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_clan_applications_clan_id_character_id",
                table: "clan_applications",
                columns: new[] { "clan_id", "character_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clans_emblem_editor_id",
                table: "clans",
                column: "emblem_editor_id");

            migrationBuilder.CreateIndex(
                name: "IX_clans_leader_id",
                table: "clans",
                column: "leader_id");

            migrationBuilder.CreateIndex(
                name: "IX_clans_name",
                table: "clans",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clans_notice_writer_id",
                table: "clans",
                column: "notice_writer_id");

            migrationBuilder.CreateIndex(
                name: "IX_clans_members_character_id",
                table: "clans_members",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_clans_members_clan_id_character_id",
                table: "clans_members",
                columns: new[] { "clan_id", "character_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_master_mail_sender_character_id",
                table: "game_master_mail",
                column: "sender_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_players_character_id",
                table: "game_players",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_rounds_character_id",
                table: "game_rounds",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_games_host_id",
                table: "games",
                column: "host_id");

            migrationBuilder.CreateIndex(
                name: "IX_games_lobby_id",
                table: "games",
                column: "lobby_id");

            migrationBuilder.CreateIndex(
                name: "IX_host_reviews_host_character_id_reviewed_at",
                table: "host_reviews",
                columns: new[] { "host_character_id", "reviewed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_host_reviews_voter_character_id",
                table: "host_reviews",
                column: "voter_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_instructor_reviews_instructor_character_id_reviewed_at",
                table: "instructor_reviews",
                columns: new[] { "instructor_character_id", "reviewed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_instructor_reviews_student_character_id",
                table: "instructor_reviews",
                column: "student_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_lobbies_port",
                table: "lobbies",
                column: "port",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lobbies_subtype_id",
                table: "lobbies",
                column: "subtype_id");

            migrationBuilder.CreateIndex(
                name: "IX_mail_recipient_character_id",
                table: "mail",
                column: "recipient_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_mail_sender_character_id",
                table: "mail",
                column: "sender_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_reports_game_id",
                table: "round_reports",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_reports_host_character_id",
                table: "round_reports",
                column: "host_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_reports_target_character_id",
                table: "round_reports",
                column: "target_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_weapon_stats_character_id",
                table: "round_weapon_stats",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_weapon_stats_game_id",
                table: "round_weapon_stats",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_user_id",
                table: "sessions",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_current_character_id",
                table: "users",
                column: "current_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_display_name",
                table: "users",
                column: "display_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_main_character_id",
                table: "users",
                column: "main_character_id");

            migrationBuilder.AddForeignKey(
                name: "FK_character_connections_characters_character_id",
                table: "character_connections",
                column: "character_id",
                principalTable: "characters",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_character_stats_characters_character_id",
                table: "character_stats",
                column: "character_id",
                principalTable: "characters",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_character_training_times_characters_character_id",
                table: "character_training_times",
                column: "character_id",
                principalTable: "characters",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_characters_users_user_id",
                table: "characters",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_clan_applications_clans_clan_id",
                table: "clan_applications",
                column: "clan_id",
                principalTable: "clans",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_clans_clans_members_emblem_editor_id",
                table: "clans",
                column: "emblem_editor_id",
                principalTable: "clans_members",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_clans_clans_members_leader_id",
                table: "clans",
                column: "leader_id",
                principalTable: "clans_members",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_clans_clans_members_notice_writer_id",
                table: "clans",
                column: "notice_writer_id",
                principalTable: "clans_members",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clans_members_characters_character_id",
                table: "clans_members");

            migrationBuilder.DropForeignKey(
                name: "FK_users_characters_current_character_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_characters_main_character_id",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_clans_members_clans_clan_id",
                table: "clans_members");

            migrationBuilder.DropTable(
                name: "character_connections");

            migrationBuilder.DropTable(
                name: "character_stats");

            migrationBuilder.DropTable(
                name: "character_training_times");

            migrationBuilder.DropTable(
                name: "characters_appearance");

            migrationBuilder.DropTable(
                name: "characters_chatmacros");

            migrationBuilder.DropTable(
                name: "characters_equipped_skills");

            migrationBuilder.DropTable(
                name: "characters_friends");

            migrationBuilder.DropTable(
                name: "characters_hostsettings");

            migrationBuilder.DropTable(
                name: "characters_instructors");

            migrationBuilder.DropTable(
                name: "characters_sets_gear");

            migrationBuilder.DropTable(
                name: "characters_sets_skills");

            migrationBuilder.DropTable(
                name: "characters_titles");

            migrationBuilder.DropTable(
                name: "clan_applications");

            migrationBuilder.DropTable(
                name: "game_master_mail");

            migrationBuilder.DropTable(
                name: "game_players");

            migrationBuilder.DropTable(
                name: "game_rounds");

            migrationBuilder.DropTable(
                name: "host_reviews");

            migrationBuilder.DropTable(
                name: "instructor_reviews");

            migrationBuilder.DropTable(
                name: "mail");

            migrationBuilder.DropTable(
                name: "news");

            migrationBuilder.DropTable(
                name: "round_reports");

            migrationBuilder.DropTable(
                name: "round_weapon_stats");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "lobbies");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "lobby_game_types");

            migrationBuilder.DropTable(
                name: "clans");

            migrationBuilder.DropTable(
                name: "clans_members");
        }
    }
}
