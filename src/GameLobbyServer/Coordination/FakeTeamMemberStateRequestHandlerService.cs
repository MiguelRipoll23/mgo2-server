using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to change the entry-decision byte on the
/// fake players of a real team.
/// <para>
/// The request names a team by its display name and a lobby by its mode, for
/// the reasons every other request in this group does: the coordinator knows
/// neither the identifier a stored team has nor the identifier of the lobby it
/// is in, and a lobby asked for a mode it is not running refuses rather than
/// reaching into another lobby.
/// </para>
/// <para>
/// The change is pushed to the team's sessions afterwards, because a member
/// whose byte moves on the server and not on the wire is a member the client
/// keeps painting with the old value until it re-reads the team. The queue is
/// reconciled too: a team that was waiting and has just become ready is
/// eligible, and one that has just become unready is not.
/// </para>
/// </summary>
/// <param name="memberStateService">Service that changes the fake members of a stored team.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="pushService">Service that tells the team's clients.</param>
/// <param name="matchmakingService">Queue the changed team is reconciled against.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeTeamMemberStateRequestHandlerService(
    FakeTeamMemberStateService memberStateService,
    LobbyIdentityService identityService,
    EventTeamPushService pushService,
    EventMatchmakingService matchmakingService,
    ILogger<FakeTeamMemberStateRequestHandlerService> logger)
{
    /// <summary>Largest value a state byte the client reads can carry.</summary>
    private const int MaximumStateByte = 255;

    /// <summary>Carries out a coordinator's request.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakeTeamMemberStateRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake member state request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.TeamName)
            || request.MemberState is < 0 or > MaximumStateByte)
        {
            logger.LogWarning(
                "A fake member state request was malformed (name, member state {MemberState}); refused",
                request.MemberState);
            return;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no member state was changed");
            return;
        }

        var result = await memberStateService.SetAsync(
            lobbyIdentifier,
            request.TeamName,
            request.MemberState,
            cancellationToken);

        switch (result.Outcome)
        {
            case FakeTeamMemberStateOutcome.TeamNotFound:
                logger.LogInformation(
                    "No team named \"{TeamName}\" is in lobby {LobbyIdentifier}; refused",
                    request.TeamName,
                    lobbyIdentifier);
                return;

            case FakeTeamMemberStateOutcome.NoFakeMembers:
                logger.LogInformation(
                    "Team {TeamIdentifier} holds no fake player whose member state is not already {MemberState}; nothing to change",
                    result.TeamIdentifier,
                    request.MemberState);
                return;
        }

        var snapshot = result.Snapshot!;
        foreach (var slot in result.ChangedSlots)
        {
            await pushService.PushParticipantDecisionAsync(snapshot, slot, cancellationToken);
        }

        logger.LogInformation(
            "Set {Count} fake member(s) of team {TeamIdentifier} ({TeamName}) to member state {MemberState}",
            result.ChangedCount,
            result.TeamIdentifier,
            snapshot.Name,
            request.MemberState);

        // Readiness is what the queue reads, and it is exactly what moved.
        await matchmakingService.ReconcileAsync(result.TeamIdentifier, cancellationToken);
    }
}
