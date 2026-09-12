using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>A game room hosted by a player inside a gameplay lobby.</summary>
[Table("games")]
public sealed class Game
{
    /// <summary>Identifier of the room.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Character hosting the room.</summary>
    [Column("host_id")]
    public int HostIdentifier { get; set; }

    /// <summary>Lobby the room belongs to.</summary>
    [Column("lobby_id")]
    public int LobbyIdentifier { get; set; }

    /// <summary>Display name of the room.</summary>
    [Column("name")]
    [MaxLength(16)]
    public required string Name { get; set; }

    /// <summary>Join password of the room; empty when the room is open.</summary>
    [Column("password")]
    [MaxLength(15)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Free-form comment shown in the room list.</summary>
    [Column("comment")]
    [MaxLength(128)]
    public string Comment { get; set; } = string.Empty;

    /// <summary>Maximum number of players the room accepts.</summary>
    [Column("max_players")]
    public int MaximumPlayers { get; set; } = 8;

    /// <summary>Index of the game currently selected in the rotation.</summary>
    [Column("current_game")]
    public int CurrentGame { get; set; }

    /// <summary>Serialized game rotation.</summary>
    [Column("games")]
    public string Games { get; set; } = "[]";

    /// <summary>Stance the host allows in the room.</summary>
    [Column("stance")]
    public int Stance { get; set; }

    /// <summary>Reported ping of the hosting client.</summary>
    [Column("ping")]
    public int Ping { get; set; }

    /// <summary>Serialized common settings block.</summary>
    [Column("common")]
    public string Common { get; set; } = "{}";

    /// <summary>Serialized rule settings block.</summary>
    [Column("rules")]
    public string Rules { get; set; } = "{}";

    /// <summary>Status of the room.</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>Timestamp without time zone the room was created at.</summary>
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last heartbeat. Only a dedicated host maintains this
    /// stamp; a dedicated-host match whose heartbeat stopped is expired, while
    /// a player-hosted room keeps its current lifetime.
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Hosting character.</summary>
    [ForeignKey(nameof(HostIdentifier))]
    public Character? Host { get; set; }

    /// <summary>Lobby the room belongs to.</summary>
    [ForeignKey(nameof(LobbyIdentifier))]
    public Lobby? Lobby { get; set; }

    /// <summary>Players currently in the room.</summary>
    public ICollection<GamePlayer> Players { get; set; } = [];
}

/// <summary>
/// The per-room roster with team slots and the host-reported ping shown by the
/// player-list and game-details screens.
/// </summary>
[Table("game_players")]
public sealed class GamePlayer
{
    /// <summary>Room the player is in.</summary>
    [Column("game_id")]
    public int GameIdentifier { get; set; }

    /// <summary>Character in the room.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Team slot assigned to the player.</summary>
    [Column("team")]
    public short Team { get; set; }

    /// <summary>Ping reported by the host for this player.</summary>
    [Column("ping")]
    public int Ping { get; set; }

    /// <summary>Timestamp the player joined at.</summary>
    [Column("joined_at")]
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>Room the player is in.</summary>
    [ForeignKey(nameof(GameIdentifier))]
    public Game? Game { get; set; }

    /// <summary>Character in the room.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}

/// <summary>
/// Snapshot of the roster at round start, so an end-of-round report still
/// applies to a player who left before it arrived.
/// </summary>
[Table("game_rounds")]
public sealed class GameRound
{
    /// <summary>Room the round was played in.</summary>
    [Column("game_id")]
    public int GameIdentifier { get; set; }

    /// <summary>Character that was present when the round started.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Room the round was played in.</summary>
    [ForeignKey(nameof(GameIdentifier))]
    public Game? Game { get; set; }

    /// <summary>Character present at round start.</summary>
    [ForeignKey(nameof(CharacterIdentifier))]
    public Character? Character { get; set; }
}
