using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to make one in-memory team pairable.
/// <para>
/// The request names a team by its display name because a team that exists only
/// in memory has no row and no identifier the coordinator could know. It names a
/// lobby mode rather than an identifier for the same reason the other requests
/// do, and a lobby asked for a mode it is not running refuses rather than
/// touching a team in another lobby.
/// </para>
/// <para>
/// This is the only request on the stream that writes a row, and the pairing
/// service is what explains why: a pairing is a durable row naming two teams,
/// and everything downstream of it re-reads both sides as rows. A memory-only
/// team cannot be one half of that. Once written out the team is an ordinary
/// team, so the queue is asked to reconcile it rather than being taught about
/// memory — the eligibility rules that decide whether it may be paired are the
/// same ones a real team goes through.
/// </para>
/// </summary>
/// <param name="pairingService">Service that writes the team out as a row.</param>
/// <param name="matchmakingService">Queue the written-out team is reconciled against.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeTeamPairingRequestHandlerService(
    FakeTeamPairingService pairingService,
    EventMatchmakingService matchmakingService,
    LobbyIdentityService identityService,
    ILogger<FakeTeamPairingRequestHandlerService> logger)
{
    /// <summary>Carries out a coordinator's request.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakeTeamPairingRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake team pairing request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.TeamName))
        {
            logger.LogWarning("A fake team pairing request named no team; refused");
            return;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no team was made pairable");
            return;
        }

        var result = await pairingService.MaterialiseAsync(
            lobbyIdentifier,
            request.TeamName,
            mode.Value,
            cancellationToken);

        if (result.Outcome != FakeTeamPairingOutcome.Materialised)
        {
            logger.LogInformation(
                "In-memory team \"{TeamName}\" was not made pairable in lobby {LobbyIdentifier}: {Outcome}",
                request.TeamName,
                lobbyIdentifier,
                result.Outcome);
            return;
        }

        // The team is a row now, so it is queued by the same path a real team
        // is: reconciling it runs the readiness and match-type rules and puts
        // it in the queue, where a waiting real team becomes its opponent.
        var matchmaking = await matchmakingService.ReconcileAsync(
            result.TeamIdentifier,
            cancellationToken);

        logger.LogInformation(
            "Wrote in-memory team {TeamName} out as team {TeamIdentifier} and queued it ({Status})",
            result.TeamName,
            result.TeamIdentifier,
            matchmaking.Status);
    }
}
