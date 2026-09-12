using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Interfaces;

/// <summary>
/// A peer-to-peer message handler, the UDP analogue of <see cref="ICommandHandler"/>.
/// </summary>
public interface IPeerCommandHandler
{
    /// <summary>Handles one inbound peer message.</summary>
    /// <param name="context">Session, message and reply channel for this message.</param>
    Task HandleAsync(PeerContext context);
}
