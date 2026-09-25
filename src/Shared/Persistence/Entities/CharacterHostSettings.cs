using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>Host settings a character saved for a game type.</summary>
[Table("characters_host_settings")]
public sealed class CharacterHostSettings
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character owning the settings.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Game type the settings apply to.</summary>
    [Column("type")]
    public short Type { get; set; }

    [Column("name")]
    [MaxLength(16)]
    public string Name { get; set; } = string.Empty;

    [Column("password")]
    [MaxLength(16)]
    public string? Password { get; set; }

    [Column("comment")]
    [MaxLength(128)]
    public string Comment { get; set; } = string.Empty;

    [Column("stance")]
    public short Stance { get; set; }

    [Column("max_players")]
    public short MaxPlayers { get; set; } = 16;

    [Column("briefing_time")]
    public int BriefingTime { get; set; }

    [Column("dedicated")]
    public bool Dedicated { get; set; }

    [Column("non_stat")]
    public bool NonStat { get; set; }

    [Column("friendly_fire")]
    public bool FriendlyFire { get; set; }

    [Column("auto_aim")]
    public bool AutoAim { get; set; }

    [Column("uniques_enabled")]
    public bool UniquesEnabled { get; set; }

    [Column("enemy_nametags")]
    public bool EnemyNametags { get; set; }

    [Column("silent_mode")]
    public bool SilentMode { get; set; }

    [Column("auto_assign")]
    public bool AutoAssign { get; set; }

    [Column("teams_switch")]
    public bool TeamsSwitch { get; set; }

    [Column("ghosts")]
    public bool Ghosts { get; set; }

    [Column("voice_chat")]
    public bool VoiceChat { get; set; }

    [Column("level_limit_enabled")]
    public bool LevelLimitEnabled { get; set; }

    [Column("level_limit_base")]
    public int LevelLimitBase { get; set; }

    [Column("level_limit_tolerance")]
    public short LevelLimitTolerance { get; set; }

    [Column("team_kill_kick")]
    public short TeamKillKick { get; set; }

    [Column("idle_kick")]
    public short IdleKick { get; set; }

    [Column("settings_lobby_subtype")]
    public short SettingsLobbySubtype { get; set; }

    [Column("rotation_rules")]
    public short[]? RotationRules { get; set; }

    [Column("rotation_maps")]
    public short[]? RotationMaps { get; set; }

    [Column("rotation_flags")]
    public short[]? RotationFlags { get; set; }

    [Column("weapon_restrictions")]
    public byte[]? WeaponRestrictions { get; set; }

    [Column("rule_timers")]
    public int[]? RuleTimers { get; set; }

    [Column("unique_red")]
    public short UniqueRed { get; set; }

    [Column("unique_blue")]
    public short UniqueBlue { get; set; }

    [Column("common_a")]
    public short CommonA { get; set; }

    [Column("common_b")]
    public short CommonB { get; set; }

    [Column("capture_extra_time")]
    public bool CaptureExtraTime { get; set; }

    [Column("sneaking_snake_kills")]
    public short SneakingSnakeKills { get; set; } = 3;

    [Column("unread_800")]
    public short Unread800 { get; set; }

    [Column("unread_801")]
    public short Unread801 { get; set; }

    [Column("unread_824")]
    public long Unread824 { get; set; }

    [Column("unread_832")]
    public int Unread832 { get; set; }

    [Column("unread_836")]
    public long Unread836 { get; set; }

    [Column("unread_844")]
    public int Unread844 { get; set; }

    [Column("unread_931")]
    public short Unread931 { get; set; }

    [Column("unread_tail")]
    public byte[]? UnreadTail { get; set; }

    /// <summary>Character owning the settings.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}