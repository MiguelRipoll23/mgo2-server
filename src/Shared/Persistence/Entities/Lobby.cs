using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>Kind of service a lobby row describes.</summary>
public enum LobbyType
{
    /// <summary>Gate server entry.</summary>
    Gate = 0,

    /// <summary>Account server entry.</summary>
    Account = 1,

    /// <summary>Gameplay lobby entry.</summary>
    Game = 2,
}

/// <summary>
/// A listening endpoint published to clients. The client expects the list index
/// and the lobby type to coincide (index zero is the gate, one the account,
/// two the first gameplay lobby), so rows are ordered by identifier.
/// </summary>
[Table("lobbies")]
public sealed class Lobby
{
    /// <summary>Identifier of the lobby.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Kind of service the row describes.</summary>
    [Column("type_id")]
    public LobbyType Type { get; set; }

    /// <summary>Game type of the lobby, referencing <see cref="LobbyGameType.Identifier"/>.</summary>
    [Column("subtype_id")]
    public int SubtypeIdentifier { get; set; }

    /// <summary>Display name of the lobby.</summary>
    [Column("name")]
    [MaxLength(16)]
    public required string Name { get; set; }

    /// <summary>Address clients are told to connect to.</summary>
    [Column("ip_address")]
    [MaxLength(15)]
    public required string IpAddress { get; set; }

    /// <summary>Port the lobby listens on.</summary>
    [Column("port")]
    public int Port { get; set; }

    /// <summary>Number of players currently in the lobby.</summary>
    [Column("players_count")]
    public int PlayersCount { get; set; }

    /// <summary>Whether the lobby only accepts beginners.</summary>
    [Column("beginner_only")]
    public bool BeginnerOnly { get; set; }

    /// <summary>Whether the lobby only accepts expansion owners.</summary>
    [Column("expansion_only")]
    public bool ExpansionOnly { get; set; }

    /// <summary>Whether the lobby disables headshots.</summary>
    [Column("no_headshot")]
    public bool NoHeadshot { get; set; }

    /// <summary>Whether the lobby only accepts replays.</summary>
    [Column("replays_only")]
    public bool ReplaysOnly { get; set; }

    /// <summary>Timestamp the row was registered at.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last heartbeat. A gameplay lobby is served to clients
    /// only while this stamp is recent, so a lobby whose server stopped must
    /// heartbeat eventually disappears from the lobby list on its own.
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Game type of the lobby.</summary>
    [ForeignKey(nameof(SubtypeIdentifier))]
    public LobbyGameType? Subtype { get; set; }
}

/// <summary>A game type a lobby can be configured with.</summary>
[Table("lobby_game_types")]
public sealed class LobbyGameType
{
    /// <summary>Identifier of the game type; equals the wire-format game identifier.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Game identifier used by the wire format.</summary>
    [Column("game_id")]
    public int GameIdentifier { get; set; }

    /// <summary>Display name of the game type.</summary>
    [Column("name")]
    [MaxLength(64)]
    public required string Name { get; set; }
}
