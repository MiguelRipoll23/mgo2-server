using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>Appearance of a character, stored as flat customization indices.</summary>
[Table("characters_appearance")]
public sealed class CharacterAppearance
{
    /// <summary>Identifier of the row.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character the appearance belongs to.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Gender index.</summary>
    [Column("gender")]
    public int Gender { get; set; }

    /// <summary>Face index.</summary>
    [Column("face")]
    public int Face { get; set; }

    /// <summary>Voice index.</summary>
    [Column("voice")]
    public int Voice { get; set; }

    /// <summary>Voice pitch index.</summary>
    [Column("pitch")]
    public int Pitch { get; set; }

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

    /// <summary>Character the appearance belongs to.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
