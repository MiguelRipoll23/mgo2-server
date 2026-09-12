using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>An entry of a character's friends or blocked list.</summary>
[Table("characters_friends")]
public sealed class CharacterFriend
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character owning the entry.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Character the entry refers to.</summary>
    [Column("target_id")]
    public int TargetIdentifier { get; set; }

    /// <summary>Kind of entry: friend or blocked.</summary>
    [Column("type")]
    public int Type { get; set; }

    /// <summary>Character owning the entry.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }

    /// <summary>Character the entry refers to.</summary>
    [ForeignKey(nameof(TargetIdentifier))]
    public Character? Target { get; set; }
}

/// <summary>A saved skill set of a character.</summary>
[Table("characters_sets_skills")]
public sealed class CharacterSkillSet
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character owning the skill set.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Slot index of the skill set.</summary>
    [Column("idx")]
    public int Index { get; set; }

    /// <summary>Name given to the skill set.</summary>
    [Column("name")]
    [MaxLength(63)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Bitmask of the game modes the set applies to.</summary>
    [Column("modes")]
    public int Modes { get; set; }

    /// <summary>First skill slot.</summary>
    [Column("skill_1")]
    public int Skill1 { get; set; }

    /// <summary>Second skill slot.</summary>
    [Column("skill_2")]
    public int Skill2 { get; set; }

    /// <summary>Third skill slot.</summary>
    [Column("skill_3")]
    public int Skill3 { get; set; }

    /// <summary>Fourth skill slot.</summary>
    [Column("skill_4")]
    public int Skill4 { get; set; }

    /// <summary>Level of the first skill slot.</summary>
    [Column("level_1")]
    public int Level1 { get; set; }

    /// <summary>Level of the second skill slot.</summary>
    [Column("level_2")]
    public int Level2 { get; set; }

    /// <summary>Level of the third skill slot.</summary>
    [Column("level_3")]
    public int Level3 { get; set; }

    /// <summary>Level of the fourth skill slot.</summary>
    [Column("level_4")]
    public int Level4 { get; set; }

    /// <summary>Character owning the skill set.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}

/// <summary>The skills a character currently has equipped.</summary>
[Table("characters_equipped_skills")]
public sealed class CharacterEquippedSkill
{
    /// <summary>
    /// Character the row belongs to. A character has exactly one row, so the
    /// character identifier is the key on both sides of the migration.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>First equipped skill.</summary>
    [Column("skill_1")]
    public int Skill1 { get; set; }

    /// <summary>Second equipped skill.</summary>
    [Column("skill_2")]
    public int Skill2 { get; set; }

    /// <summary>Third equipped skill.</summary>
    [Column("skill_3")]
    public int Skill3 { get; set; }

    /// <summary>Fourth equipped skill.</summary>
    [Column("skill_4")]
    public int Skill4 { get; set; }

    /// <summary>Level of the first equipped skill.</summary>
    [Column("level_1")]
    public int Level1 { get; set; }

    /// <summary>Level of the second equipped skill.</summary>
    [Column("level_2")]
    public int Level2 { get; set; }

    /// <summary>Level of the third equipped skill.</summary>
    [Column("level_3")]
    public int Level3 { get; set; }

    /// <summary>Level of the fourth equipped skill.</summary>
    [Column("level_4")]
    public int Level4 { get; set; }

    /// <summary>Character the row belongs to.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}

/// <summary>A saved gear set of a character.</summary>
[Table("characters_sets_gear")]
public sealed class CharacterGearSet
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character owning the gear set.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Slot index of the gear set.</summary>
    [Column("idx")]
    public int Index { get; set; }

    /// <summary>Name given to the gear set.</summary>
    [Column("name")]
    [MaxLength(63)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Bitmask of the stages the set applies to.</summary>
    [Column("stages")]
    public int Stages { get; set; }

    /// <summary>Face index.</summary>
    [Column("face")]
    public int Face { get; set; }

    /// <summary>Head equipment index.</summary>
    [Column("head")]
    public int Head { get; set; }

    /// <summary>Head equipment color index.</summary>
    [Column("head_color")]
    public int HeadColor { get; set; }

    /// <summary>Upper-body equipment index.</summary>
    [Column("upper")]
    public int Upper { get; set; }

    /// <summary>Upper-body equipment color index.</summary>
    [Column("upper_color")]
    public int UpperColor { get; set; }

    /// <summary>Lower-body equipment index.</summary>
    [Column("lower")]
    public int Lower { get; set; }

    /// <summary>Lower-body equipment color index.</summary>
    [Column("lower_color")]
    public int LowerColor { get; set; }

    /// <summary>Chest equipment index.</summary>
    [Column("chest")]
    public int Chest { get; set; }

    /// <summary>Chest equipment color index.</summary>
    [Column("chest_color")]
    public int ChestColor { get; set; }

    /// <summary>Waist equipment index.</summary>
    [Column("waist")]
    public int Waist { get; set; }

    /// <summary>Waist equipment color index.</summary>
    [Column("waist_color")]
    public int WaistColor { get; set; }

    /// <summary>Hand equipment index.</summary>
    [Column("hands")]
    public int Hands { get; set; }

    /// <summary>Hand equipment color index.</summary>
    [Column("hands_color")]
    public int HandsColor { get; set; }

    /// <summary>Foot equipment index.</summary>
    [Column("feet")]
    public int Feet { get; set; }

    /// <summary>Foot equipment color index.</summary>
    [Column("feet_color")]
    public int FeetColor { get; set; }

    /// <summary>First accessory index.</summary>
    [Column("accessory1")]
    public int Accessory1 { get; set; }

    /// <summary>First accessory color index.</summary>
    [Column("accessory1_color")]
    public int Accessory1Color { get; set; }

    /// <summary>Second accessory index.</summary>
    [Column("accessory2")]
    public int Accessory2 { get; set; }

    /// <summary>Second accessory color index.</summary>
    [Column("accessory2_color")]
    public int Accessory2Color { get; set; }

    /// <summary>Face paint index.</summary>
    [Column("face_paint")]
    public int FacePaint { get; set; }

    /// <summary>Character owning the gear set.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}

/// <summary>Host settings a character saved for a game type.</summary>
[Table("characters_hostsettings")]
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
    public int Type { get; set; }

    /// <summary>Serialized settings blob.</summary>
    [Column("settings")]
    public required string Settings { get; set; }

    /// <summary>Character owning the settings.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}

/// <summary>A chat macro configured by a character.</summary>
[Table("characters_chatmacros")]
public sealed class CharacterChatMacro
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character owning the macro.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Macro page the entry belongs to.</summary>
    [Column("type")]
    public int Type { get; set; }

    /// <summary>Slot index of the macro.</summary>
    [Column("idx")]
    public int Index { get; set; }

    /// <summary>Macro text.</summary>
    [Column("text")]
    [MaxLength(64)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Character owning the macro.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
