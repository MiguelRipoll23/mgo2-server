using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// One player's end-of-match statistics for a leased event game. The host
/// reports these as players leave, and the outcome is inferred once every
/// occupied participant of the match has reported.
/// <para>
/// The reports are a table rather than a map held by the process that happened
/// to receive them. A match is played across two processes — the lobby that
/// paired it and the gameplay server that hosts it — and a report that only
/// existed in the receiving process' memory would be lost by a restart at
/// exactly the moment the result matters.
/// </para>
/// </summary>
[Table("event_stat_reports")]
public sealed class EventStatReport
{
    /// <summary>Identifier of the report.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>Match the report belongs to.</summary>
    [Column("match_id")]
    public int MatchIdentifier { get; set; }

    /// <summary>Character the report describes.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Team the character played for.</summary>
    [Column("team_id")]
    public int TeamIdentifier { get; set; }

    /// <summary>Rounds the character's team won in the match.</summary>
    [Column("rounds_won")]
    public int RoundsWon { get; set; }

    /// <summary>Whether the character's report was for an aborted match.</summary>
    [Column("aborted")]
    public bool Aborted { get; set; }

    /// <summary>Score the character reported.</summary>
    [Column("score")]
    public int Score { get; set; }

    /// <summary>Kills the character reported.</summary>
    [Column("kills")]
    public int Kills { get; set; }

    /// <summary>Deaths the character reported.</summary>
    [Column("deaths")]
    public int Deaths { get; set; }

    /// <summary>Timestamp the report was written at.</summary>
    [Column("reported_at")]
    public DateTimeOffset ReportedAt { get; set; }
}
