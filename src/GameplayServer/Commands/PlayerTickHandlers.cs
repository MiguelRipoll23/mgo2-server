using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands;

/// <summary>
/// Reports the health and stamina a peer sends, so a round can be watched
/// without turning the log into the tick stream.
/// </summary>
/// <remarks>
/// <para>
/// The record is two bytes and it travels several times a second per player,
/// with a character between fights reading at its ceiling in every one of them.
/// So every record is logged at debug level and a record that changed the
/// character's state — health reaching zero, or coming back from it — is logged
/// again at information level. That is the shape of a round: three of each over
/// thirteen minutes, with the repeats underneath at debug.
/// </para>
/// <para>
/// Nothing is answered. The capture has the host reading these records and not
/// writing one, and a record the host answers with a record of its own is a
/// record this server cannot yet say is right.
/// </para>
/// </remarks>
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

/// <summary>
/// Reports where each character is and whether it is dead, so a round can be
/// watched as movement rather than as the tick stream behind it.
/// </summary>
/// <remarks>
/// <para>
/// The record arrives several times a second per character, so every one is
/// logged at debug level and a record that changed the character's state — the
/// death bit flipping — is logged again at information level. The position
/// itself is carried in world units, scaled out of the wire's tenths by
/// <see cref="PlayerPosition.FromWire"/>, and the death bit is the one the
/// capture measures: set on 390 records, every one of them a character whose
/// health record was reading zero at the time.
/// </para>
/// <para>
/// The 44-byte form is logged with its bytes and nothing else. No offset in it
/// lands on any character's path, so <see cref="PlayerPositionRecordUtility.Parse"/>
/// returns <c>null</c> for it and there is no position to report — but the
/// record is on the wire several times a second and the bytes are the only way
/// to get at the form while a session is running.
/// </para>
/// <para>
/// Nothing is answered, for the same reason the vitals record is not.
/// </para>
/// </remarks>
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