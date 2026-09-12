using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Interfaces;

/// <summary>A handler for one TCP command of one server role.</summary>
public interface ICommandHandler
{
    /// <summary>Handles one inbound packet.</summary>
    /// <param name="session">Connection the packet arrived on.</param>
    /// <param name="packet">Decoded packet to handle.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken);
}
