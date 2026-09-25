using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// A pairing of two event teams. It is the unit matchmaking produces and the
/// unit a host is leased for, so it is deliberately not a room: a room is a
/// <see cref="Game"/>, and a match is played inside a leased one.
/// </summary>
[Table("event_matches")]
public sealed class EventMatch
{
    /// <summary>Identifier of the match.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>First of the two paired teams.</summary>
    [Column("first_team_id")]
    public int FirstTeamIdentifier { get; set; }

    /// <summary>Second of the two paired teams.</summary>
    [Column("second_team_id")]
    public int SecondTeamIdentifier { get; set; }

    /// <summary>Match type shared by both teams.</summary>
    [Column("match_type")]
    public int MatchType { get; set; }

    /// <summary>Lobby both teams belong to.</summary>
    [Column("lobby_id")]
    public int LobbyIdentifier { get; set; }

    /// <summary>Lifecycle state of the match.</summary>
    [Column("state")]
    public int State { get; set; }

    /// <summary>Team that won, once a result was reported.</summary>
    [Column("winner_team_id")]
    public int? WinnerTeamIdentifier { get; set; }

    /// <summary>Timestamp the pairing was made at.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the match was last changed at.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Timestamp the match finished at, when it has.</summary>
    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// The gameplay room a match is leased to. The unique game identifier is the
/// serialization point: two lobbies cannot lease the same room, and the version
/// column makes a confirmation that lost a race a refusal rather than an
/// overwrite.
/// </summary>
[Table("event_host_leases")]
public sealed class EventHostLease
{
    /// <summary>Identifier of the lease.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Match the lease belongs to.</summary>
    [Column("match_id")]
    public int MatchIdentifier { get; set; }

    /// <summary>Gameplay room the match is leased to.</summary>
    [Column("game_id")]
    public int GameIdentifier { get; set; }

    /// <summary>Active-state identifier the client correlates its cache with.</summary>
    [Column("active_state_id")]
    public int ActiveStateIdentifier { get; set; }

    /// <summary>Sequence the client echoes back on state mutations.</summary>
    [Column("active_state_sequence")]
    public int ActiveStateSequence { get; set; }

    /// <summary>Lobby the room belongs to.</summary>
    [Column("lobby_id")]
    public int LobbyIdentifier { get; set; }

    /// <summary>Game type of the lobby, cached so a sweep need not join.</summary>
    [Column("lobby_subtype")]
    public int LobbySubtype { get; set; }

    /// <summary>Lease status: active or released.</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>Timestamp the lease was taken at.</summary>
    [Column("leased_at")]
    public DateTimeOffset LeasedAt { get; set; }

    /// <summary>Timestamp the lease was last changed at.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Timestamp the lease was released at, when it has been.</summary>
    [Column("released_at")]
    public DateTimeOffset? ReleasedAt { get; set; }

    /// <summary>
    /// Optimistic-concurrency token. Mapped to PostgreSQL's <c>xmin</c> system
    /// column, so a confirmation that lost a race fails instead of overwriting a
    /// newer assignment.
    /// </summary>
    public uint Version { get; set; }
}

/// <summary>
/// The reward paid for one character in one completed match. It is appended
/// once, which is what makes a replayed completion harmless.
/// </summary>
[Table("event_round_rewards")]
public sealed class EventRoundReward
{
    /// <summary>Identifier of the reward.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Match the reward was paid for.</summary>
    [Column("match_id")]
    public int MatchIdentifier { get; set; }

    /// <summary>Character that was paid.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Team the character played for.</summary>
    [Column("team_id")]
    public int TeamIdentifier { get; set; }

    /// <summary>Amount paid.</summary>
    [Column("reward")]
    public int Reward { get; set; }

    /// <summary>Whether the reward was the participation payment rather than a win.</summary>
    [Column("is_participation")]
    public bool IsParticipation { get; set; }

    /// <summary>Timestamp the reward was written at.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
