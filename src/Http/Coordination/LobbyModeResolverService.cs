using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Finds the running gameplay lobby a lobby mode names.
/// <para>
/// The requests the API hands to the lobbies name a mode rather than an
/// identifier, because the API does not know the lobby identifiers. Resolving
/// one is the same question every time — a mode may have no lobby, and several
/// lobbies may share one — so it is asked in one place instead of in each
/// request that needs it.
/// </para>
/// <para>
/// Several lobbies may share a mode, so the first is taken rather than
/// insisting a deployment only ever runs one. A request goes to a lobby that
/// exists rather than to none.
/// </para>
/// </summary>
/// <param name="lobbyService">Service the running lobbies are read from.</param>
public sealed class LobbyModeResolverService(LobbyService lobbyService)
{
    /// <summary>Finds the running lobby of a mode.</summary>
    /// <param name="mode">Lobby mode to resolve.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The lobby, or null when the mode is not an event lobby or none runs it.</returns>
    public async Task<LobbyResponse?> ResolveAsync(int mode, CancellationToken cancellationToken)
    {
        if (!EventConstants.IsEventSelector(mode))
        {
            return null;
        }

        var lobbies = await lobbyService.GetLobbiesAsync(cancellationToken);
        return lobbies.FirstOrDefault(lobby => lobby.SubtypeIdentifier == mode);
    }
}
