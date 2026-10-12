using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Reclaims the room membership a connection held when it goes away without
/// saying so. A dropped socket never sends the quit, so without this the roster
/// row it took on joining stays behind, and a dedicated host room with a
/// departed player still in it is no longer idle — which is what the assignment
/// sweep asks of every room before it will lease it. One player who closed the
/// game while in the Survival host therefore costs the deployment every match
/// that host would have served, until the daily sweep deletes the room.
/// <para>
/// Leaving on disconnect is deliberate, for the same reason the event cleanup
/// leaves a team: the connection was the member's presence in the room, and a
/// roster row is what the client list and the host itself read. A player who
/// reconnects can rejoin, which is a decision they can make; a room quietly
/// holding a place for a socket that is gone is not.
/// </para>
/// <para>
/// The room's host is not removed. A host that drops takes its own room with it
/// by no longer pinging it, and the daily cleanup is what collects a room the
/// reaper has not reached; removing the host from its own roster instead would
/// leave a live room the host is not in, which is the one shape the idle rule
/// cannot express.
/// </para>
/// </summary>
/// <param name="gameService">Service that owns the rooms, which carry the roster.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class RoomSessionCleanupService(
    GameService gameService,
    ILogger<RoomSessionCleanupService> logger)
{
    /// <summary>Releases the room membership the session held.</summary>
    /// <param name="session">Session that ended.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task CleanupAsync(TcpSession session, CancellationToken cancellationToken = default)
    {
        // The room is named by the session — the join wrote it onto the session
        // in this process — rather than looked up from the character: a roster
        // row may outlive in the database what a character is connected to, and
        // a lookup by character could name a room in a lobby this process does
        // not serve.
        if (session.CharacterIdentifier is not { } characterIdentifier
            || session.GameIdentifier is not { } gameIdentifier)
        {
            return;
        }

        try
        {
            var room = await gameService.FindByIdAsync(gameIdentifier, cancellationToken);
            if (room is null || room.HostIdentifier == characterIdentifier)
            {
                return;
            }

            await gameService.RemovePlayerAsync(gameIdentifier, characterIdentifier, cancellationToken);

            logger.LogDebug(
                "Character {CharacterIdentifier} was removed from room {GameIdentifier} because its connection ended",
                characterIdentifier,
                gameIdentifier);
        }
        catch (Exception exception)
        {
            // A cleanup failure must not take the process down with the socket,
            // and it must not be silent either: what is left behind is a host
            // room the assignment sweep will refuse to lease.
            logger.LogError(
                exception,
                "Room cleanup for character {CharacterIdentifier} after a disconnect failed",
                characterIdentifier);
        }
    }
}
