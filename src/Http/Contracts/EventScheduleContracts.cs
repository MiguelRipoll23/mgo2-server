using System.ComponentModel.DataAnnotations;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Http.Contracts;

/// <summary>One event schedule as the API publishes it.</summary>
/// <param name="Id">Identifier a client names to enter the event.</param>
/// <param name="LobbySubtypeId">Lobby mode the event is played in.</param>
/// <param name="Enabled">Whether the event is published at all.</param>
/// <param name="PublishStart">Epoch second the event is published from.</param>
/// <param name="PublishEnd">Epoch second it stops being published, or zero for none.</param>
/// <param name="TeamCapacity">Number of teams the field holds.</param>
public sealed record EventScheduleContract(
    int Id,
    int LobbySubtypeId,
    bool Enabled,
    long PublishStart,
    long PublishEnd,
    int TeamCapacity);

/// <summary>Fields accepted when an event is scheduled.</summary>
public sealed class EventScheduleRequest
{
    /// <summary>
    /// Lobby mode the event is played in: Survival, Tournament, or Tournament
    /// registration. An event cannot be played in any other lobby.
    /// </summary>
    [Range(EventConstants.SurvivalSelector, EventConstants.TournamentRegistrationSelector)]
    public int LobbySubtypeId { get; set; } = EventConstants.TournamentRegistrationSelector;

    /// <summary>Whether the event is published at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Epoch second the event is published from.</summary>
    [Range(0, long.MaxValue)]
    public long PublishStart { get; set; }

    /// <summary>Epoch second it stops being published, or zero for an open-ended one.</summary>
    [Range(0, long.MaxValue)]
    public long PublishEnd { get; set; }

    /// <summary>Number of teams the field holds.</summary>
    [Range(1, EventConstants.BracketMaximumEntrants)]
    public int TeamCapacity { get; set; } = EventConstants.BracketMaximumEntrants;
}

/// <summary>
/// Fields accepted when a schedule is changed. The window is stated whole
/// rather than as a difference, because a schedule says when an event runs and
/// a partial statement is one nobody can read.
/// </summary>
public sealed class EventSchedulePatchRequest
{
    /// <summary>Whether the event is published at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Epoch second the event is published from.</summary>
    [Range(0, long.MaxValue)]
    public long PublishStart { get; set; }

    /// <summary>Epoch second it stops being published, or zero for an open-ended one.</summary>
    [Range(0, long.MaxValue)]
    public long PublishEnd { get; set; }

    /// <summary>Number of teams the field holds.</summary>
    [Range(1, EventConstants.BracketMaximumEntrants)]
    public int TeamCapacity { get; set; } = EventConstants.BracketMaximumEntrants;
}

/// <summary>Projects a schedule onto its contract.</summary>
/// <param name="schedule">Schedule to project.</param>
public static class EventScheduleContractMapping
{
    /// <summary>Projects a schedule onto the contract the API publishes it as.</summary>
    /// <param name="schedule">Schedule to project.</param>
    public static EventScheduleContract ToContract(this EventSchedule schedule) =>
        new(
            schedule.Identifier,
            schedule.LobbySubtype,
            schedule.Enabled,
            schedule.PublishStart,
            schedule.PublishEnd,
            EventScheduleService.TeamCapacityOf(schedule));
}
