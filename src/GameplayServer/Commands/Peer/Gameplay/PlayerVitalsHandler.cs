using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>Reports health and stamina records received from a peer.</summary>
/// <param name="playerStates">Last state seen for each peer.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerVitalsHandler(
    PlayerStateService playerStates,
    ILogger<PlayerVitalsHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        var message = context.Message;
        if (PlayerVitalsRecordUtility.Parse(message.Body) is not { } vitals)
        {
            logger.LogDebug(
                "UDP {LocalPort}: vitals {MessageType:x4} from {RemoteAddress} carries {BodyLength} bytes, " +
                "not the two the record is built of; not answered",
                context.LocalPort,
                message.Type,
                context.Remote,
                message.Body.Length);
            return Task.CompletedTask;
        }

        var change = playerStates.ObserveVitals(context.Remote, vitals);

        logger.LogDebug(
            "UDP {LocalPort}: vitals {MessageType:x4} from {RemoteAddress}: health {Health}, stamina {Stamina}",
            context.LocalPort,
            message.Type,
            context.Remote,
            vitals.Health,
            vitals.Stamina);

        if (change.Died)
        {
            logger.LogInformation(
                "UDP {LocalPort}: {RemoteAddress} died: health {PreviousHealth} -> 0",
                context.LocalPort,
                context.Remote,
                change.PreviousHealth);
        }
        else if (change.Revived)
        {
            logger.LogInformation(
                "UDP {LocalPort}: {RemoteAddress} is back: health 0 -> {Health}",
                context.LocalPort,
                context.Remote,
                vitals.Health);
        }

        return Task.CompletedTask;
    }
}
