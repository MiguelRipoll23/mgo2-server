using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rooms a match may be hosted in, read from the one place they live: the
/// games table, and the one place the choice of host is made.
/// <para>
/// It is a service of its own because "which room can host this match" is asked
/// from more than one place, and the answer was once assembled in each of them.
/// A reader that asked a slightly different question found a host available, or
/// found none, depending on which reader it was. Both the assignment that leases
/// a room and the readers that report which room is free ask here now.
/// </para>
/// <para>
/// The rooms are deliberately not narrowed by lobby. A dedicated host is a room
/// rented for a role, and the role sets the room's mode when it is created, so a
/// host opened in another lobby is still the host the match needs.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
public sealed class EventHostRoomPoolService(GameService gameService)
{
    /// <summary>Every room a match could be hosted in.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The rooms, each with the roster the idle rule reads.</returns>
    public async Task<List<Game>> ListAsync(CancellationToken cancellationToken = default) =>
        await gameService.FindHostRoomCandidatesAsync(cancellationToken);

    /// <summary>Picks the room that hosts a match out of a candidate list.</summary>
    /// <param name="rooms">Rooms to choose from, as <see cref="ListAsync"/> returned them.</param>
    /// <param name="matchType">Mode of the match.</param>
    /// <param name="participantCount">Players the match brings.</param>
    /// <returns>The first room that may take the match, or null when none may.</returns>
    public static Game? FindHost(List<Game> rooms, int matchType, int participantCount)
    {
        ArgumentNullException.ThrowIfNull(rooms);

        return rooms.FirstOrDefault(room =>
            EventHostEligibilityUtils.IsEligibleHost(room, matchType, participantCount));
    }
}
