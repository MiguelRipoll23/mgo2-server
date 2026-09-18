using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// The gameplay options a character has set: one typed column per setting rather
/// than a serialized blob, so a setting can be read, compared and defaulted by the
/// database the way the rest of the schema is.
/// <para>
/// The initializers are the game's own defaults, byte for byte what the client sends
/// back when it has never stored anything, and they are what a character with no row
/// is served.
/// </para>
/// <para>
/// They are deliberately not also declared as column defaults. Entity Framework omits
/// a property from an insert when it holds the provider's sentinel, so a store default
/// would arrive in place of a value the client really chose: a player selecting codec
/// entry zero would be stored as one. One place holds the defaults, and it is this one.
/// </para>
/// <para>
/// One row per character, keyed by the character identifier. A character that has
/// never changed a setting has no row at all, which is the state its defaults
/// describe — writing one would claim the player chose them.
/// </para>
/// </summary>
[Table("character_gameplay_options")]
public sealed class CharacterGameplayOptions
{
    /// <summary>
    /// Character the settings belong to. A character has exactly one row, so the
    /// identifier is the key on both sides of the migration.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Who may see the character is online: 0 everyone, 1 friends, 2 nobody.</summary>
    [Column("online_status_mode")]
    public int OnlineStatusMode { get; set; }

    /// <summary>Whether email is offered only to friends.</summary>
    [Column("email_friends_only")]
    public bool EmailFriendsOnly { get; set; }

    /// <summary>Whether notices are received.</summary>
    [Column("receive_notices")]
    public bool ReceiveNotices { get; set; } = true;

    /// <summary>Whether invitations are received.</summary>
    [Column("receive_invites")]
    public bool ReceiveInvites { get; set; } = true;

    /// <summary>Whether the normal view's vertical axis is inverted.</summary>
    [Column("normal_view_vertical_invert")]
    public bool NormalViewVerticalInvert { get; set; }

    /// <summary>Whether the normal view's horizontal axis is inverted.</summary>
    [Column("normal_view_horizontal_invert")]
    public bool NormalViewHorizontalInvert { get; set; }

    /// <summary>Normal view speed, shown one-based from one to sixteen.</summary>
    [Column("normal_view_speed")]
    public int NormalViewSpeed { get; set; } = 5;

    /// <summary>Whether the shoulder view's vertical axis is inverted.</summary>
    [Column("shoulder_view_vertical_invert")]
    public bool ShoulderViewVerticalInvert { get; set; }

    /// <summary>Whether the shoulder view's horizontal axis is inverted.</summary>
    [Column("shoulder_view_horizontal_invert")]
    public bool ShoulderViewHorizontalInvert { get; set; }

    /// <summary>Shoulder view speed, shown one-based from one to sixteen.</summary>
    [Column("shoulder_view_speed")]
    public int ShoulderViewSpeed { get; set; } = 5;

    /// <summary>Whether the first-person view's vertical axis is inverted.</summary>
    [Column("first_view_vertical_invert")]
    public bool FirstViewVerticalInvert { get; set; }

    /// <summary>Whether the first-person view's horizontal axis is inverted.</summary>
    [Column("first_view_horizontal_invert")]
    public bool FirstViewHorizontalInvert { get; set; }

    /// <summary>First-person view speed, shown one-based from one to sixteen.</summary>
    [Column("first_view_speed")]
    public int FirstViewSpeed { get; set; } = 5;

    /// <summary>Whether the character turns the way the first-person view points.</summary>
    [Column("first_view_player_direction")]
    public bool FirstViewPlayerDirection { get; set; } = true;

    /// <summary>How fast the view changes, shown one-based from one to sixteen.</summary>
    [Column("view_change_speed")]
    public int ViewChangeSpeed { get; set; } = 5;

    /// <summary>Whether the first-person view is remembered.</summary>
    [Column("first_view_memory")]
    public bool FirstViewMemory { get; set; }

    /// <summary>Whether the radar holds north at the top.</summary>
    [Column("radar_lock_north")]
    public bool RadarLockNorth { get; set; }

    /// <summary>Whether the radar hides the floor the character is not on.</summary>
    [Column("radar_floor_hide")]
    public bool RadarFloorHide { get; set; }

    /// <summary>Size of the heads-up display: 0 small through 2 large.</summary>
    [Column("hud_display_size")]
    public int HudDisplaySize { get; set; }

    /// <summary>Whether the heads-up display hides name tags.</summary>
    [Column("hud_hide_name_tags")]
    public bool HudHideNameTags { get; set; }

    /// <summary>Whether lock-on is enabled.</summary>
    [Column("lock_on_enabled")]
    public bool LockOnEnabled { get; set; }

    /// <summary>How the weapon switch cycles: 0 through 2.</summary>
    [Column("weapon_switch_mode")]
    public int WeaponSwitchMode { get; set; } = 2;

    /// <summary>Direction the first weapon-switch slot selects: 0 through 15.</summary>
    [Column("weapon_switch_a")]
    public int WeaponSwitchA { get; set; }

