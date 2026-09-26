using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to add fake players to a team in this
/// lobby, and tells the team's clients about every slot it filled.
/// <para>
/// The request names a lobby mode rather than an identifier because the
/// coordinator does not know this lobby's identifier. A lobby asked for a mode
/// it is not running refuses the request rather than filling the wrong event: a
/// fake player in the wrong lobby is a player a real client will meet in a
/// bracket it does not belong to.
/// </para>
/// <para>
/// The team is the one the request names, whether it is a real row a player
/// formed or an in-memory team a fake-team request created. The players go in
/// already ready, because there is nobody to press the decision button — and the
/// roster is pushed to the team's sessions afterwards, because a client that is
/// holding the team open otherwise shows the roster it cached, which is the
/// leader and no players.
/// </para>
/// </summary>
/// <param name="fakeTeamService">Service that holds the fake teams and players.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="pushService">Service that tells the team's clients.</param>
/// <param name="matchmakingService">Service that re-queues a real team that changed.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakePlayerRequestHandlerService(
    FakeTeamService fakeTeamService,
    LobbyIdentityService identityService,
    EventTeamPushService pushService,
    EventMatchmakingService matchmakingService,
    ILogger<FakePlayerRequestHandlerService> logger)
{
    /// <summary>Adds the players a coordinator request asked for.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakePlayerRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake player request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        if (request.Count < 1 || request.Count > FakeTeamService.MaximumPerTeam)
        {
            logger.LogWarning(
                "A fake player request asked for {Count} players, which is outside 1..{Maximum}; refused",
                request.Count,
                FakeTeamService.MaximumPerTeam);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.TeamName))
        {
            logger.LogWarning(
                "A fake player request named no team, so there was nothing to fill; refused");
            return;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no fake players were created");
            return;
        }

        var result = await fakeTeamService.FillTeamAsync(
            lobbyIdentifier,
            request.TeamName,
            request.Count,
            request.PlayerPrefix,
            cancellationToken);

        switch (result.Outcome)
        {
            case FakeTeamFillOutcome.TeamNotFound:
                logger.LogInformation(
                    "No team named \"{TeamName}\" is in lobby {LobbyIdentifier}; refused",
                    request.TeamName,
                    lobbyIdentifier);
                return;

            case FakeTeamFillOutcome.TeamFull:
                logger.LogInformation(
                    "Team {TeamIdentifier} has no free slot; refused",
                    result.TeamIdentifier);
                return;
        }

        // The push is what makes the roster appear: a client holding the team
        // open is told about each filled slot rather than left with the cached
        // leader, and the team's own members are the recipients.
        var snapshot = result.Snapshot!;
        foreach (var slot in result.AddedSlots)
        {
            await pushService.PushParticipantAddedAsync(
                snapshot,
                slot,
                excludedSession: null,
                cancellationToken);
        }

        logger.LogInformation(
            "Added {Count} fake players to team {TeamName} ({TeamIdentifier}) in lobby {LobbyIdentifier}",
            result.AddedSlots.Count,
            snapshot.Name,
            result.TeamIdentifier,
            lobbyIdentifier);

        if (!result.InMemory)
        {
            // A real team's roster changed, so a team that is already queued is
            // re-checked: the new members are ready, and a team that was not
            // eligible may now be.
            await matchmakingService.ReconcileAsync(result.TeamIdentifier, cancellationToken);
        }
    }
}
