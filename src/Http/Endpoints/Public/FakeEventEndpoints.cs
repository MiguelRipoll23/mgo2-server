using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The testing endpoints of the in-memory teams: creating teams and players
/// that exist only in a lobby's memory, moving such a team through the entry
/// pipeline, and listing and removing the ones a lobby is holding.
/// <para>
/// They are the HTTP twin of what the staff commands used to do, and they carry
/// the same requests down the same lobby streams. Nothing here writes a row: the
/// team and its players live in the lobby's memory and go away with it, which
/// is what makes them a testing device rather than a second source of teams.
/// </para>
/// <para>
/// They are public because they are a testing device and the page that drives
/// them is public: a page that asked for a bearer token would only move the
/// token from the URL bar into a form. What an unauthenticated caller can reach
/// is therefore exactly what a testing device should offer — teams that no
/// client can mistake for real ones, in the lobby the mode names.
/// </para>
/// <para>
/// A request is a push, so the answer says what was handed to which lobby rather
/// than what the client then rendered; the lobby that was named logs the rest.
/// The list is the exception: it asks the lobby and waits for its answer, since
/// the teams are the lobby's own and nothing else can enumerate them.
/// </para>
/// </summary>
internal static class FakeEventEndpoints
{
    /// <summary>Maps the fake-event endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapFakeEventEndpoints(this RouteGroupBuilder group)
    {
        var fakeTeams = group.MapGroup("/fake-teams")
            .WithTags("Fake teams");

        fakeTeams.MapPost("/", CreateAsync)
            .WithSummary("Create an in-memory team")
            .WithDescription(
                "Asks the running lobby of a mode to create a team that exists only in its memory. "
                + "The team is listed and can be filled, and it is gone when the lobby restarts.");

        fakeTeams.MapPost("/players", AddPlayersAsync)
            .WithSummary("Add fake players to a team")
            .WithDescription(
                "Asks the running lobby of a mode to add players who are not really there to a team "
                + "that already exists, whether it is a real team or an in-memory one.");

        fakeTeams.MapPost("/state", ChangeStateAsync)
            .WithSummary("Change an in-memory team's state")
            .WithDescription(
                "Asks the running lobby of a mode to move one of its in-memory teams to a state, and "
                + "optionally to force a member state on its whole roster. Nothing is written.");

        fakeTeams.MapPost("/pairing", PairAsync)
            .WithSummary("Make an in-memory team pairable")
            .WithDescription(
                "Asks the running lobby of a mode to write one of its in-memory teams out as a row and "
                + "queue it, which is what lets a real team be paired against it. This is the only "
                + "fake-team request that writes anything, and the row it writes outlives the match so "
                + "the pairing can be inspected afterwards. Survival only: a Tournament entrant is "
                + "seeded into a bracket and frozen into a roster, which a team that existed only in "
                + "memory has neither of.");

        fakeTeams.MapPost("/member-state", ChangeMemberStateAsync)
            .WithSummary("Set the member state of a stored team's fake players")
            .WithDescription(
                "Asks the running lobby of a mode to set the entry-decision byte on the fake players of "
                + "a team that has a row. Those players are added in whatever state the team's own state "
                + "implies, which for an open team is the one the client paints NG, and nobody else can "
                + "change it: a fake player has no button, and a real player only decides for themselves. "
                + "Only the fake players move — a real member's decision and the team's own state are "
                + "left alone, so the testing device cannot accept an entry on a real player's behalf.");

        fakeTeams.MapPost("/host-room", CreateHostRoomAsync)
            .WithSummary("Create a dedicated event host room")
            .WithDescription(
                "Asks the running lobby of a mode to create the dedicated room its matches are hosted in. "
                + "A pairing is written when two teams are queued but is not announced until a room has "
                + "been leased for it, and a room is only leased from one that is named for the role, "
                + "says it is dedicated and is sitting idle with its host present — all things a real "
                + "host client does merely by existing. Without this, a pairing made from these tools "
                + "waits for a room nobody has opened. Survival and Tournament only.");

        fakeTeams.MapGet("/", ListAsync)
            .WithSummary("List the in-memory teams of a lobby")
            .WithDescription(
                "Asks the running lobby of a mode what teams it holds in its memory, and answers with "
                + "them. The teams live in the lobby's process, so this is the only way to see them.");

        fakeTeams.MapDelete("/{teamName}", RemoveAsync)
            .WithSummary("Remove an in-memory team")
            .WithDescription(
                "Asks the running lobby of a mode to forget one of the teams it holds in its memory, "
                + "which is what a lobby restart would do to it anyway.");
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

    /// <summary>Relays a request to make one in-memory team pairable.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="request">Team to write out.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> PairAsync(
        FakeTeamDispatchService dispatch,
        FakeTeamPromotionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.DispatchPairingAsync(
            request.Mode,
            request.TeamName,
            cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to write \"{request.TeamName}\" out as a team and queue it."),
                statusCode: StatusCodes.Status202Accepted),
            FakeTeamDispatchService.DispatchOutcome.NoTeamName => Results.BadRequest(
                new FakeEventResult("Name the in-memory team to write out.")),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so \"{request.TeamName}\" was not written out."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>Relays a request to change a stored team's fake member state.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="request">Member state to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> ChangeMemberStateAsync(
        FakeTeamDispatchService dispatch,
        FakeTeamMemberStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.DispatchMemberStateAsync(
            request.Mode,
            request.TeamName,
            request.MemberState,
            cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to set the fake players of \"{request.TeamName}\" to member state {request.MemberState}."),
                statusCode: StatusCodes.Status202Accepted),
            FakeTeamDispatchService.DispatchOutcome.InvalidState => Results.BadRequest(
                new FakeEventResult(
                    $"A state is a byte between 0 and {FakeTeamDispatchService.MaximumStateByte}.")),
            FakeTeamDispatchService.DispatchOutcome.NoTeamName => Results.BadRequest(
                new FakeEventResult("Name the team whose fake players change.")),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so no member state was changed."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>Relays a request to create a dedicated event host room.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="request">Room to create.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> CreateHostRoomAsync(
        FakeTeamDispatchService dispatch,
        FakeHostRoomCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.DispatchHostRoomAsync(
            request.Mode,
            request.HostCharacterIdentifier,
            cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to create a dedicated host room."),
                statusCode: StatusCodes.Status202Accepted),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so no host room was created."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>Asks a lobby what in-memory teams it is holding.</summary>
    /// <param name="dispatch">Service the question is carried through.</param>
    /// <param name="mode">Lobby mode to ask about.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> ListAsync(
        FakeTeamDispatchService dispatch,
        int mode,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.ListAsync(mode, cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Ok(
                new FakeTeamListingResult(mode, [.. result.Teams.Select(FakeTeamEntry.Of)])),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(mode)} lobby running.")),
            FakeTeamDispatchService.DispatchOutcome.LobbyOffline => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(mode)} lobby is not connected, so no teams could be listed."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(mode)} lobby did not answer, so no teams could be listed."),
                statusCode: StatusCodes.Status504GatewayTimeout),
        };
    }

    /// <summary>Asks a lobby to forget one of the teams it holds in memory.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="teamName">Name of the in-memory team to remove.</param>
    /// <param name="mode">Lobby mode the team is in.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> RemoveAsync(
        FakeTeamDispatchService dispatch,
        string teamName,
        int mode,
        CancellationToken cancellationToken)
    {
        var result = await dispatch.RemoveAsync(mode, teamName, cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent when result.Teams.Count == 0 => Results.NotFound(
                new FakeEventResult(
                    $"No in-memory team called \"{teamName}\" is in the {EventScheduleService.ModeName(mode)} lobby.")),
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked the {EventScheduleService.ModeName(mode)} lobby to forget \"{teamName}\".")),
            FakeTeamDispatchService.DispatchOutcome.NoTeamName => Results.BadRequest(
                new FakeEventResult("Name the in-memory team to remove.")),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(mode)} lobby running.")),
            FakeTeamDispatchService.DispatchOutcome.LobbyOffline => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(mode)} lobby is not connected, so nothing was removed."),
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(mode)} lobby did not answer, so nothing was removed."),
                statusCode: StatusCodes.Status504GatewayTimeout),
        };
    }
}
