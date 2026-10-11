using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Connection;

/// <summary>
/// Reads a join-probe record the client sends on reconnection attempts after a
/// stalled join. The same twelve-bit id travels in both the keep-alive class
/// (0x5709) and the in-game control class (0xd709); the class bit decides
/// whether the body is empty or one byte.
/// </summary>
/// <remarks>
/// <para>
/// The type is recognised rather than dropped as an unknown command, because
/// dropping it leaves the rest of the frame's messages unprocessed and because
/// the recorded host answers every record the client sends. What the body — when
/// present — means is **[U]**, and the server does not answer: answering a record
/// this server cannot read is how 0x43CA/0x43CB and 0x4442 went wrong in this
/// project.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this handler.</param>
public sealed class JoinProbeHandler(ILogger<JoinProbeHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: join probe 0x{MessageType:x4} from {RemoteAddress}; not answered, meaning unresolved",
            context.LocalPort,
            context.Message.Type,
            context.Remote);

        return Task.CompletedTask;
    }
}
