using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>An account playing the game.</summary>
[Table("users")]
public sealed class User
{
    /// <summary>Identifier of the account.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Login name of the account.</summary>
    [Column("display_name")]
    [MaxLength(32)]
    public required string DisplayName { get; set; }

    /// <summary>Hashed password of the account.</summary>
    [Column("password")]
    [MaxLength(255)]
    public required string Password { get; set; }

    /// <summary>Role of the account: zero for players, higher values for administrators.</summary>
    [Column("role")]
    public int Role { get; set; }

    /// <summary>Unix timestamp until which the account is banned, or <c>null</c> when it is not banned.</summary>
    [Column("banned_until")]
    public int? BannedUntil { get; set; }

    /// <summary>Reason shown to a banned account.</summary>
    [Column("ban_reason")]
    [MaxLength(255)]
    public string? BanReason { get; set; }

    /// <summary>Number of character slots the account owns.</summary>
    [Column("slots")]
    public int Slots { get; set; } = 3;

    /// <summary>Character the account most recently selected.</summary>
    [Column("current_character_id")]
    public int? CurrentCharacterIdentifier { get; set; }

    /// <summary>Character the account designates as its main.</summary>
    [Column("main_character_id")]
    public int? MainCharacterIdentifier { get; set; }

    /// <summary>Experience banked by the account's main character.</summary>
    [Column("main_exp")]
    public int MainExperience { get; set; }

    /// <summary>Experience banked by the account's alternate characters.</summary>
    [Column("alt_exp")]
    public int AlternateExperience { get; set; }

    /// <summary>Character most recently selected by the account.</summary>
    [ForeignKey(nameof(CurrentCharacterIdentifier))]
    public Character? CurrentCharacter { get; set; }

    /// <summary>Character designated as the account's main.</summary>
    [ForeignKey(nameof(MainCharacterIdentifier))]
    public Character? MainCharacter { get; set; }

    /// <summary>Characters owned by the account.</summary>
    public ICollection<Character> Characters { get; set; } = [];
}
