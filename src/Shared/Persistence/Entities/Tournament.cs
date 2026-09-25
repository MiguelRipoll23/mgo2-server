using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// A reserved or registered Tournament place. Capacity is counted from the
/// event, and the unique character key is what stops one character taking two
/// places.
/// </summary>
[Table("tournament_registrations")]
public sealed class TournamentRegistration
{
    /// <summary>Identifier of the registration.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Event the place was taken in.</summary>
    [Column("event_id")]
    public int EventIdentifier { get; set; }

    /// <summary>Character holding the place.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Team the character registered with, when it has one.</summary>
    [Column("team_id")]
    public int? TeamIdentifier { get; set; }

    /// <summary>Slot the place occupies, assigned when registration closes.</summary>
    [Column("slot_index")]
    public int SlotIndex { get; set; }

    /// <summary>Timestamp the place was reserved at.</summary>
    [Column("reserved_at")]
    public DateTimeOffset ReservedAt { get; set; }
}

/// <summary>
/// The header of one seeded Tournament bracket. Seeds and results hang from it,
/// so a restart mid-tournament resumes the same bracket.
/// </summary>
[Table("tournament_brackets")]
public sealed class TournamentBracket
{
    /// <summary>Event the bracket belongs to.</summary>
    [Key]
    [Column("event_id")]
    public int EventIdentifier { get; set; }

    /// <summary>Lifecycle state of the bracket.</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>Round the bracket is currently playing.</summary>
    [Column("current_round")]
    public int CurrentRound { get; set; }

    /// <summary>Team that won the bracket, once it has.</summary>
    [Column("champion_team_id")]
    public int? ChampionTeamIdentifier { get; set; }

    /// <summary>Timestamp the bracket was seeded at.</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp the bracket was last changed at.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One entrant of a bracket, at a fixed position. The seed index is the bracket
/// position, so round-one pairings follow from the ordered list and a restart
/// reconstructs who was meant to play whom.
/// </summary>
[Table("tournament_seeds")]
public sealed class TournamentSeed
{
    /// <summary>Event the seed belongs to.</summary>
    [Column("event_id")]
    public int EventIdentifier { get; set; }

    /// <summary>Position of the seed inside the bracket.</summary>
    [Column("seed_index")]
    public int SeedIndex { get; set; }

    /// <summary>Team occupying the position.</summary>
    [Column("team_id")]
    public int TeamIdentifier { get; set; }

    /// <summary>Bracket the seed belongs to.</summary>
    [ForeignKey(nameof(EventIdentifier))]
    public TournamentBracket? Bracket { get; set; }
}

/// <summary>
/// The ledger of one played bracket fixture. Advancement is derived from this
/// ledger rather than by mutating a tree, so a replayed result is idempotent.
/// </summary>
[Table("tournament_results")]
public sealed class TournamentResult
{
    /// <summary>Identifier of the result.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Event the fixture was played in.</summary>
    [Column("event_id")]
    public int EventIdentifier { get; set; }

    /// <summary>Match the result belongs to.</summary>
    [Column("match_id")]
    public int MatchIdentifier { get; set; }

    /// <summary>Round the fixture was played in, counted from one.</summary>
    [Column("round_index")]
    public int RoundIndex { get; set; }

    /// <summary>First of the two teams.</summary>
    [Column("first_team_id")]
    public int FirstTeamIdentifier { get; set; }

    /// <summary>Second of the two teams.</summary>
    [Column("second_team_id")]
    public int SecondTeamIdentifier { get; set; }

    /// <summary>Team that won the fixture.</summary>
    [Column("winner_team_id")]
    public int WinnerTeamIdentifier { get; set; }

    /// <summary>Timestamp the result was reported at.</summary>
    [Column("reported_at")]
    public DateTimeOffset ReportedAt { get; set; }
}
