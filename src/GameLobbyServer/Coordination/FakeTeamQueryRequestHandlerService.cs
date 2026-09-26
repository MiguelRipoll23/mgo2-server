using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Answers the coordinator's question about the teams this lobby holds only in
/// its memory.
/// <para>
/// Every other request on the stream is a push: the API hands the lobby
/// something to do and the lobby logs what it did. A question is the other
/// shape, so the answer is built here and sent back up the same stream the
/// question arrived on, carrying the correlation the API is waiting on.
/// </para>
/// <para>
/// The lobby is named by its mode rather than by its identifier because the
/// coordinator does not know this lobby's identifier, and one asked for a mode
/// it is not running answers with nothing at all: reporting the teams of a
/// lobby the caller did not ask about would be a list of another lobby's
/// teams.
/// </para>
/// <para>
/// Nothing is written. Removing a team is the same forgetting a restart does,
/// which is what makes the in-memory teams a testing device rather than a
/// second source of teams.
/// </para>
/// </summary>
/// <param name="fakeTeamService">Service that holds the fake teams.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="outgoingEvents">Queue the answer is written to.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeTeamQueryRequestHandlerService(
    FakeTeamService fakeTeamService,
    LobbyIdentityService identityService,
    LobbyEventQueueService outgoingEvents,
    ILogger<FakeTeamQueryRequestHandlerService> logger)
{
    /// <summary>Answers one question the coordinator asked.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakeTeamQueryRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake team query named mode {Mode} and this lobby is {LobbySubtype}; answered with nothing",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            await AnswerAsync(request, [], cancellationToken);
            return;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; the team query was answered with nothing");
            await AnswerAsync(request, [], cancellationToken);
            return;
        }

        var teams = request.Action switch
        {
            FakeTeamQueryAction.List => Summarize(fakeTeamService.ListTeams(lobbyIdentifier)),
            FakeTeamQueryAction.Remove => RemoveOne(lobbyIdentifier, request.TeamName),
            _ => [],
        };

        logger.LogInformation(
            "Answered a {Action} team query for lobby {LobbyIdentifier} with {Count} team(s)",
            request.Action,
            lobbyIdentifier,
            teams.Count);

        await AnswerAsync(request, teams, cancellationToken);
    }

    /// <summary>Removes the team the question named, when this lobby holds it.</summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name the question named.</param>
    /// <returns>The team that was removed, or nothing.</returns>
    private List<FakeTeamSummary> RemoveOne(int lobbyIdentifier, string teamName)
    {
        var removed = fakeTeamService.RemoveTeam(lobbyIdentifier, teamName);
        if (removed is null)
        {
            logger.LogInformation(
                "No in-memory team named \"{TeamName}\" is in lobby {LobbyIdentifier}; nothing was removed",
                teamName,
                lobbyIdentifier);
            return [];
        }

        return Summarize([removed]);
    }

    /// <summary>Projects the teams into the shape the coordinator reads.</summary>
    /// <param name="teams">Teams this lobby holds.</param>
    /// <returns>The summary of each, in the order they are held.</returns>
    private static List<FakeTeamSummary> Summarize(IReadOnlyList<FakeTeam> teams) =>
        [.. teams.Select(team => new FakeTeamSummary
        {
            TeamIdentifier = team.Identifier,
            TeamName = team.Name,
            State = team.State,
            MemberCount = team.Members.Count,
            EventIdentifier = team.EventIdentifier,
        })];

    /// <summary>Queues the answer for the coordinator.</summary>
    /// <param name="request">Question being answered.</param>
    /// <param name="teams">Teams the answer carries.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private Task AnswerAsync(
        FakeTeamQueryRequest request,
        IReadOnlyList<FakeTeamSummary> teams,
        CancellationToken cancellationToken)
    {
        var listing = new FakeTeamListing
        {
            RequestIdentifier = request.RequestIdentifier,
        };
        listing.Teams.AddRange(teams);

        // The answer is dropped rather than queued for a stream that is not
        // open: a coordinator that asked a question it is no longer waiting for
        // has already moved on, and the queue is bounded for the same reason
        // the presence events in it are.
        outgoingEvents.TryEnqueue(new LobbyEvent { FakeTeamListing = listing });
        return Task.CompletedTask;
    }
}
