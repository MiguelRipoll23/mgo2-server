using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Answers the two questions a lobby is asked about itself: which mode it is
/// running, and which row of the lobby table is its own. Both are read from the
/// process's configuration and the database rather than from anything a caller
/// passed, so a request naming a lobby cannot move itself to a different one.
/// </summary>
/// <param name="gameTypeService">Service that resolves the configured game type.</param>
/// <param name="lobbyService">Service that knows this process's lobby rows.</param>
/// <param name="options">Options that name this lobby.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyIdentityService(
    LobbyGameTypeService gameTypeService,
    LobbyService lobbyService,
    IOptions<LobbyOptions> options,
    ILogger<LobbyIdentityService> logger)
{
    private readonly LobbyOptions options = options.Value;

    /// <summary>
    /// Resolves the game type this lobby was configured with, which is the mode
    /// a request has to name for this lobby to answer it.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The mode, or null when the lobby's own configuration is broken.</returns>
    public async Task<int?> ResolveModeAsync(CancellationToken cancellationToken)
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
    /// <param name="mode">Mode of this lobby, which its row carries.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The lobby identifier, or zero when no row matches.</returns>
    public async Task<int> ResolveIdentifierAsync(int mode, CancellationToken cancellationToken)
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
