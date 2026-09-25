using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// One member of a team's roster as it stood when the team entered the field.
/// <para>
/// The row exists because a roster is not the same thing as a team. A team is
/// live: members join, leave and are replaced, and the row the screens read is
/// rebuilt from it every time. A draw is not live — once a field is frozen the
/// pairings are settled, and a pairing card that named whoever happened to be
/// in the team when the card was written would name a team the draw never
/// matched. So the roster the draw was made with is copied here once, at
/// submission, and read from here afterwards.
/// </para>
/// <para>
/// The name is copied with the identifier because that is what the card
/// renders. Reading a name from the character would answer with whatever the
/// player has since renamed themselves to, which is a different answer from the
/// one the draw was made with.
/// </para>
/// </summary>
[Table("tournament_roster")]
public sealed class TournamentRosterMember
{
    /// <summary>Event the roster was submitted to.</summary>
    [Column("event_id")]
    public int EventIdentifier { get; set; }

    /// <summary>Team the member belongs to.</summary>
    [Column("team_id")]
    public int TeamIdentifier { get; set; }

    /// <summary>Position the member holds in the roster, counted from zero.</summary>
    [Column("member_index")]
    public int MemberIndex { get; set; }

    /// <summary>Character occupying the position.</summary>
    [Column("character_id")]
    public int CharacterIdentifier { get; set; }

    /// <summary>Name the member had when the roster was submitted.</summary>
    [Column("name")]
    [MaxLength(16)]
    public required string Name { get; set; }
}
