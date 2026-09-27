using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The testing endpoints of the event teams: creating teams and players drawn
/// from the test-character pool, moving a team through the entry pipeline, and
/// removing one a lobby is holding.
/// <para>
/// They are the HTTP twin of what the staff commands used to do, and they carry
/// the same requests down the same lobby streams. What they create are ordinary
/// rows whose players are marked test characters: there is no second store, so
/// every reader sees one kind of data and cannot tell a testing team from a
/// player's own.
/// </para>
/// <para>
/// They are public because they are a testing device and the page that drives
/// them is public: a page that asked for a bearer token would only move the
/// token from the URL bar into a form. What an unauthenticated caller can reach
/// is therefore exactly what a testing device should offer — teams whose
/// players are named so nobody mistakes them for real ones, in the lobby the
/// mode names.
/// </para>
/// <para>
/// A request is a push, so the answer says what was handed to which lobby rather
/// than what the client then rendered; the lobby that was named logs the rest.
/// The removal is the exception: it asks the lobby and waits for its answer,
/// since the team is the lobby's own and the caller is shown what it removed.
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
            .WithSummary("Create a testing team")
            .WithDescription(
                "Asks the running lobby of a mode to create a team whose players are taken from the "
                + "test-character pool. The team is an ordinary row and can be filled and paired.");

        fakeTeams.MapPost("/players", AddPlayersAsync)
            .WithSummary("Add test players to a team")
            .WithDescription(
                "Asks the running lobby of a mode to add players from the test-character pool to a "
                + "team that already exists, whether a player formed it or the tools created it.");

        fakeTeams.MapPost("/state", ChangeStateAsync)
            .WithSummary("Change a team's state")
            .WithDescription(
                "Asks the running lobby of a mode to move one of its teams to a state, and optionally "
                + "to force a member state on its whole roster.");

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

        fakeTeams.MapDelete("/{teamName}", RemoveAsync)
            .WithSummary("Remove a team")
            .WithDescription(
                "Asks the running lobby of a mode to remove one of the teams it holds. The team is a "
                + "row, so this is a deletion and its roster goes with it.");

        // The pool is global: test characters are rows in the characters table,
        // owned by the server account, so the API lists and changes them without
        // asking any lobby. The team requests reach a lobby because a team lives
        // in one; these do not.
        var pool = fakeTeams.MapGroup("/characters");
        pool.MapGet("/", ListCharactersAsync)
            .WithSummary("List the test characters")
            .WithDescription(
                "Lists the characters the testing tools create, with whether a team already holds each. "
                + "These are the characters the create-team and add-players actions draw on.");
        pool.MapPost("/", AddCharactersAsync)
            .WithSummary("Add test characters")
            .WithDescription(
                "Creates characters named with the server- prefix under the server account, and returns "
                + "the pool as it stands afterwards.");
        pool.MapDelete("/{characterIdentifier:int}", DeleteCharacterAsync)
            .WithSummary("Delete a test character")
            .WithDescription(
                "Removes one test character and any roster row that names it.");
    }

    /// <summary>Relays a request to create a testing team.</summary>
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
            cancellationToken);

        return result.Outcome switch
        {
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to create a testing team."),
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

    /// <summary>Relays a request to add test players to an existing team.</summary>
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
            cancellationToken);

        return result.Outcome switch
        {
            FakePlayerDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked {EventScheduleService.ModeName(request.Mode)} to add {request.Count} test players."),
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

    /// <summary>Relays a request to change a team's state.</summary>
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
                new FakeEventResult("Name the team whose state changes.")),
            FakeTeamDispatchService.DispatchOutcome.NoSuchLobby => Results.NotFound(
                new FakeEventResult($"There is no {EventScheduleService.ModeName(request.Mode)} lobby running.")),
            _ => Results.Json(
                new FakeEventResult(
                    $"The {EventScheduleService.ModeName(request.Mode)} lobby is not connected, so no state was changed."),
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

    /// <summary>Asks a lobby to remove one of the teams it holds.</summary>
    /// <param name="dispatch">Service the request is carried through.</param>
    /// <param name="teamName">Name of the team to remove.</param>
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
                    $"No team called \"{teamName}\" is in the {EventScheduleService.ModeName(mode)} lobby.")),
            FakeTeamDispatchService.DispatchOutcome.Sent => Results.Json(
                new FakeEventResult(
                    $"Asked the {EventScheduleService.ModeName(mode)} lobby to remove \"{teamName}\".")),
            FakeTeamDispatchService.DispatchOutcome.NoTeamName => Results.BadRequest(
                new FakeEventResult("Name the team to remove.")),
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

    /// <summary>Lists the test characters the server is holding.</summary>
    /// <param name="pool">The pool to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> ListCharactersAsync(
        TestCharacterPoolService pool,
        CancellationToken cancellationToken)
    {
        var entries = await pool.ListAsync(cancellationToken);
        return Results.Ok(new TestCharacterPoolResult([.. entries.Select(TestCharacterEntry.Of)]));
    }

    /// <summary>Adds test characters to the pool.</summary>
    /// <param name="pool">The pool to add to.</param>
    /// <param name="request">How many to add.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> AddCharactersAsync(
        TestCharacterPoolService pool,
        TestCharacterAddRequest request,
        CancellationToken cancellationToken)
    {
        var added = await pool.AddAsync(request.Count, cancellationToken);
        if (added == 0)
        {
            return Results.Json(
                new FakeEventResult(
                    "The server account is missing, so no test characters could be added. "
                    + "Start the gameplay server once so it creates its account."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var entries = await pool.ListAsync(cancellationToken);
        return Results.Ok(new TestCharacterPoolResult([.. entries.Select(TestCharacterEntry.Of)]));
    }

    /// <summary>Removes one test character from the pool.</summary>
    /// <param name="pool">The pool to remove from.</param>
    /// <param name="characterIdentifier">Character to remove.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> DeleteCharacterAsync(
        TestCharacterPoolService pool,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        var removed = await pool.DeleteAsync(characterIdentifier, cancellationToken);
        return removed
            ? Results.Ok(new FakeEventResult($"Removed test character {characterIdentifier}."))
            : Results.NotFound(
                new FakeEventResult("No test character of that identifier is in the pool."));
    }
}
