using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Endpoints.Authenticated;

/// <summary>
/// The event schedule endpoints of the authenticated API surface.
/// <para>
/// A schedule is what makes an event enterable, so this is the surface that
/// creates them. Without it the only way to publish an event is to write a row
/// by hand, which is how an event ends up in the schedules table with a window
/// nobody chose.
/// </para>
/// </summary>
internal static class EventScheduleEndpoints
{
    /// <summary>Maps the event schedule endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapEventScheduleEndpoints(this RouteGroupBuilder group)
    {
        var schedules = group.MapGroup("/event-schedules")
            .WithTags("Event schedules")
            .RequireAuthorization();

        schedules.MapGet("/", ListAsync)
            .WithSummary("List event schedules")
            .WithDescription("Returns every scheduled event, published or not, oldest identifier first");

        schedules.MapGet("/{id:int}", GetAsync)
            .WithSummary("Get an event schedule")
            .WithDescription("Returns a single event schedule by its numeric identifier");

        schedules.MapGet("/published/{lobbySubtypeId:int}", ListPublishedAsync)
            .WithSummary("List the events a lobby is publishing")
            .WithDescription("Returns the events currently published in one lobby mode, which is what its list shows");

        schedules.MapPost("/", CreateAsync)
            .WithSummary("Schedule an event")
            .WithDescription("Publishes a new event and returns it, with the identifier the database assigned");

        schedules.MapPut("/{id:int}", UpdateAsync)
            .WithSummary("Change an event schedule")
            .WithDescription("Replaces the publication window and field size of a scheduled event");

        schedules.MapDelete("/{id:int}", DeleteAsync)
            .WithSummary("Withdraw an event schedule")
            .WithDescription("Removes a schedule, which closes the event without altering a bracket already drawn");
    }

    private static async Task<IResult> ListAsync(
        EventScheduleService scheduleService,
        CancellationToken cancellationToken)
    {
        var schedules = await scheduleService.ListAllAsync(cancellationToken);
        return Results.Ok(schedules.Select(schedule => schedule.ToContract()));
    }

    private static async Task<IResult> GetAsync(
        EventScheduleService scheduleService,
        int id,
        CancellationToken cancellationToken)
    {
        var schedule = await scheduleService.FindAsync(id, cancellationToken);
        return schedule is null
            ? Results.NotFound()
            : Results.Ok(schedule.ToContract());
    }

    private static async Task<IResult> ListPublishedAsync(
        EventScheduleService scheduleService,
        int lobbySubtypeId,
        CancellationToken cancellationToken)
    {
        if (!EventConstants.IsEventSelector(lobbySubtypeId))
        {
            return Results.BadRequest();
        }

        var schedules = await scheduleService.ListPublishedAsync(lobbySubtypeId, cancellationToken);
        return Results.Ok(schedules.Select(schedule => schedule.ToContract()));
    }

    private static async Task<IResult> CreateAsync(
        EventScheduleService scheduleService,
        EventScheduleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await scheduleService.ScheduleAsync(
                request.LobbySubtypeId,
                request.TeamCapacity,
                request.PublishStart,
                request.PublishEnd,
                request.Enabled,
                request.Name,
                cancellationToken);
            return Results.Json(schedule.ToContract(), statusCode: StatusCodes.Status201Created);
        }
        catch (ArgumentException)
        {
            // The window is stated by the caller and the service refuses the ones
            // that cannot be entered, which is a bad request rather than a fault.
            return Results.BadRequest();
        }
    }

    private static async Task<IResult> UpdateAsync(
        EventScheduleService scheduleService,
        int id,
        EventSchedulePatchRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await scheduleService.UpdateAsync(
            id,
            request.Enabled,
            request.PublishStart,
            request.PublishEnd,
            request.TeamCapacity,
            cancellationToken);

        return outcome switch
        {
            ScheduleWriteOutcome.Written => Results.Ok(
                (await scheduleService.FindAsync(id, cancellationToken))!.ToContract()),
            ScheduleWriteOutcome.NotFound => Results.NotFound(),
            _ => Results.BadRequest(),
        };
    }

    private static async Task<IResult> DeleteAsync(
        EventScheduleService scheduleService,
        int id,
        CancellationToken cancellationToken) =>
        await scheduleService.WithdrawAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
}
