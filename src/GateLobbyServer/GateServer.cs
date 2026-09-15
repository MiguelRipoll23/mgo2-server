using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GateLobbyServer;

/// <summary>
/// First connection point of the game: answers the lobby list and the news. It
/// serves the port of the lobby row of type gate, which the server publishes
/// from the environment when it starts.
/// </summary>
public sealed class GateServer(IServiceProvider serviceProvider, int port)
    : TcpServerBase(serviceProvider, port)
{
    /// <inheritdoc />
    protected override ServerType ServerType => ServerType.Gate;

    /// <inheritdoc />
    protected override string LogPrefix => "tcp:gate";
}
