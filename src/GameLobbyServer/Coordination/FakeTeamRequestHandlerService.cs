using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to create a team that exists only in this
/// lobby's memory.
/// <para>
/// The request names a lobby mode rather than an identifier because the
/// coordinator does not know this lobby's identifier. A lobby asked for a mode
/// it is not running refuses the request rather than creating the team
/// elsewhere: a team in the wrong lobby is one a client will meet in a bracket
/// it does not belong to.
/// </para>
/// <para>
/// Nothing is written. The team is held so the lobby's own team-list and
/// event-list replies can show it, and it is gone the moment the process ends,
/// which is what makes it a testing device rather than a second source of
/// teams.
/// </para>
/// </summary>
/// <param name="fakeTeamService">Service that holds the fake teams.</param>
/// <param name="identityService">Service that knows this lobby's own mode and row.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakeTeamRequestHandlerService(
    FakeTeamService fakeTeamService,
    LobbyIdentityService identityService,
    EventScheduleService scheduleService,
    ILogger<FakeTeamRequestHandlerService> logger)
{
    /// <summary>Creates the team a coordinator request asked for.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakeTeamRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await identityService.ResolveModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake team request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        if (request.Count < 1 || request.Count > FakeTeamService.MaximumPerTeam)
        {
            logger.LogWarning(
                "A fake team request asked for {Count} players, which is outside 1..{Maximum}; refused",
                request.Count,
                FakeTeamService.MaximumPerTeam);
            return;
        }

        // The team is filed under the event the lobby publishes. Survival and
        // Tournament carry one standing event, so the transient correlation is
        // what the list screens read. A registration lobby's teams belong to a
        // scheduled event, and the request selected none, so the first event the
        // lobby is currently publishing is used: a team the bracket never knew
        // about would not appear next to the entrants it is meant to be tested
        // with. With nothing published there is no event to file it under, so
        // the request is refused.
        if (!EventTeamCreationUtils.TryResolveEventIdentifier(
                mode.Value,
                selectedEventIdentifier: null,
                out var eventIdentifier))
        {
            var published = await scheduleService.ListPublishedAsync(mode.Value, cancellationToken);
            if (published.Count == 0)
            {
                logger.LogInformation(
                    "A fake team request named a lobby with no published event to file a team under; refused");
                return;
            }

            eventIdentifier = published[0].Identifier;
        }

        var lobbyIdentifier = await identityService.ResolveIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no fake team was created");
            return;
        }

        var team = fakeTeamService.CreateTeam(
            mode.Value,
            lobbyIdentifier,
            eventIdentifier,
            request.TeamName,
            request.PlayerPrefix,
            request.Count);

        if (team is null)
        {
            logger.LogWarning("A fake team request could not be created; refused");
            return;
        }

        logger.LogInformation(
            "Created in-memory team {TeamName} ({TeamIdentifier}) with {Count} players for event {EventIdentifier} in lobby {LobbyIdentifier}",
            team.Name,
            team.Identifier,
            team.Members.Count,
            eventIdentifier,
            lobbyIdentifier);
    }
}
