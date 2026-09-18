using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>A player character belonging to an account.</summary>
[Table("characters")]
public sealed class Character
{
    /// <summary>Identifier of the character.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Account that owns the character.</summary>
    [Column("user_id")]
    public int UserIdentifier { get; set; }

    /// <summary>Name shown to other players.</summary>
    [Column("name")]
    [MaxLength(16)]
    public required string Name { get; set; }

    /// <summary>Previous name of the character, kept for display history.</summary>
    [Column("old_name")]
    [MaxLength(16)]
    public string? OldName { get; set; }

    /// <summary>Rank of the character.</summary>
    [Column("rank")]
    public int Rank { get; set; }

    /// <summary>Free-form comment shown on the character card.</summary>
    [Column("comment")]
    [MaxLength(128)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>Experience earned by the character.</summary>
    [Column("experience")]
    public int Experience { get; set; }

    /// <summary>
    /// Reward points the character holds. This is the figure the player-details
    /// card renders under its own "TOTAL REWARDS" label, and the same cell the
    /// cumulative statistics matrix carries in its summary row.
    /// </summary>
    [Column("total_rewards")]
    public int TotalRewards { get; set; }

    /// <summary>
    /// Gameplay options the character has set, when it has a row. A character that has
    /// never changed a setting has none, which is the state the defaults describe.
    /// </summary>
    public CharacterGameplayOptions? GameplayOptions { get; set; }

    /// <summary>Unix timestamp the character was created at.</summary>
    [Column("creation_time")]
    public int CreationTime { get; set; }

    /// <summary>
    /// Unix timestamp of the login recorded before the character's most recent one, or
    /// <c>null</c> when none has been recorded since the column existed. The client shows
    /// it beside the current login on the character card.
    /// </summary>
    [Column("previous_login_time")]
    public int? PreviousLoginTime { get; set; }

    /// <summary>
    /// Unix timestamp of the character's most recent login, or <c>null</c> when none has
    /// been recorded since the column existed. One title family measures an absence from
    /// this stamp, so it is written before that family is evaluated, not after.
    /// </summary>
    [Column("last_login_time")]
    public int? LastLoginTime { get; set; }

    /// <summary>
    /// Whether the character may be used: a suspended character keeps its row, its name
    /// and its history, and is filtered out of everything that lists characters.
    /// </summary>
    [Column("active")]
    public bool Active { get; set; } = true;

    /// <summary>Account that owns the character.</summary>
    [ForeignKey(nameof(UserIdentifier))]
    public User? User { get; set; }

    /// <summary>Appearance of the character.</summary>
    public CharacterAppearance? Appearance { get; set; }

    /// <summary>Statistics of the character.</summary>
    public CharacterStatistics? Statistics { get; set; }
}
