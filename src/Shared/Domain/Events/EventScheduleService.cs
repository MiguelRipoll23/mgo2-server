using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Owns the question "is this event open, and how big is its field".
/// <para>
/// The answer is read from a stored row rather than from configuration because
/// the identifier a client names has to be resolved against something. A
/// configured bracket size says how large a field is; it says nothing about
/// whether the event that field belongs to is one anybody may enter.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class EventScheduleService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Returns the schedule of an event that is open to players right now, or
    /// null when there is no such event.
    /// <para>
    /// An identifier naming no row, a row that is unpublished, a row that has
    /// not opened yet and a row that has already closed are all the same answer,
    /// because to a client they are the same thing: there is no event here to
    /// enter.
    /// </para>
    /// </summary>
    /// <param name="eventIdentifier">Event the client named.</param>
    /// <param name="mode">Lobby mode the caller is entering through.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventSchedule?> FindOpenAsync(
        int eventIdentifier,
        int mode,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0)
        {
            return null;
        }

        var schedule = await FindAsync(eventIdentifier, cancellationToken);
        return IsOpen(schedule, mode, DateTimeOffset.UtcNow) ? schedule : null;
    }

    /// <summary>Finds an event's schedule whatever state it is in.</summary>
    /// <param name="eventIdentifier">Event to look for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<EventSchedule?> FindAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0)
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventSchedules
            .FirstOrDefaultAsync(
                schedule => schedule.Identifier == eventIdentifier,
                cancellationToken);
    }

    /// <summary>
    /// Whether a schedule is one a player may enter through the given lobby
    /// mode at the given moment.
    /// </summary>
    /// <param name="schedule">Schedule to test; null is closed.</param>
    /// <param name="mode">Lobby mode the caller is entering through.</param>
    /// <param name="now">Moment to test at.</param>
    public static bool IsOpen(EventSchedule? schedule, int mode, DateTimeOffset now) =>
        schedule is not null
        && schedule.LobbySubtype == mode
        && IsPublished(schedule, now.ToUnixTimeSeconds());

    /// <summary>
    /// Whether a schedule is published at the given epoch second. A closing
    /// moment of zero means the event never closes, which is how an operator
    /// states an open-ended one.
    /// </summary>
    /// <param name="schedule">Schedule to test.</param>
    /// <param name="nowSeconds">Moment to test at, in epoch seconds.</param>
    public static bool IsPublished(EventSchedule schedule, long nowSeconds) =>
        schedule.Enabled
        && nowSeconds >= schedule.PublishStart
        && (schedule.PublishEnd == 0 || nowSeconds < schedule.PublishEnd);

    /// <summary>
    /// Number of teams a field holds. A row states it in team places, because a
    /// bracket is drawn in teams however many players each brings; the value is
    /// bounded by what the client can render, so a row claiming more is read as
    /// the largest field that can be drawn rather than as a field too large to
    /// show.
    /// </summary>
    /// <param name="schedule">Schedule to read.</param>
    public static int TeamCapacityOf(EventSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return Math.Clamp(schedule.TeamCapacity, 1, EventConstants.BracketMaximumEntrants);
    }
}
