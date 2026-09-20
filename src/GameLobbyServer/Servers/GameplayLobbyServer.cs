using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Servers;

/// <summary>
/// Serves one gameplay lobby: room management, player sessions and match
/// statistics. One process hosts exactly one lobby, whose name and port come
/// from the environment and whose row this instance registered.
/// </summary>
public sealed class GameplayLobbyServer(IServiceProvider serviceProvider, int port, string lobbyName, int lobbyIdentifier)
    : TcpServerBase(serviceProvider, port), IDisposable
{
    /// <summary>
    /// How long a burst of abrupt disconnects is allowed to build up before the
    /// published population is written again.
    /// <para>
    /// It is short, and it is deliberately not derived from any beat, because it is
    /// now the only thing that republishes a count an abrupt disconnect changed: the
    /// heartbeat no longer writes a population. It is long enough that everybody
    /// dropping at once collapses into the one write they would have shared, and
    /// short enough that the next list a client reads is not told about players who
    /// are already gone.
    /// </para>
    /// </summary>
    private static readonly TimeSpan CountSyncDelay = TimeSpan.FromSeconds(5);

    private readonly Lock syncGate = new();
    private Timer? countSyncTimer;

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

        // An abrupt disconnect that never sends the lobby-leave command is the one
        // departure nobody writes the count for: the leave path republishes it and
        // so does every join, and the beat no longer does. So it is written a moment
        // later, which collapses everyone dropping at once into one statement
        // instead of one per socket.
        lock (syncGate)
        {
            countSyncTimer ??= CreateCountSyncTimer(lobbyTracker);
            countSyncTimer.Change(CountSyncDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Creates the timer that republishes the count after an abrupt disconnect. It
    /// is armed once and re-armed by every disconnect, so a burst lands on the last
    /// arming and costs one write.
    /// </summary>
    /// <param name="lobbyTracker">Tracker whose count is republished.</param>
    private Timer CreateCountSyncTimer(LobbyTrackerService lobbyTracker) =>
        new(
            _ => _ = SynchronizeCountsAsync(lobbyTracker),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

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
