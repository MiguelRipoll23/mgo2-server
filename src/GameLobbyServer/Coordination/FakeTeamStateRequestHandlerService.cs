using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to change the state of a team that exists
/// only in this lobby's memory.
/// <para>
/// The request names the team by its display name because an in-memory team has
/// no row and no identifier the coordinator could know. It names a lobby mode
/// rather than an identifier for the same reason the create request does, and a
/// lobby asked for a mode it is not running refuses rather than changing a team
/// in another lobby.
/// </para>
/// <para>
/// Nothing is written. The change is held so the lobby's own list and detail
/// replies show it, and it is gone the moment the process ends.
/// </para>
/// </summary>
/// <param name="fakeTeamService">Service that holds the fake teams.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeTeamStateRequestHandlerService(
    FakeTeamService fakeTeamService,
    LobbyIdentityService identityService,
    ILogger<FakeTeamStateRequestHandlerService> logger)
{
    /// <summary>Largest value a state byte the client reads can carry.</summary>
    private const int MaximumStateByte = 255;

    /// <summary>Changes the state a coordinator request asked for.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakeTeamStateRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake team state request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.TeamName)
            || request.State is < 0 or > MaximumStateByte
            || request.MemberState is < 0 or > MaximumStateByte)
        {
            logger.LogWarning(
                "A fake team state request was malformed (name, state {State}, member state {MemberState}); refused",
                request.State,
                request.MemberState);
            return;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no fake team state was changed");
            return;
        }

        var team = fakeTeamService.SetState(
            lobbyIdentifier,
            request.TeamName,
            request.State,
            request.MemberState > 0 ? request.MemberState : null);

        if (team is null)
        {
            logger.LogInformation(
                "No in-memory team named \"{TeamName}\" is in lobby {LobbyIdentifier}; refused",
                request.TeamName,
                lobbyIdentifier);
            return;
        }

        logger.LogInformation(
            "Set in-memory team {TeamName} ({TeamIdentifier}) to state {State} with member state {MemberState}",
            team.Name,
            team.Identifier,
            team.State,
            request.MemberState);
    }
}
