using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Gameplay;

/// <summary>Recognises in-game control records without answering undecoded traffic.</summary>
/// <param name="logger">Logger of this handler.</param>
public sealed class InGameControlHandler(ILogger<InGameControlHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: in-game control {MessageType:x4} of {BodyLength} bytes from {RemoteAddress} " +
            "(fourth byte {Flags}); not answered, its meaning is unresolved",
            context.LocalPort,
            context.Message.Type,
            context.Message.Body.Length,
            context.Remote,
            context.Message.Flags);

        return Task.CompletedTask;
    }
}