    /// <summary>Direction the second weapon-switch slot selects: 0 through 15.</summary>
    [Column("weapon_switch_b")]
    public int WeaponSwitchB { get; set; } = 1;

    /// <summary>Direction the third weapon-switch slot selects: 0 through 15.</summary>
    [Column("weapon_switch_c")]
    public int WeaponSwitchC { get; set; } = 2;

    /// <summary>Direction the current weapon-switch slot selects: 0 through 15.</summary>
    [Column("weapon_switch_now")]
    public int WeaponSwitchNow { get; set; }

    /// <summary>Direction the weapon-switch slot selected before held: 0 through 15.</summary>
    [Column("weapon_switch_before")]
    public int WeaponSwitchBefore { get; set; } = 1;

    /// <summary>Direction the toggled weapon-switch slot selects: 0 through 15.</summary>
    [Column("weapon_switch_toggle")]
    public int WeaponSwitchToggle { get; set; } = 2;

    /// <summary>How the item switch cycles: 0 through 2.</summary>
    [Column("item_switch_mode")]
    public int ItemSwitchMode { get; set; } = 2;

    /// <summary>Device the voice chat plays through.</summary>
    [Column("voice_chat_output_device")]
    public int VoiceChatOutputDevice { get; set; }

    /// <summary>Device the codec plays through.</summary>
    [Column("codec_output_device")]
    public int CodecOutputDevice { get; set; }

    /// <summary>Name of the first codec shortcut.</summary>
    [Column("codec1_name")]
    [MaxLength(64)]
    public string Codec1Name { get; set; } = string.Empty;

    /// <summary>First entry of the first codec shortcut.</summary>
    [Column("codec1a")]
    public int Codec1A { get; set; } = 1;

    /// <summary>Second entry of the first codec shortcut.</summary>
    [Column("codec1b")]
    public int Codec1B { get; set; } = 3;

    /// <summary>Third entry of the first codec shortcut.</summary>
    [Column("codec1c")]
    public int Codec1C { get; set; } = 4;

    /// <summary>Fourth entry of the first codec shortcut.</summary>
    [Column("codec1d")]
    public int Codec1D { get; set; } = 2;

    /// <summary>Name of the second codec shortcut.</summary>
    [Column("codec2_name")]
    [MaxLength(64)]
    public string Codec2Name { get; set; } = string.Empty;

    /// <summary>First entry of the second codec shortcut.</summary>
    [Column("codec2a")]
    public int Codec2A { get; set; } = 10;

    /// <summary>Second entry of the second codec shortcut.</summary>
    [Column("codec2b")]
    public int Codec2B { get; set; } = 12;

    /// <summary>Third entry of the second codec shortcut.</summary>
    [Column("codec2c")]
    public int Codec2C { get; set; } = 13;

    /// <summary>Fourth entry of the second codec shortcut.</summary>
    [Column("codec2d")]
    public int Codec2D { get; set; } = 11;

    /// <summary>Name of the third codec shortcut.</summary>
    [Column("codec3_name")]
    [MaxLength(64)]
    public string Codec3Name { get; set; } = string.Empty;

    /// <summary>First entry of the third codec shortcut.</summary>
    [Column("codec3a")]
    public int Codec3A { get; set; } = 14;

    /// <summary>Second entry of the third codec shortcut.</summary>
    [Column("codec3b")]
    public int Codec3B { get; set; } = 16;

    /// <summary>Third entry of the third codec shortcut.</summary>
    [Column("codec3c")]
    public int Codec3C { get; set; } = 17;

    /// <summary>Fourth entry of the third codec shortcut.</summary>
    [Column("codec3d")]
    public int Codec3D { get; set; } = 15;

    /// <summary>Name of the fourth codec shortcut.</summary>
    [Column("codec4_name")]
    [MaxLength(64)]
    public string Codec4Name { get; set; } = string.Empty;

    /// <summary>First entry of the fourth codec shortcut.</summary>
    [Column("codec4a")]
    public int Codec4A { get; set; } = 5;

    /// <summary>Second entry of the fourth codec shortcut.</summary>
    [Column("codec4b")]
    public int Codec4B { get; set; } = 7;

    /// <summary>Third entry of the fourth codec shortcut.</summary>
    [Column("codec4c")]
    public int Codec4C { get; set; } = 8;

    /// <summary>Fourth entry of the fourth codec shortcut.</summary>
    [Column("codec4d")]
    public int Codec4D { get; set; } = 6;

    /// <summary>How readily voice chat is recognised.</summary>
    [Column("voice_chat_recognition_level")]
    public int VoiceChatRecognitionLevel { get; set; } = 5;

    /// <summary>Voice chat volume.</summary>
    [Column("voice_chat_volume")]
    public int VoiceChatVolume { get; set; } = 5;

    /// <summary>Headset volume.</summary>
    [Column("headset_volume")]
    public int HeadsetVolume { get; set; } = 5;

    /// <summary>Background music volume.</summary>
    [Column("bgm_volume")]
    public int BgmVolume { get; set; } = 10;

    /// <summary>Character the settings belong to.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
