using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Discord;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Endpoints.Authenticated;

/// <summary>
/// The testing endpoints of the authenticated API surface: creating teams and
/// players that exist only in a lobby's memory, and moving an in-memory team
/// through the entry pipeline.
/// <para>
/// They are the HTTP twin of the staff Discord commands and carry the same
/// requests down the same lobby streams. Nothing here writes a row: the team and
/// its players live in the lobby's memory and go away with it, which is what
/// makes them a testing device rather than a second source of teams.
/// </para>
/// <para>
/// A request is a push, so the answer says what was handed to which lobby rather
/// than what the client then rendered; the lobby that was named logs the rest.
/// </para>
/// </summary>
internal static class FakeEventEndpoints
{
    /// <summary>Maps the fake-event endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapFakeEventEndpoints(this RouteGroupBuilder group)
    {
        var fakeTeams = group.MapGroup("/fake-teams")
            .WithTags("Fake teams")
            .RequireAuthorization();

        fakeTeams.MapPost("/", CreateAsync)
            .WithSummary("Create an in-memory team")
            .WithDescription(
                "Asks the running lobby of a mode to create a team that exists only in its memory. " +
                "The team is listed and can be filled, and it is gone when the lobby restarts.");

        fakeTeams.MapPost("/players", AddPlayersAsync)
            .WithSummary("Add fake players to a team")
            .WithDescription(
                "Asks the running lobby of a mode to add players who are not really there to a team " +
                "that already exists, whether it is a real team or an in-memory one.");

        fakeTeams.MapPost("/state", ChangeStateAsync)
            .WithSummary("Change an in-memory team's state")
            .WithDescription(
                "Asks the running lobby of a mode to move one of its in-memory teams to a state, and " +
                "optionally to force a member state on its whole roster. Nothing is written.");
    }

    /// <summary>Relays a request to create an in-memory team.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="request">Team to create.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> CreateAsync(
        FakeTeamDispatchService dispatch,
        FakeTeamCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.DispatchAsync(
            request.Mode,
            request.Count,
            request.TeamName,
            request.PlayerPrefix,
            cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to create an in-memory team."),
                statusCode: StatusCodes.Status202Accepted),
            FakeTeamDispatchService.DispatchOutcome.InvalidCount => Results.BadRequest(
                new FakeEventResult(
                    $"A team holds between 1 and {FakeTeamDispatchService.MaximumCount} players.")),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so no team was created."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>Relays a request to add fake players to an existing team.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="request">Players to add.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> AddPlayersAsync(
        FakePlayerDispatchService dispatch,
        FakePlayerAddRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.DispatchAsync(
            request.Mode,
            request.Count,
            request.TeamName,
            request.PlayerPrefix,
            cancellationToken);

        return result.Outcome switch
        {
            FakePlayerDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to add {request.Count} fake players."),
                statusCode: StatusCodes.Status202Accepted),
            FakePlayerDispatchService.DispatchOutcome.InvalidCount => Results.BadRequest(
                new FakeEventResult(
                    $"A team holds between 1 and {FakePlayerDispatchService.MaximumCount} players.")),
            FakePlayerDispatchService.DispatchOutcome.NoTeamName => Results.BadRequest(
                new FakeEventResult("Name the team the players join.")),
            FakePlayerDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so nothing was added."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>Relays a request to change an in-memory team's state.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="request">State to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> ChangeStateAsync(
        FakeTeamDispatchService dispatch,
        FakeTeamStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.DispatchStateAsync(
            request.Mode,
            request.TeamName,
            request.State,
            request.MemberState,
            cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to set \"{request.TeamName}\" to state {request.State}."),
                statusCode: StatusCodes.Status202Accepted),
            FakeTeamDispatchService.DispatchOutcome.InvalidState => Results.BadRequest(
                new FakeEventResult(
                    $"A state is a byte between 0 and {FakeTeamDispatchService.MaximumStateByte}.")),
            FakeTeamDispatchService.DispatchOutcome.NoTeamName => Results.BadRequest(
                new FakeEventResult("Name the in-memory team whose state changes.")),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so no state was changed."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }
}
