using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.AccountLobbyServer;

/// <summary>
/// Serves character creation, deletion and selection. It serves the port of the
/// lobby row of type account, which the server publishes from the environment
/// when it starts.
/// </summary>
public sealed class AccountServer(IServiceProvider serviceProvider, int port)
    : TcpServerBase(serviceProvider, port)
{
    /// <inheritdoc />
    protected override ServerType ServerType => ServerType.Account;

    /// <inheritdoc />
    protected override string LogPrefix => "tcp:account";
}
