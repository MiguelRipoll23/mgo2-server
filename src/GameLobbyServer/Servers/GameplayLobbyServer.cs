using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Servers;

/// <summary>
/// Serves one gameplay lobby: room management, player sessions and match
/// statistics. One process hosts exactly one lobby, whose name and port come
/// from the environment and whose row this instance registered.
/// </summary>
public sealed class GameplayLobbyServer(IServiceProvider serviceProvider, int port, string lobbyName, int lobbyIdentifier)
    : TcpServerBase(serviceProvider, port), IDisposable
{
    private readonly Lock syncGate = new();
    private Timer? countSyncTimer;
    private TimeSpan countSyncDelay;

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

        // An abrupt disconnect that never sends the lobby-leave command leaves
        // the published count stale. Republish a moment later, but coalesce a
        // burst of disconnects into one write instead of one per disconnect;
        // the heartbeat republishes every lobby anyway.
        lock (syncGate)
        {
            countSyncTimer ??= CreateCountSyncTimer(lobbyTracker);
            countSyncTimer.Change(countSyncDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Creates the timer that republishes the counts after a disconnect, and
    /// settles how long a burst is allowed to build up before it does.
    /// <para>
    /// That wait is the lifetime of a read lobby list, which is the only thing the
    /// count is published for: a list written more often than it is read is work
    /// nobody can see. It used to be a second, which announced a departure to the
    /// database hundreds of times for what a reader sees once — and republishing is
    /// already covered by the heartbeat of this lobby, so nothing is lost by
    /// waiting for a window that is a fraction of it.
    /// </para>
    /// </summary>
    /// <param name="lobbyTracker">Tracker whose counts are republished.</param>
    private Timer CreateCountSyncTimer(LobbyTrackerService lobbyTracker)
    {
        countSyncDelay = TimeSpan.FromSeconds(
            Services.GetRequiredService<IOptions<ServerOptions>>().Value.LobbyHeartbeatIntervalSeconds);

        return new Timer(
            _ => _ = SynchronizeCountsAsync(lobbyTracker),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    private async Task SynchronizeCountsAsync(LobbyTrackerService lobbyTracker)
    {
        try
        {
            await lobbyTracker.SynchronizeAllLobbyCountsAsync();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "[{LogPrefix}] lobby count sync after disconnect failed", LogPrefix);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (syncGate)
        {
            countSyncTimer?.Dispose();
            countSyncTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}
