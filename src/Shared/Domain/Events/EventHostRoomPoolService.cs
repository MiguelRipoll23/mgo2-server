using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rooms a match may be hosted in.
/// <para>
/// It is a service of its own because "which rooms can host this match" is asked
/// from more than one place, and the answer has two sources: the rows a player
/// opened, and the rooms this process opened for a test. A reader that reached
/// only one of them found a host available or found none depending on which it
/// was, so both are read here and the caller sees one list.
/// </para>
/// <para>
/// The two are handed back as the same thing — the <see cref="Game"/> a room is,
/// with the roster the idle rule reads — which is what lets the eligibility rule,
/// the claim and the assignment packets be written against one type and never
/// learn where a room came from.
/// </para>
/// <para>
/// The merge lives here rather than in <see cref="GameService"/> because the
/// games service's reads are the room browser and the lookup every room handler
/// makes before it writes. A room with no row behind it would be offered to
/// players as one they may join, and the join would then fail on the foreign key
/// that ties a roster row to the room it sits in. The reference hides its
/// reserved host rooms from the browser for the same reason: a room that exists
/// to be leased to a match does not belong on the room list.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the rooms that are rows.</param>
/// <param name="memoryService">Store that owns the rooms that exist only in this process.</param>
public sealed class EventHostRoomPoolService(
    GameService gameService,
    EventHostRoomMemoryService memoryService)
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

        var rooms = await gameService.FindByLobbyAsync(lobbyIdentifier, cancellationToken);

        // Appended rather than merged on identity: an in-memory room's identifier
        // comes from the test range, which no database sequence reaches, so the
        // two sets cannot name the same room.
        rooms.AddRange(memoryService.List(lobbyIdentifier));
        return rooms;
    }
}
