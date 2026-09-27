using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Answers the coordinator's question about the testing teams this lobby holds.
/// <para>
/// Every other request on the stream is a push: the API hands the lobby
/// something to do and the lobby logs what it did. A question is the other
/// shape, so the answer is built here and sent back up the same stream the
/// question arrived on, carrying the correlation the API is waiting on.
/// </para>
/// <para>
/// The teams are real rows now, so removing one is a delete rather than a
/// forgetting. The lobby is still asked rather than the database written
/// directly, because the lobby is where the request is already routed and its
/// answer carries the correlation the caller waits on.
/// </para>
/// </summary>
/// <param name="fakeTeamService">Service that owns the stored teams.</param>
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

        // The only question left is the removal: the listing moved to the rows,
        // which the API reads itself and never has to ask a lobby for.
        IReadOnlyList<FakeTeamRecord> teams = request.Action switch
        {
            FakeTeamQueryAction.Remove => await RemoveOneAsync(lobbyIdentifier, request.TeamName, cancellationToken),
            _ => [],
        };

        logger.LogInformation(
            "Answered a {Action} team query for lobby {LobbyIdentifier} with {Count} team(s)",
            request.Action,
            lobbyIdentifier,
            teams.Count);

        await AnswerAsync(request, Summarize(teams), cancellationToken);
    }

    /// <summary>Removes the team the question named, when this lobby holds it.</summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="teamName">Name the question named.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The team that was removed, or nothing.</returns>
    private async Task<IReadOnlyList<FakeTeamRecord>> RemoveOneAsync(
        int lobbyIdentifier,
        string teamName,
        CancellationToken cancellationToken)
    {
        var removed = await fakeTeamService.RemoveTeamAsync(lobbyIdentifier, teamName, cancellationToken);
        if (removed is null)
        {
            logger.LogInformation(
                "No team named \"{TeamName}\" is in lobby {LobbyIdentifier}; nothing was removed",
                teamName,
                lobbyIdentifier);
            return [];
        }

        return [removed.Value];
    }

    /// <summary>Projects the teams into the shape the coordinator reads.</summary>
    /// <param name="teams">Teams this lobby holds.</param>
    /// <returns>The summary of each, in the order they are held.</returns>
    private static List<FakeTeamSummary> Summarize(IReadOnlyList<FakeTeamRecord> teams) =>
        [.. teams.Select(team => new FakeTeamSummary
        {
            TeamIdentifier = team.Identifier,
            TeamName = team.Name,
            State = team.State,
            MemberCount = team.MemberCount,
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
