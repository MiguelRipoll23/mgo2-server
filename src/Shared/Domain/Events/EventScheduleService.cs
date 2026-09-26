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
/// <param name="nameService">Service that owns the name a schedule is addressed by.</param>
public sealed class EventScheduleService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    EventScheduleNameService nameService)
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
    /// Finds an event's schedule by the name an operator gave it.
    /// </summary>
    /// <param name="name">Name the event was given.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task<EventSchedule?> FindByNameAsync(
        string? name,
        CancellationToken cancellationToken = default) =>
        nameService.FindAsync(name, cancellationToken);

    /// <summary>
    /// Lists the events a lobby is currently publishing, oldest identifier
    /// first, so the order a client is shown them is the order they were
    /// scheduled in rather than the order rows happened to be read.
    /// </summary>
    /// <param name="mode">Lobby mode to list.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventSchedule>> ListPublishedAsync(
        int mode,
        CancellationToken cancellationToken = default)
    {
        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var context = await CreateContextAsync(cancellationToken);
        var schedules = await context.EventSchedules
            .Where(schedule => schedule.LobbySubtype == mode)
            .OrderBy(schedule => schedule.Identifier)
            .ToListAsync(cancellationToken);

        return
        [
            .. schedules.Where(schedule => IsPublished(schedule, nowSeconds)),
        ];
    }

    /// <summary>
    /// Lists every scheduled event, published or not. This is the operator's
    /// view: an event that has closed or was unpublished is still a schedule
    /// somebody wrote, and hiding it would make it impossible to reopen.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<EventSchedule>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.EventSchedules
            .OrderBy(schedule => schedule.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Schedules a new event. The identifier is chosen by the database, because
    /// it is the value a client will name and a hand-issued one is a collision
    /// waiting to happen.
    /// </summary>
    /// <param name="mode">Lobby mode the event is played in.</param>
    /// <param name="teamCapacity">Number of teams its field holds.</param>
    /// <param name="publishStart">Epoch second it is published from.</param>
    /// <param name="publishEnd">Epoch second it stops being published, or zero.</param>
    /// <param name="enabled">Whether it is published at all.</param>
    /// <param name="name">Name to address the event by, or blank to compose one.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The schedule that was written.</returns>
    public async Task<EventSchedule> ScheduleAsync(
        int mode,
        int teamCapacity,
        long publishStart,
        long publishEnd,
        bool enabled,
        string name = "",
        CancellationToken cancellationToken = default)
    {
        if (!EventConstants.IsEventSelector(mode))
        {
            // Only the three event lobbies hold an event, so a mode outside them
            // is not one an event can be played in.
            throw new ArgumentException(
                $"Lobby mode {mode} does not hold events.",
                nameof(mode));
        }

        if (publishStart < 0 || publishEnd < 0)
        {
            throw new ArgumentException("A schedule window cannot be negative.", nameof(publishStart));
        }

        if (!IsWindowEnterable(publishStart, publishEnd))
        {
            throw new ArgumentException(
                "A schedule that closes must close after it opens.",
                nameof(publishEnd));
        }

        var eventName = EventScheduleNameService.Normalise(name);
        if (eventName.Length == 0)
        {
            eventName = await nameService.ComposeAsync(mode, cancellationToken);
        }

        if (await nameService.FindAsync(eventName, cancellationToken) is not null)
        {
            // Two events sharing a name would make the name address two of them,
            // so this is refused here rather than caught as a constraint later.
            throw new ArgumentException(
                $"An event named '{eventName}' already exists.",
                nameof(name));
        }

        var schedule = new EventSchedule
        {
            Name = eventName,
            LobbySubtype = mode,
            Enabled = enabled,
            PublishStart = publishStart,
            PublishEnd = publishEnd,
            TeamCapacity = Math.Clamp(teamCapacity, 1, EventConstants.BracketMaximumEntrants),
        };

        await using var context = await CreateContextAsync(cancellationToken);
        context.EventSchedules.Add(schedule);
        await context.SaveChangesAsync(cancellationToken);
        return schedule;
    }

    /// <summary>Name a lobby mode is written with in a schedule name.</summary>
    /// <param name="mode">Lobby mode to name.</param>
    public static string ModeName(int mode) => EventScheduleNameService.ModeName(mode);

    /// <summary>
    /// Changes a schedule in place. Every column is written, because the caller
    /// states the whole window rather than a difference: a schedule is a
    /// statement of when an event runs, and a partial one is a schedule nobody
    /// can read.
    /// </summary>
    /// <param name="eventIdentifier">Event to change.</param>
    /// <param name="enabled">Whether it is published at all.</param>
    /// <param name="publishStart">Epoch second it is published from.</param>
    /// <param name="publishEnd">Epoch second it stops being published, or zero.</param>
    /// <param name="teamCapacity">Number of teams its field holds.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>What happened.</returns>
    public async Task<ScheduleWriteOutcome> UpdateAsync(
        int eventIdentifier,
        bool enabled,
        long publishStart,
        long publishEnd,
        int teamCapacity,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0)
        {
            return ScheduleWriteOutcome.NotFound;
        }

        if (publishStart < 0 || publishEnd < 0
            || !IsWindowEnterable(publishStart, publishEnd))
        {
            return ScheduleWriteOutcome.InvalidWindow;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var schedule = await context.EventSchedules
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == eventIdentifier,
                cancellationToken);
        if (schedule is null)
        {
            return ScheduleWriteOutcome.NotFound;
        }

        schedule.Enabled = enabled;
        schedule.PublishStart = publishStart;
        schedule.PublishEnd = publishEnd;
        schedule.TeamCapacity = Math.Clamp(teamCapacity, 1, EventConstants.BracketMaximumEntrants);
        await context.SaveChangesAsync(cancellationToken);
        return ScheduleWriteOutcome.Written;
    }

    /// <summary>
    /// Withdraws a schedule. An event that has already been entered is not
    /// refused here: the window that decides whether it is still live is the
    /// one this closes, and a bracket already drawn keeps its seeds whatever
    /// happens to the row.
    /// </summary>
    /// <param name="eventIdentifier">Event to withdraw.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether a schedule was withdrawn.</returns>
    public async Task<bool> WithdrawAsync(
        int eventIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (eventIdentifier <= 0)
        {
            return false;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var schedule = await context.EventSchedules
            .FirstOrDefaultAsync(
                candidate => candidate.Identifier == eventIdentifier,
                cancellationToken);
        if (schedule is null)
        {
            return false;
        }

        context.EventSchedules.Remove(schedule);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Whether a stated window can ever be entered.
    /// <para>
    /// A closing moment of zero is the way an operator states an open-ended
    /// event, so it is a window rather than a missing one. Anything else has to
    /// close strictly after it opens: a window that closes at the moment it
    /// opens contains no moment at all, which is a mistake rather than a
    /// schedule.
    /// </para>
    /// </summary>
    /// <param name="publishStart">Epoch second the window opens.</param>
    /// <param name="publishEnd">Epoch second it closes, or zero for never.</param>
    public static bool IsWindowEnterable(long publishStart, long publishEnd) =>
        publishStart >= 0
        && publishEnd >= 0
        && (publishEnd == 0 || publishEnd > publishStart);

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
