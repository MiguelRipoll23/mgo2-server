using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// A scheduled Tournament or Survival event.
/// <para>
/// The row is what makes an event identifier mean something. A client names an
/// event in every Tournament request, and without a row behind that identifier
/// the name resolves to nothing and every name is equally valid — so a place
/// could be taken in an event that was never announced, was unpublished, or
/// does not exist. This is the row the registration rules read to answer that.
/// </para>
/// <para>
/// The window is expressed in whole Unix epoch seconds rather than in a stored
/// timestamp, so an operator states the hours an event runs and the row does not
/// go stale against a clock.
/// </para>
/// </summary>
[Table("event_schedules")]
public sealed class EventSchedule
{
    /// <summary>Identifier of the event, which is what clients name.</summary>
    [Key]
    [Column("id")]
    public int Identifier { get; set; }

    /// <summary>
    /// Lobby mode the event is played in, which is also what makes it a
    /// Tournament: a Tournament is only ever held in the registration lobby, so
    /// a row naming any other mode is not one a player may enter.
    /// </summary>
    [Column("lobby_subtype")]
    public int LobbySubtype { get; set; }

    /// <summary>Whether the event is published at all. An unpublished row is closed.</summary>
    [Column("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Epoch second the event is published from.</summary>
    [Column("publish_start")]
    public long PublishStart { get; set; }

    /// <summary>
    /// Epoch second publication ends at, or zero for an event that stays
    /// published. Zero is not "ended at the epoch": it is the way an operator
    /// says the event has no closing moment.
    /// </summary>
    [Column("publish_end")]
    public long PublishEnd { get; set; }

    /// <summary>Number of teams the field holds.</summary>
    [Column("team_capacity")]
    public int TeamCapacity { get; set; }
}
