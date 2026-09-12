using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GateLobbyServer;

/// <summary>
/// First connection point of the game: answers the lobby list and the news. Its
/// port comes from the lobby row of type gate, so it is known only once the
/// lobby cache has been loaded.
/// </summary>
public sealed class GateServer(IServiceProvider serviceProvider, int port)
    : TcpServerBase(serviceProvider, port)
{
    /// <inheritdoc />
    protected override ServerType ServerType => ServerType.Gate;

    /// <inheritdoc />
    protected override string LogPrefix => "tcp:gate";
}
