using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>Reports the position and death state carried by a peer's record.</summary>
/// <param name="playerStates">Last state seen for each peer.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerPositionHandler(
    PlayerStateService playerStates,
    ILogger<PlayerPositionHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        var message = context.Message;
        var isDead = PlayerPositionRecordUtility.IsDead(message.Body);

        if (PlayerPositionRecordUtility.ParseSample(message.Body) is not { } sample)
        {
            logger.LogDebug(
                "UDP {LocalPort}: position {MessageType:x4} from {RemoteAddress} is the {BodyLength}-byte form, " +
                "which no offset has been placed on: {BodyHex}",
                context.LocalPort,
                message.Type,
                context.Remote,
                message.Body.Length,
                Convert.ToHexString(message.Body));
            return Task.CompletedTask;
        }

        var change = playerStates.ObservePosition(context.Remote, isDead);
        var position = sample.Position;

        logger.LogDebug(
            "UDP {LocalPort}: position {MessageType:x4} from {RemoteAddress}: " +
            "x {X}, y {Y}, z {Z}{DeadSuffix}",
            context.LocalPort,
            message.Type,
            context.Remote,
            position.X,
            position.Y,
            position.Z,
            isDead ? " (dead)" : string.Empty);

        if (change.Died)
        {
            logger.LogInformation(
                "UDP {LocalPort}: {RemoteAddress} died at x {X}, y {Y}, z {Z}; health last read {PreviousHealth}",
                context.LocalPort,
                context.Remote,
                position.X,
                position.Y,
                position.Z,
                change.PreviousHealth);
        }
        else if (change.Revived)
        {
            logger.LogInformation(
                "UDP {LocalPort}: {RemoteAddress} is back at x {X}, y {Y}, z {Z}",
                context.LocalPort,
                context.Remote,
                position.X,
                position.Y,
                position.Z);
        }

        return Task.CompletedTask;
    }
}
