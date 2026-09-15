using Mgo2Server.Shared.Telemetry;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Counts the sessions each gameplay lobby holds in this process and publishes
/// those counts onto the lobby row. A lobby is served by exactly one process, so
/// the published count is this process's population and nothing is shared
/// between instances. The population is also reported to the telemetry as
/// total_players, at the moment a session joins or leaves rather than on a
/// timer.
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="metricsService">Service the lobby population is reported to.</param>
public sealed class LobbyTrackerService(
    LobbyService lobbyService,
    ServerMetricsService metricsService)
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, HashSet<TcpSession>> sessionsByLobby = [];

    /// <summary>
    /// Records that a session joined a lobby. The session is removed from
    /// whatever lobby it was in first, so a move reports both populations, and
    /// the new population is published as it changes.
    /// </summary>
    /// <param name="session">Session that joined.</param>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public void JoinLobby(TcpSession session, int lobbyIdentifier)
    {
        List<(int LobbyIdentifier, int Count)> changed;

        lock (gate)
        {
            changed = LeaveLobbyCore(session);

            if (!sessionsByLobby.TryGetValue(lobbyIdentifier, out var sessions))
            {
                sessions = [];
                sessionsByLobby[lobbyIdentifier] = sessions;
            }

            sessions.Add(session);
            changed.Add((lobbyIdentifier, sessions.Count));
        }

        Report(changed);
    }

    /// <summary>Records that a session left whatever lobby it was in.</summary>
    /// <param name="session">Session that left.</param>
    public void LeaveLobby(TcpSession session)
    {
        List<(int LobbyIdentifier, int Count)> changed;

        lock (gate)
        {
            changed = LeaveLobbyCore(session);
        }

        Report(changed);
    }

    /// <summary>Returns how many sessions this process holds in a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public int GetPlayerCount(int lobbyIdentifier)
    {
        lock (gate)
        {
            return sessionsByLobby.TryGetValue(lobbyIdentifier, out var sessions) ? sessions.Count : 0;
        }
    }

    /// <summary>Publishes the player count of every lobby this process serves.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SynchronizeAllLobbyCountsAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot under the lock, then write outside it: a join or leave that
        // arrives mid-write must not invalidate the enumeration.
        List<(int LobbyIdentifier, int Count)> counts;
        lock (gate)
        {
            counts = [.. sessionsByLobby.Select(pair => (pair.Key, pair.Value.Count))];
        }

        foreach (var (lobbyIdentifier, count) in counts)
        {
            await lobbyService.UpdatePlayerCountAsync(lobbyIdentifier, count, cancellationToken);
        }
    }

    /// <summary>
    /// Removes a session from every lobby and reports the populations it left.
    /// The caller holds the gate.
    /// </summary>
    private List<(int LobbyIdentifier, int Count)> LeaveLobbyCore(TcpSession session)
    {
        var changed = new List<(int LobbyIdentifier, int Count)>();

        foreach (var (lobbyIdentifier, sessions) in sessionsByLobby)
        {
            if (sessions.Remove(session))
            {
                changed.Add((lobbyIdentifier, sessions.Count));
            }
        }

        return changed;
    }

    /// <summary>
    /// Publishes the populations that just changed, each labelled with its
    /// lobby so a dashboard can filter by lobby. Nothing is recorded for a lobby
    /// the session was not in, so the gauge never repeats a value that nobody
    /// changed.
    /// </summary>
    private void Report(List<(int LobbyIdentifier, int Count)> changed)
    {
        foreach (var (lobbyIdentifier, count) in changed)
        {
            metricsService.RecordTotalPlayers(count, LobbyName(lobbyIdentifier));
        }
    }

    /// <summary>
    /// Reports the name a lobby is labelled with, falling back to its identifier
    /// when the cache does not hold it yet.
    /// </summary>
    private string LobbyName(int lobbyIdentifier) =>
        lobbyService.GetCached().FirstOrDefault(lobby => lobby.Identifier == lobbyIdentifier)?.Name
            ?? ServerMetricsService.FormatLobbyIdentifier(lobbyIdentifier);
}
