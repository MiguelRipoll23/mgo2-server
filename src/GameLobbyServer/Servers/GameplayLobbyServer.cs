using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.GameLobbyServer.Servers;

/// <summary>
/// Serves one gameplay lobby: room management, player sessions and match
/// statistics. One process hosts exactly one lobby, whose name and port come
/// from the environment and whose row this instance registered.
/// </summary>
public sealed class GameplayLobbyServer(IServiceProvider serviceProvider, int port, string lobbyName, int lobbyIdentifier)
    : TcpServerBase(serviceProvider, port)
{
    /// <inheritdoc />
    protected override ServerType ServerType => ServerType.GameplayLobby;

    /// <inheritdoc />
    protected override string LogPrefix =>
        $"tcp:{lobbyName.ToLowerInvariant().Replace(' ', '_')}";

    /// <inheritdoc />
    protected override void OnSessionCreated(TcpSession session)
    {
        Services.GetRequiredService<ActiveGameSessionsService>().Add(session);

        // The lobby a client lands in is the server it connected to, not a
        // database column that is never written. Stamping it here lets the
        // room commands validate the session without trusting character rows.
        session.LobbyIdentifier = lobbyIdentifier;
    }

    /// <inheritdoc />
    protected override void OnSessionDestroyed(TcpSession session)
    {
        Services.GetRequiredService<ActiveGameSessionsService>().Remove(session);
        var lobbyTracker = Services.GetRequiredService<LobbyTrackerService>();
        lobbyTracker.LeaveLobby(session);

        // Without this, an abrupt disconnect that never sends the lobby-leave
        // command leaves the published count stale until the next join.
        _ = Task.Run(async () =>
        {
            try
            {
                await lobbyTracker.SynchronizeAllLobbyCountsAsync();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "[{LogPrefix}] lobby count sync after disconnect failed", LogPrefix);
            }
        });
    }
}
