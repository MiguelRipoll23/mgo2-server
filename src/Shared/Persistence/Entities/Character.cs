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

    /// <summary>Accumulated host score of the character.</summary>
    [Column("host_score")]
    public int HostScore { get; set; }

    /// <summary>Number of host votes the character received.</summary>
    [Column("host_votes")]
    public int HostVotes { get; set; }

    /// <summary>Experience earned by the character.</summary>
    [Column("experience")]
    public int Experience { get; set; }

    /// <summary>Serialized gameplay options blob.</summary>
    [Column("gameplay_options")]
    public string? GameplayOptions { get; set; }

    /// <summary>Unix timestamp the character was created at.</summary>
    [Column("creation_time")]
    public int CreationTime { get; set; }

    /// <summary>Whether the character may be used: zero means suspended.</summary>
    [Column("active")]
    public int Active { get; set; } = 1;

    /// <summary>Lobby the character is currently in, when known.</summary>
    [Column("lobby_id")]
    public int? LobbyIdentifier { get; set; }

    /// <summary>Account that owns the character.</summary>
    [ForeignKey(nameof(UserIdentifier))]
    public User? User { get; set; }

    /// <summary>Lobby the character is currently in.</summary>
    [ForeignKey(nameof(LobbyIdentifier))]
    public Lobby? Lobby { get; set; }

    /// <summary>Appearance of the character.</summary>
    public CharacterAppearance? Appearance { get; set; }

    /// <summary>Statistics of the character.</summary>
    public CharacterStatistics? Statistics { get; set; }
}
