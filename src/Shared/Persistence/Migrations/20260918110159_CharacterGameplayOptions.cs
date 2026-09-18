using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CharacterGameplayOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // The stored settings were a JSON object whose keys are the settings' own
            // names, one per column, written by the codec that read them. Each column takes
            // its key with the same fallback that codec applied, so a character whose blob
            // was missing a setting keeps the value it was being served. A blob that is not
            // an object is treated as no settings, which is what the old reader did with it.
            migrationBuilder.Sql(
                """
                INSERT INTO character_gameplay_options (
                    character_id,
                    online_status_mode,
                    email_friends_only,
                    receive_notices,
                    receive_invites,
                    normal_view_vertical_invert,
                    normal_view_horizontal_invert,
                    normal_view_speed,
                    shoulder_view_vertical_invert,
                    shoulder_view_horizontal_invert,
                    shoulder_view_speed,
                    first_view_vertical_invert,
                    first_view_horizontal_invert,
                    first_view_speed,
                    first_view_player_direction,
                    view_change_speed,
                    first_view_memory,
                    radar_lock_north,
                    radar_floor_hide,
                    hud_display_size,
                    hud_hide_name_tags,
                    lock_on_enabled,
                    weapon_switch_mode,
                    weapon_switch_a,
                    weapon_switch_b,
                    weapon_switch_c,
                    weapon_switch_now,
                    weapon_switch_before,
                    weapon_switch_toggle,
                    item_switch_mode,
                    voice_chat_output_device,
                    codec_output_device,
                    codec1_name,
                    codec1a,
                    codec1b,
                    codec1c,
                    codec1d,
                    codec2_name,
                    codec2a,
                    codec2b,
                    codec2c,
                    codec2d,
                    codec3_name,
                    codec3a,
                    codec3b,
                    codec3c,
                    codec3d,
                    codec4_name,
                    codec4a,
                    codec4b,
                    codec4c,
                    codec4d,
                    voice_chat_recognition_level,
                    voice_chat_volume,
                    headset_volume,
                    bgm_volume)
                SELECT
                    c.id,
                    COALESCE((stored.settings ->> 'onlineStatusMode')::int, 0),
                    COALESCE((stored.settings ->> 'emailFriendsOnly')::boolean, false),
                    COALESCE((stored.settings ->> 'receiveNotices')::boolean, true),
                    COALESCE((stored.settings ->> 'receiveInvites')::boolean, true),
                    COALESCE((stored.settings ->> 'normalViewVerticalInvert')::boolean, false),
                    COALESCE((stored.settings ->> 'normalViewHorizontalInvert')::boolean, false),
                    COALESCE((stored.settings ->> 'normalViewSpeed')::int, 5),
                    COALESCE((stored.settings ->> 'shoulderViewVerticalInvert')::boolean, false),
                    COALESCE((stored.settings ->> 'shoulderViewHorizontalInvert')::boolean, false),
                    COALESCE((stored.settings ->> 'shoulderViewSpeed')::int, 5),
                    COALESCE((stored.settings ->> 'firstViewVerticalInvert')::boolean, false),
                    COALESCE((stored.settings ->> 'firstViewHorizontalInvert')::boolean, false),
                    COALESCE((stored.settings ->> 'firstViewSpeed')::int, 5),
                    COALESCE((stored.settings ->> 'firstViewPlayerDirection')::boolean, true),
                    COALESCE((stored.settings ->> 'viewChangeSpeed')::int, 5),
                    COALESCE((stored.settings ->> 'firstViewMemory')::boolean, false),
                    COALESCE((stored.settings ->> 'radarLockNorth')::boolean, false),
                    COALESCE((stored.settings ->> 'radarFloorHide')::boolean, false),
                    COALESCE((stored.settings ->> 'hudDisplaySize')::int, 0),
                    COALESCE((stored.settings ->> 'hudHideNameTags')::boolean, false),
                    COALESCE((stored.settings ->> 'lockOnEnabled')::boolean, false),
                    COALESCE((stored.settings ->> 'weaponSwitchMode')::int, 2),
                    COALESCE((stored.settings ->> 'weaponSwitchA')::int, 0),
                    COALESCE((stored.settings ->> 'weaponSwitchB')::int, 1),
                    COALESCE((stored.settings ->> 'weaponSwitchC')::int, 2),
                    COALESCE((stored.settings ->> 'weaponSwitchNow')::int, 0),
                    COALESCE((stored.settings ->> 'weaponSwitchBefore')::int, 1),
                    COALESCE((stored.settings ->> 'weaponSwitchToggle')::int, 2),
                    COALESCE((stored.settings ->> 'itemSwitchMode')::int, 2),
                    COALESCE((stored.settings ->> 'voiceChatOutputDevice')::int, 0),
                    COALESCE((stored.settings ->> 'codecOutputDevice')::int, 0),
                    COALESCE(stored.settings ->> 'codec1Name', ''),
                    COALESCE((stored.settings ->> 'codec1a')::int, 1),
                    COALESCE((stored.settings ->> 'codec1b')::int, 3),
                    COALESCE((stored.settings ->> 'codec1c')::int, 4),
                    COALESCE((stored.settings ->> 'codec1d')::int, 2),
                    COALESCE(stored.settings ->> 'codec2Name', ''),
                    COALESCE((stored.settings ->> 'codec2a')::int, 10),
                    COALESCE((stored.settings ->> 'codec2b')::int, 12),
                    COALESCE((stored.settings ->> 'codec2c')::int, 13),
                    COALESCE((stored.settings ->> 'codec2d')::int, 11),
                    COALESCE(stored.settings ->> 'codec3Name', ''),
                    COALESCE((stored.settings ->> 'codec3a')::int, 14),
                    COALESCE((stored.settings ->> 'codec3b')::int, 16),
                    COALESCE((stored.settings ->> 'codec3c')::int, 17),
                    COALESCE((stored.settings ->> 'codec3d')::int, 15),
                    COALESCE(stored.settings ->> 'codec4Name', ''),
                    COALESCE((stored.settings ->> 'codec4a')::int, 5),
                    COALESCE((stored.settings ->> 'codec4b')::int, 7),
                    COALESCE((stored.settings ->> 'codec4c')::int, 8),
                    COALESCE((stored.settings ->> 'codec4d')::int, 6),
                    COALESCE((stored.settings ->> 'voiceChatRecognitionLevel')::int, 5),
                    COALESCE((stored.settings ->> 'voiceChatVolume')::int, 5),
                    COALESCE((stored.settings ->> 'headsetVolume')::int, 5),
                    COALESCE((stored.settings ->> 'bgmVolume')::int, 10)
                FROM characters AS c
                CROSS JOIN LATERAL (
                    SELECT CASE
                               WHEN c.gameplay_options IS NOT NULL
                                    AND left(btrim(c.gameplay_options), 1) = '{'
                               THEN c.gameplay_options::jsonb
                           END AS settings
                ) AS stored
                WHERE stored.settings IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "gameplay_options",
                table: "characters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gameplay_options",
                table: "characters",
                type: "text",
                nullable: true);

            // The reverse of the statement above, so a rollback keeps the settings rather
            // than resetting every character to the game's defaults.
            migrationBuilder.Sql(
                """
                UPDATE characters AS c
                SET gameplay_options = jsonb_build_object(
                        'onlineStatusMode', o.online_status_mode,
                        'emailFriendsOnly', o.email_friends_only,
                        'receiveNotices', o.receive_notices,
                        'receiveInvites', o.receive_invites,
                        'normalViewVerticalInvert', o.normal_view_vertical_invert,
                        'normalViewHorizontalInvert', o.normal_view_horizontal_invert,
                        'normalViewSpeed', o.normal_view_speed,
                        'shoulderViewVerticalInvert', o.shoulder_view_vertical_invert,
                        'shoulderViewHorizontalInvert', o.shoulder_view_horizontal_invert,
                        'shoulderViewSpeed', o.shoulder_view_speed,
                        'firstViewVerticalInvert', o.first_view_vertical_invert,
                        'firstViewHorizontalInvert', o.first_view_horizontal_invert,
                        'firstViewSpeed', o.first_view_speed,
                        'firstViewPlayerDirection', o.first_view_player_direction,
                        'viewChangeSpeed', o.view_change_speed,
                        'firstViewMemory', o.first_view_memory,
                        'radarLockNorth', o.radar_lock_north,
                        'radarFloorHide', o.radar_floor_hide,
                        'hudDisplaySize', o.hud_display_size,
                        'hudHideNameTags', o.hud_hide_name_tags,
                        'lockOnEnabled', o.lock_on_enabled,
                        'weaponSwitchMode', o.weapon_switch_mode,
                        'weaponSwitchA', o.weapon_switch_a,
                        'weaponSwitchB', o.weapon_switch_b,
                        'weaponSwitchC', o.weapon_switch_c,
                        'weaponSwitchNow', o.weapon_switch_now,
                        'weaponSwitchBefore', o.weapon_switch_before,
                        'weaponSwitchToggle', o.weapon_switch_toggle,
                        'itemSwitchMode', o.item_switch_mode,
                        'voiceChatOutputDevice', o.voice_chat_output_device,
                        'codecOutputDevice', o.codec_output_device,
                        'codec1Name', o.codec1_name,
                        'codec1a', o.codec1a,
                        'codec1b', o.codec1b,
                        'codec1c', o.codec1c,
                        'codec1d', o.codec1d,
                        'codec2Name', o.codec2_name,
                        'codec2a', o.codec2a,
                        'codec2b', o.codec2b,
                        'codec2c', o.codec2c,
                        'codec2d', o.codec2d,
                        'codec3Name', o.codec3_name,
                        'codec3a', o.codec3a,
                        'codec3b', o.codec3b,
                        'codec3c', o.codec3c,
                        'codec3d', o.codec3d,
                        'codec4Name', o.codec4_name,
                        'codec4a', o.codec4a,
                        'codec4b', o.codec4b,
                        'codec4c', o.codec4c,
                        'codec4d', o.codec4d,
                        'voiceChatRecognitionLevel', o.voice_chat_recognition_level,
                        'voiceChatVolume', o.voice_chat_volume,
                        'headsetVolume', o.headset_volume,
                        'bgmVolume', o.bgm_volume)::text
                FROM character_gameplay_options AS o
                WHERE o.character_id = c.id;
                """);

            migrationBuilder.DropTable(
                name: "character_gameplay_options");
        }
    }
}
