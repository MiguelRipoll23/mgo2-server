using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Connection;

/// <summary>Reads a peer's keyed keep-alive without answering it.</summary>
/// <param name="logger">Logger of this handler.</param>
public sealed class PeerKeepAliveHandler(ILogger<PeerKeepAliveHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        logger.LogDebug(
            "UDP {LocalPort}: keyed keep-alive from {RemoteAddress}; not answered, the recorded host sends none",
            context.LocalPort,
            context.Remote);

        return Task.CompletedTask;
    }
}
