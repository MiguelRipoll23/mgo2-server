using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rooms a match may be hosted in, read from the one place they live: the
/// games table.
/// <para>
/// It is a service of its own because "which rooms can host this match" is asked
/// from more than one place, and the answer was once assembled from two sources
/// — the rows a player opened and a second store the testing tools kept in
/// memory. A reader that reached only one of them found a host available or
/// found none depending on which it was. Both are the same kind of room now, a
/// row, so there is one source and one answer.
/// </para>
/// <para>
/// The rooms are returned as the <see cref="Game"/> they are, with the roster
/// the idle rule reads, which is what lets the eligibility rule and the
/// assignment packets be written against one type.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
public sealed class EventHostRoomPoolService(GameService gameService)
{
    /// <summary>Every room a match in a lobby could be hosted in.</summary>
    /// <param name="lobbyIdentifier">Lobby the rooms belong to.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The rooms, each with the roster the idle rule reads.</returns>
    public async Task<List<Game>> ListAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (lobbyIdentifier <= 0)
        {
            return [];
        }

        return await gameService.FindByLobbyAsync(lobbyIdentifier, cancellationToken);
    }
}
