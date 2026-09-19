using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountsTableAndCharacterLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_characters_users_user_id",
                table: "characters");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "sessions",
                newName: "account_id");

            migrationBuilder.RenameIndex(
                name: "IX_sessions_user_id",
                table: "sessions",
                newName: "IX_sessions_account_id");

            migrationBuilder.RenameColumn(
                name: "value_c",
                table: "round_weapon_stats",
                newName: "kills");

            migrationBuilder.RenameColumn(
                name: "value_b",
                table: "round_weapon_stats",
                newName: "headshots");

            migrationBuilder.RenameColumn(
                name: "value_a",
                table: "round_weapon_stats",
                newName: "faints");

            migrationBuilder.RenameColumn(
                name: "topic",
                table: "news",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "message",
                table: "news",
                newName: "body");

            migrationBuilder.RenameColumn(
                name: "recipient_read",
                table: "mail",
                newName: "is_read");

            migrationBuilder.RenameColumn(
                name: "expansion_only",
                table: "lobbies",
                newName: "expansion_required");

            migrationBuilder.RenameColumn(
                name: "beginner_only",
                table: "lobbies",
                newName: "begginers_only");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "characters",
                newName: "account_id");

            migrationBuilder.RenameIndex(
                name: "IX_characters_user_id",
                table: "characters",
                newName: "IX_characters_account_id");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reported_at",
                table: "round_weapon_stats",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "round_reports",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "time",
                table: "news",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "sent_at",
                table: "mail",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "games",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "sent_at",
                table: "game_master_mail",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "notice_time",
                table: "clans",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldDefaultValue: 0L);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "clans",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "applied_at",
                table: "clan_applications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "unlocked_at",
                table: "characters_titles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "instructor_skill_awarded_at",
                table: "characters_instructors",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "graduated_at",
                table: "characters_instructors",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<short>(
                name: "type",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "auto_aim",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "auto_assign",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "briefing_time",
                table: "characters_hostsettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "capture_extra_time",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "comment",
                table: "characters_hostsettings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "common_a",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "common_b",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "dedicated",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "enemy_nametags",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "friendly_fire",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ghosts",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "idle_kick",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<int>(
                name: "level_limit_base",
                table: "characters_hostsettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "level_limit_enabled",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "level_limit_tolerance",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "max_players",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "characters_hostsettings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "non_stat",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "password",
                table: "characters_hostsettings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<short[]>(
                name: "rotation_flags",
                table: "characters_hostsettings",
                type: "smallint[]",
                nullable: true);

            migrationBuilder.AddColumn<short[]>(
                name: "rotation_maps",
                table: "characters_hostsettings",
                type: "smallint[]",
                nullable: true);

            migrationBuilder.AddColumn<short[]>(
                name: "rotation_rules",
                table: "characters_hostsettings",
                type: "smallint[]",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "rule_timers",
                table: "characters_hostsettings",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "settings_lobby_subtype",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "silent_mode",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "sneaking_snake_kills",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "stance",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "team_kill_kick",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "teams_switch",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "unique_blue",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "unique_red",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "uniques_enabled",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "unread_800",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "unread_801",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<long>(
                name: "unread_824",
                table: "characters_hostsettings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "unread_832",
                table: "characters_hostsettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "unread_836",
                table: "characters_hostsettings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "unread_844",
                table: "characters_hostsettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<short>(
                name: "unread_931",
                table: "characters_hostsettings",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<byte[]>(
                name: "unread_tail",
                table: "characters_hostsettings",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "voice_chat",
                table: "characters_hostsettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "weapon_restrictions",
                table: "characters_hostsettings",
                type: "bytea",
                nullable: true);

            // DATA STATEMENT (no DDL): the stored settings were a JSON array of the
            // 0x4310 block's bytes, written by the codec that read the client's push as
            // `[byte, byte, ...]`. Each column takes the block bytes it is written from,
            // so a host keeps the settings it had pushed; a row without a valid array
            // keeps its defaults, which is what the old reader served it. The blob has no
            // shape for the rest byte 0x155, so it stores those bits verbatim in
            // unread_tail and the model answers them from there. The drop of the `settings`
            // column follows this statement, not the tool's earlier spot, so the data is
            // read while it still exists.
            migrationBuilder.Sql(
                """
                UPDATE characters_hostsettings AS hs
                SET
                    name = COALESCE(split_part(convert_from(substring(d.bytes from 1 for 16), 'ISO-8859-1'), chr(0), 1), ''),
                    comment = COALESCE(split_part(convert_from(substring(d.bytes from 17 for 128), 'ISO-8859-1'), chr(0), 1), ''),
                    password = CASE WHEN get_byte(d.bytes, 0x90) <> 0
                                    THEN NULLIF(split_part(convert_from(substring(d.bytes from 0x92 for 16), 'ISO-8859-1'), chr(0), 1), '')
                               END,
                    dedicated = get_byte(d.bytes, 0xa1) <> 0,
                    settings_lobby_subtype = get_byte(d.bytes, 0xa2),
                    rotation_rules = (SELECT array_agg(get_byte(d.bytes, 0xa3 + i * 3)::smallint ORDER BY i) FROM generate_series(0, 15) AS t(i)),
                    rotation_maps = (SELECT array_agg(get_byte(d.bytes, 0xa3 + i * 3 + 1)::smallint ORDER BY i) FROM generate_series(0, 15) AS t(i)),
                    rotation_flags = (SELECT array_agg(get_byte(d.bytes, 0xa3 + i * 3 + 2)::smallint ORDER BY i) FROM generate_series(0, 15) AS t(i)),
                    unread_800 = get_byte(d.bytes, 0xd3),
                    unread_801 = get_byte(d.bytes, 0xd4),
                    weapon_restrictions = substring(d.bytes from 0xd6 for 16),
                    max_players = get_byte(d.bytes, 0xe5),
                    briefing_time = (get_byte(d.bytes, 0xe6) << 24) | (get_byte(d.bytes, 0xe7) << 16) | (get_byte(d.bytes, 0xe8) << 8) | get_byte(d.bytes, 0xe9),
                    unread_824 = (get_byte(d.bytes, 0xea) << 24) | (get_byte(d.bytes, 0xeb) << 16) | (get_byte(d.bytes, 0xec) << 8) | get_byte(d.bytes, 0xed),
                    unread_832 = (get_byte(d.bytes, 0xee) << 8) | get_byte(d.bytes, 0xef),
                    unread_836 = (get_byte(d.bytes, 0xf0) << 24) | (get_byte(d.bytes, 0xf1) << 16) | (get_byte(d.bytes, 0xf2) << 8) | get_byte(d.bytes, 0xf3),
                    unread_844 = (get_byte(d.bytes, 0xf4) << 8) | get_byte(d.bytes, 0xf5),
                    stance = get_byte(d.bytes, 0xf6),
                    level_limit_tolerance = get_byte(d.bytes, 0xf7),
                    level_limit_base = (get_byte(d.bytes, 0xf8) << 24) | (get_byte(d.bytes, 0xf9) << 16) | (get_byte(d.bytes, 0xfa) << 8) | get_byte(d.bytes, 0xfb),
                    rule_timers = (SELECT array_agg((get_byte(d.bytes, 0xfc + i * 4) << 24) | (get_byte(d.bytes, 0xfd + i * 4) << 16) | (get_byte(d.bytes, 0xfe + i * 4) << 8) | get_byte(d.bytes, 0xff + i * 4) ORDER BY i) FROM generate_series(0, 16) AS t(i)),
                    unique_red = get_byte(d.bytes, 0x140),
                    unique_blue = get_byte(d.bytes, 0x141),
                    common_a = get_byte(d.bytes, 0x142),
                    common_b = get_byte(d.bytes, 0x143),
                    unread_931 = get_byte(d.bytes, 0x144),
                    idle_kick = (get_byte(d.bytes, 0x145) << 8) | get_byte(d.bytes, 0x146),
                    team_kill_kick = (get_byte(d.bytes, 0x147) << 8) | get_byte(d.bytes, 0x148),
                    capture_extra_time = get_byte(d.bytes, 0x149) <> 0,
                    sneaking_snake_kills = get_byte(d.bytes, 0x14a),
                    unread_tail = substring(d.bytes from 0x14c for 14),
                    non_stat = (get_byte(d.bytes, 0x155) & 2) <> 0
                FROM (
                    SELECT padded.id,
                           padded.raw || repeat('\x00'::bytea, greatest(0, 0x159 - length(padded.raw))) AS bytes
                    FROM (
                        SELECT decoded.id,
                               decode(string_agg(lpad(to_hex((value::text)::int), 2, '0'), '' ORDER BY ord), 'hex') AS raw
                        FROM characters_hostsettings AS decoded
                        CROSS JOIN LATERAL jsonb_array_elements(
                            CASE WHEN decoded.settings IS NOT NULL AND left(btrim(decoded.settings), 1) = '['
                                 THEN decoded.settings::jsonb
                            END) WITH ORDINALITY AS element(value, ord)
                        WHERE decoded.settings IS NOT NULL AND left(btrim(decoded.settings), 1) = '['
                        GROUP BY decoded.id
                    ) AS padded
                ) AS d
                WHERE d.id = hs.id;
                """);

            migrationBuilder.DropColumn(
                name: "settings",
                table: "characters_hostsettings");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "previous_login_time",
                table: "characters",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "last_login_time",
                table: "characters",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "creation_time",
                table: "characters",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "last_updated",
                table: "character_stats",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

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
                    table.ForeignKey(
                        name: "FK_accounts_characters_current_character_id",
                        column: x => x.current_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_accounts_characters_main_character_id",
                        column: x => x.main_character_id,
                        principalTable: "characters",
                        principalColumn: "id");
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

            migrationBuilder.AddForeignKey(
                name: "FK_characters_accounts_account_id",
                table: "characters",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_characters_accounts_account_id",
                table: "characters");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropColumn(
                name: "reported_at",
                table: "round_weapon_stats");

            migrationBuilder.DropColumn(
                name: "auto_aim",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "auto_assign",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "briefing_time",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "capture_extra_time",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "comment",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "common_a",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "common_b",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "dedicated",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "enemy_nametags",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "friendly_fire",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "ghosts",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "idle_kick",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "level_limit_base",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "level_limit_enabled",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "level_limit_tolerance",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "max_players",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "name",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "non_stat",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "password",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "rotation_flags",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "rotation_maps",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "rotation_rules",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "rule_timers",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "settings_lobby_subtype",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "silent_mode",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "sneaking_snake_kills",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "stance",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "team_kill_kick",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "teams_switch",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unique_blue",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unique_red",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "uniques_enabled",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_800",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_801",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_824",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_832",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_836",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_844",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_931",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "unread_tail",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "voice_chat",
                table: "characters_hostsettings");

            migrationBuilder.DropColumn(
                name: "weapon_restrictions",
                table: "characters_hostsettings");

            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "sessions",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "IX_sessions_account_id",
                table: "sessions",
                newName: "IX_sessions_user_id");

            migrationBuilder.RenameColumn(
                name: "kills",
                table: "round_weapon_stats",
                newName: "value_c");

            migrationBuilder.RenameColumn(
                name: "headshots",
                table: "round_weapon_stats",
                newName: "value_b");

            migrationBuilder.RenameColumn(
                name: "faints",
                table: "round_weapon_stats",
                newName: "value_a");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "news",
                newName: "topic");

            migrationBuilder.RenameColumn(
                name: "body",
                table: "news",
                newName: "message");

            migrationBuilder.RenameColumn(
                name: "is_read",
                table: "mail",
                newName: "recipient_read");

            migrationBuilder.RenameColumn(
                name: "expansion_required",
                table: "lobbies",
                newName: "expansion_only");

            migrationBuilder.RenameColumn(
                name: "begginers_only",
                table: "lobbies",
                newName: "beginner_only");

            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "characters",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "IX_characters_account_id",
                table: "characters",
                newName: "IX_characters_user_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "round_reports",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<int>(
                name: "time",
                table: "news",
                type: "integer",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "sent_at",
                table: "mail",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "games",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "sent_at",
                table: "game_master_mail",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<long>(
                name: "notice_time",
                table: "clans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                table: "clans",
                type: "timestamp without time zone",
                nullable: true,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "applied_at",
                table: "clan_applications",
                type: "timestamp without time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "unlocked_at",
                table: "characters_titles",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "instructor_skill_awarded_at",
                table: "characters_instructors",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "graduated_at",
                table: "characters_instructors",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "type",
                table: "characters_hostsettings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<string>(
                name: "settings",
                table: "characters_hostsettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "previous_login_time",
                table: "characters",
                type: "integer",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "last_login_time",
                table: "characters",
                type: "integer",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "creation_time",
                table: "characters",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "last_updated",
                table: "character_stats",
                type: "integer",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    current_character_id = table.Column<int>(type: "integer", nullable: true),
                    main_character_id = table.Column<int>(type: "integer", nullable: true),
                    ban_reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    banned_until = table.Column<int>(type: "integer", nullable: true),
                    display_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    slots = table.Column<int>(type: "integer", nullable: false, defaultValue: 3)
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
                name: "FK_characters_users_user_id",
                table: "characters",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
