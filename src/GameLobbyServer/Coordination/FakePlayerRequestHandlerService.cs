using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on the coordinator's request to fill this lobby with fake players.
/// <para>
/// The request names a lobby mode rather than an identifier because the
/// coordinator does not know this lobby's identifier. A lobby asked for a mode
/// it is not running refuses the request rather than filling the wrong event: a
/// fake player in the wrong lobby is a player a real client will meet in a
/// bracket it does not belong to.
/// </para>
/// <para>
/// The players are created already entered into the open event, because there is
/// nobody to press the button that enters a team, and a fake team that waited
/// for one would look exactly like the bug the command exists to test for.
/// </para>
/// </summary>
/// <param name="fakePlayerService">Service that holds the players.</param>
/// <param name="scheduleService">Service that says which event is open.</param>
/// <param name="lobbyService">Service that knows this lobby's own row.</param>
/// <param name="gameTypeService">Service that resolves the configured game type.</param>
/// <param name="options">Options that name this lobby.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FakePlayerRequestHandlerService(
    FakePlayerService fakePlayerService,
    EventScheduleService scheduleService,
    LobbyService lobbyService,
    LobbyGameTypeService gameTypeService,
    IOptions<LobbyOptions> options,
    ILogger<FakePlayerRequestHandlerService> logger)
{
    private readonly LobbyOptions options = options.Value;

    /// <summary>Creates the players a coordinator request asked for.</summary>
    /// <param name="request">Request the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleAsync(
        FakePlayerRequest request,
        CancellationToken cancellationToken)
    {
        var mode = await ResolveOwnModeAsync(cancellationToken);
        if (mode is null || mode.Value != request.LobbySubtype)
        {
            logger.LogInformation(
                "A fake player request named mode {Mode} and this lobby is {LobbySubtype}; refused",
                request.LobbySubtype,
                mode?.ToString() ?? "unresolved");
            return;
        }

        if (request.Count < 1 || request.Count > FakePlayerService.MaximumPerTeam)
        {
            logger.LogWarning(
                "A fake player request asked for {Count} players, which is outside 1..{Maximum}; refused",
                request.Count,
                FakePlayerService.MaximumPerTeam);
            return;
        }

        // The players join whichever event is open, so an event nobody opened is
        // a request that cannot be placed rather than one to guess at. Several
        // may be open at once, and the oldest is the one a player joining now
        // would be put into, because that is the order the lobby shows them in.
        var open = await scheduleService.ListPublishedAsync(mode.Value, cancellationToken);
        if (open.Count == 0)
        {
            logger.LogInformation(
                "A fake player request arrived while no {Mode} event was open; refused",
                EventScheduleService.ModeName(mode.Value));
            return;
        }

        var schedule = open[0];

        var lobbyIdentifier = await ResolveOwnIdentifierAsync(mode.Value, cancellationToken);
        if (lobbyIdentifier <= 0)
        {
            logger.LogWarning("This lobby's own row could not be found; no fake players were created");
            return;
        }

        var team = await fakePlayerService.CreateTeamAsync(
            mode.Value,
            lobbyIdentifier,
            schedule.Identifier,
            request.Count,
            request.TeamName,
            request.PlayerPrefix,
            cancellationToken);

        if (team is null)
        {
            logger.LogWarning("A fake player request could not be written; refused");
            return;
        }

        logger.LogInformation(
            "Created team {TeamName} with {Count} fake players for event {EventIdentifier} in lobby {LobbyIdentifier}",
            team.Name,
            request.Count,
            schedule.Identifier,
            lobbyIdentifier);
    }

    /// <summary>
    /// Resolves the game type this lobby was configured with, which is the mode
    /// a request has to name for this lobby to answer it.
    /// </summary>
    private async Task<int?> ResolveOwnModeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var gameType = await gameTypeService.ResolveAsync(options.Subtype, cancellationToken);
            return gameType.Identifier;
        }
        catch (InvalidOperationException exception)
        {
            // The lobby's own configuration is broken, which is worth saying
            // once and then refusing rather than throwing out of the stream loop.
            logger.LogWarning(
                "This lobby's game type could not be resolved: {Reason}",
                exception.Message);
            return null;
        }
    }

    /// <summary>Finds the identifier of the lobby row this process registered.</summary>
    private async Task<int> ResolveOwnIdentifierAsync(int mode, CancellationToken cancellationToken)
    {
        var lobbies = await lobbyService.FindActiveGameLobbiesAsync(cancellationToken);
        foreach (var lobby in lobbies)
        {
            if (lobby.SubtypeIdentifier == mode)
            {
                return lobby.Identifier;
            }
        }

        return 0;
    }
}
