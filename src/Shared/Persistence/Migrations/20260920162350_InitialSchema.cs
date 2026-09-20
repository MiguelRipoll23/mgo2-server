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
                    time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false)
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
                    account_id = table.Column<int>(type: "integer", nullable: false),
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
                    begginers_only = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    expansion_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    display_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    banned_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ban_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    slots = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    current_character_id = table.Column<int>(type: "integer", nullable: true),
                    main_character_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "characters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    old_name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    comment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: ""),
                    experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_rewards = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    previous_login_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.id);
                    table.CheckConstraint("characters_experience_range", "experience BETWEEN 0 AND 65535");
                    table.ForeignKey(
                        name: "FK_characters_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
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
                    table.ForeignKey(
                        name: "FK_character_connections_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_gameplay_options",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    online_status_mode = table.Column<int>(type: "integer", nullable: false),
                    email_friends_only = table.Column<bool>(type: "boolean", nullable: false),
                    receive_notices = table.Column<bool>(type: "boolean", nullable: false),
                    receive_invites = table.Column<bool>(type: "boolean", nullable: false),
                    normal_view_vertical_invert = table.Column<bool>(type: "boolean", nullable: false),
                    normal_view_horizontal_invert = table.Column<bool>(type: "boolean", nullable: false),
                    normal_view_speed = table.Column<int>(type: "integer", nullable: false),
                    shoulder_view_vertical_invert = table.Column<bool>(type: "boolean", nullable: false),
                    shoulder_view_horizontal_invert = table.Column<bool>(type: "boolean", nullable: false),
                    shoulder_view_speed = table.Column<int>(type: "integer", nullable: false),
                    first_view_vertical_invert = table.Column<bool>(type: "boolean", nullable: false),
                    first_view_horizontal_invert = table.Column<bool>(type: "boolean", nullable: false),
                    first_view_speed = table.Column<int>(type: "integer", nullable: false),
                    first_view_player_direction = table.Column<bool>(type: "boolean", nullable: false),
                    view_change_speed = table.Column<int>(type: "integer", nullable: false),
                    first_view_memory = table.Column<bool>(type: "boolean", nullable: false),
                    radar_lock_north = table.Column<bool>(type: "boolean", nullable: false),
                    radar_floor_hide = table.Column<bool>(type: "boolean", nullable: false),
                    hud_display_size = table.Column<int>(type: "integer", nullable: false),
                    hud_hide_name_tags = table.Column<bool>(type: "boolean", nullable: false),
                    lock_on_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    weapon_switch_mode = table.Column<int>(type: "integer", nullable: false),
                    weapon_switch_a = table.Column<int>(type: "integer", nullable: false),
                    weapon_switch_b = table.Column<int>(type: "integer", nullable: false),
                    weapon_switch_c = table.Column<int>(type: "integer", nullable: false),
                    weapon_switch_now = table.Column<int>(type: "integer", nullable: false),
                    weapon_switch_before = table.Column<int>(type: "integer", nullable: false),
                    weapon_switch_toggle = table.Column<int>(type: "integer", nullable: false),
                    item_switch_mode = table.Column<int>(type: "integer", nullable: false),
                    voice_chat_output_device = table.Column<int>(type: "integer", nullable: false),
                    codec_output_device = table.Column<int>(type: "integer", nullable: false),
                    codec1_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codec1a = table.Column<int>(type: "integer", nullable: false),
                    codec1b = table.Column<int>(type: "integer", nullable: false),
                    codec1c = table.Column<int>(type: "integer", nullable: false),
                    codec1d = table.Column<int>(type: "integer", nullable: false),
                    codec2_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codec2a = table.Column<int>(type: "integer", nullable: false),
                    codec2b = table.Column<int>(type: "integer", nullable: false),
                    codec2c = table.Column<int>(type: "integer", nullable: false),
                    codec2d = table.Column<int>(type: "integer", nullable: false),
                    codec3_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codec3a = table.Column<int>(type: "integer", nullable: false),
                    codec3b = table.Column<int>(type: "integer", nullable: false),
                    codec3c = table.Column<int>(type: "integer", nullable: false),
                    codec3d = table.Column<int>(type: "integer", nullable: false),
                    codec4_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    codec4a = table.Column<int>(type: "integer", nullable: false),
                    codec4b = table.Column<int>(type: "integer", nullable: false),
                    codec4c = table.Column<int>(type: "integer", nullable: false),
                    codec4d = table.Column<int>(type: "integer", nullable: false),
                    voice_chat_recognition_level = table.Column<int>(type: "integer", nullable: false),
                    voice_chat_volume = table.Column<int>(type: "integer", nullable: false),
                    headset_volume = table.Column<int>(type: "integer", nullable: false),
                    bgm_volume = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_gameplay_options", x => x.character_id);
                    table.ForeignKey(
                        name: "FK_character_gameplay_options_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "character_presence",
                columns: table => new
                {
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    lobby_id = table.Column<int>(type: "integer", nullable: false),
                    last_seen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_presence", x => x.character_id);
                    table.ForeignKey(
                        name: "FK_character_presence_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_character_presence_lobbies_lobby_id",
                        column: x => x.lobby_id,
                        principalTable: "lobbies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_character_training_times_characters_character_id",
                        column: x => x.character_id,
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "characters_host_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    character_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    comment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    stance = table.Column<short>(type: "smallint", nullable: false),
                    max_players = table.Column<short>(type: "smallint", nullable: false),
                    briefing_time = table.Column<int>(type: "integer", nullable: false),
                    dedicated = table.Column<bool>(type: "boolean", nullable: false),
                    non_stat = table.Column<bool>(type: "boolean", nullable: false),
                    friendly_fire = table.Column<bool>(type: "boolean", nullable: false),
                    auto_aim = table.Column<bool>(type: "boolean", nullable: false),
                    uniques_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    enemy_nametags = table.Column<bool>(type: "boolean", nullable: false),
                    silent_mode = table.Column<bool>(type: "boolean", nullable: false),
                    auto_assign = table.Column<bool>(type: "boolean", nullable: false),
                    teams_switch = table.Column<bool>(type: "boolean", nullable: false),
                    ghosts = table.Column<bool>(type: "boolean", nullable: false),
                    voice_chat = table.Column<bool>(type: "boolean", nullable: false),
                    level_limit_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    level_limit_base = table.Column<int>(type: "integer", nullable: false),
                    level_limit_tolerance = table.Column<short>(type: "smallint", nullable: false),
                    team_kill_kick = table.Column<short>(type: "smallint", nullable: false),
                    idle_kick = table.Column<short>(type: "smallint", nullable: false),
                    settings_lobby_subtype = table.Column<short>(type: "smallint", nullable: false),
                    rotation_rules = table.Column<short[]>(type: "smallint[]", nullable: true),
                    rotation_maps = table.Column<short[]>(type: "smallint[]", nullable: true),
                    rotation_flags = table.Column<short[]>(type: "smallint[]", nullable: true),
                    weapon_restrictions = table.Column<byte[]>(type: "bytea", nullable: true),
                    rule_timers = table.Column<int[]>(type: "integer[]", nullable: true),
                    unique_red = table.Column<short>(type: "smallint", nullable: false),
                    unique_blue = table.Column<short>(type: "smallint", nullable: false),
                    common_a = table.Column<short>(type: "smallint", nullable: false),
                    common_b = table.Column<short>(type: "smallint", nullable: false),
                    capture_extra_time = table.Column<bool>(type: "boolean", nullable: false),
                    sneaking_snake_kills = table.Column<short>(type: "smallint", nullable: false),
                    unread_800 = table.Column<short>(type: "smallint", nullable: false),
                    unread_801 = table.Column<short>(type: "smallint", nullable: false),
                    unread_824 = table.Column<long>(type: "bigint", nullable: false),
                    unread_832 = table.Column<int>(type: "integer", nullable: false),
                    unread_836 = table.Column<long>(type: "bigint", nullable: false),
                    unread_844 = table.Column<int>(type: "integer", nullable: false),
                    unread_931 = table.Column<short>(type: "smallint", nullable: false),
                    unread_tail = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters_host_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_characters_host_settings_characters_character_id",
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
                    graduated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()"),
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
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    recipient_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sender_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sender_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    rule = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    team_win = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    experience = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    aborted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    lobby_subtype = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    wins = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    kills = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    deaths = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    score = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    stuns = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    stuns_received = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    headshot_kills = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    headshot_deaths = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    headshot_stuns = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    headshot_stuns_received = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    lock_kills = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    lock_deaths = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    lock_stuns = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    lock_stuns_received = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    consecutive_kills = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    kills = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    headshots = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    faints = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
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
                    notice_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notice_writer_id = table.Column<int>(type: "integer", nullable: true),
                    emblem_editor_id = table.Column<int>(type: "integer", nullable: true),
                    emblem = table.Column<byte[]>(type: "bytea", nullable: true),
                    emblem_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    open = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "now()")
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
                name: "IX_accounts_current_character_id",
                table: "accounts",
                column: "current_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_display_name",
                table: "accounts",
                column: "display_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_main_character_id",
                table: "accounts",
                column: "main_character_id");

            migrationBuilder.CreateIndex(
                name: "IX_character_presence_lobby_id",
                table: "character_presence",
                column: "lobby_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_account_id",
                table: "characters",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_characters_name",
                table: "characters",
                column: "name",
                unique: true);

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
                name: "IX_characters_host_settings_character_id",
                table: "characters_host_settings",
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
                name: "IX_sessions_account_id",
                table: "sessions",
                column: "account_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_characters_current_character_id",
                table: "accounts",
                column: "current_character_id",
                principalTable: "characters",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_characters_main_character_id",
                table: "accounts",
                column: "main_character_id",
                principalTable: "characters",
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
                name: "FK_accounts_characters_current_character_id",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_accounts_characters_main_character_id",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_clans_members_characters_character_id",
                table: "clans_members");

            migrationBuilder.DropForeignKey(
                name: "FK_clans_members_clans_clan_id",
                table: "clans_members");

            migrationBuilder.DropTable(
                name: "character_connections");

            migrationBuilder.DropTable(
                name: "character_gameplay_options");

            migrationBuilder.DropTable(
                name: "character_presence");

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
                name: "characters_host_settings");

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
                name: "lobbies");

            migrationBuilder.DropTable(
                name: "lobby_game_types");

            migrationBuilder.DropTable(
                name: "characters");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "clans");

            migrationBuilder.DropTable(
                name: "clans_members");
        }
    }
}
