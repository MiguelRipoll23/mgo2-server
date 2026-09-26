using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The pool of rooms a match may be hosted in: the real rooms a player opened,
/// beside the in-memory ones the testing tools made.
/// <para>
/// It is a service of its own because "which rooms can host this match" is asked
/// from more than one place, and each of them was reaching only one of the two
/// sources. A sweep that listed the database found no in-memory room, and a tool
/// that listed the in-memory ones found no real room, so a pairing made from the
/// testing tools had a host available or had none depending on which reader
/// asked. One pool answers it for both.
/// </para>
/// <para>
/// The two are returned as the same <see cref="Game"/>, which is what makes an
/// in-memory room act like a real one rather than merely resemble it: the
/// eligibility rule and the assignment packets are written against that type and
/// cannot tell which of the two they are holding.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the real rooms.</param>
/// <param name="fakeRooms">The in-memory host rooms this lobby is holding.</param>
public sealed class EventHostRoomPoolService(
    GameService gameService,
    FakeHostRoomService fakeRooms)
{
    /// <summary>
    /// Every room a match in a lobby could be hosted in, real and in-memory.
    /// </summary>
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

        var real = await gameService.FindByLobbyAsync(lobbyIdentifier, cancellationToken);
        var inMemory = fakeRooms.ListRooms(lobbyIdentifier)
            .Select(room => room.Room)
            .ToList();

        // Both halves are returned, rather than one standing in for the other. A
        // real room is a player's own and is never withheld because an in-memory
        // one is available, and an in-memory room is a testing device that must
        // not be passed off as a player's.
        real.AddRange(inMemory);
        return real;
    }
}
