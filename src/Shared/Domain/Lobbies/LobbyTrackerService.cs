using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Telemetry;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Counts the sessions each gameplay lobby holds in this process and publishes
/// those counts onto the lobby row. A lobby is served by exactly one process, so
/// the published count is this process's population and nothing is shared
/// between instances. The population is also reported to the telemetry as
/// total_players, at the moment a session joins or leaves rather than on a
/// timer.
/// <para>
/// Joining and leaving a lobby is also what records which lobby a character is
/// in, which is the one fact a lobby cannot answer from its own sessions: a
/// roster, a search or a clan list that names a player connected elsewhere
/// needs the shared record rather than this process's channels.
/// </para>
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="metricsService">Service the lobby population is reported to.</param>
/// <param name="presencePublisher">Publisher the arrivals and departures are reported to.</param>
/// <param name="presenceService">Service that records which lobby a character is in.</param>
/// <param name="logger">Logger of the tracker.</param>
public sealed class LobbyTrackerService(
    LobbyService lobbyService,
    ServerMetricsService metricsService,
    ILobbyPresencePublisher presencePublisher,
    CharacterPresenceService presenceService,
    ILogger<LobbyTrackerService> logger)
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
        List<(int LobbyIdentifier, TcpSession Session)> left;

        lock (gate)
        {
            (left, changed) = LeaveLobbyCore(session);

            if (!sessionsByLobby.TryGetValue(lobbyIdentifier, out var sessions))
            {
                sessions = [];
                sessionsByLobby[lobbyIdentifier] = sessions;
            }

            sessions.Add(session);
            changed.Add((lobbyIdentifier, sessions.Count));
        }

        PublishDisconnected(left);
        PublishConnected(lobbyIdentifier, session);
        Report(changed);
        RecordPresence(lobbyIdentifier, session);
    }

    /// <summary>Records that a session left whatever lobby it was in.</summary>
    /// <param name="session">Session that left.</param>
    public void LeaveLobby(TcpSession session)
    {
        List<(int LobbyIdentifier, int Count)> changed;
        List<(int LobbyIdentifier, TcpSession Session)> left;

        lock (gate)
        {
            (left, changed) = LeaveLobbyCore(session);
        }

        PublishDisconnected(left);
        Report(changed);
        ReleasePresence(left);
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
    /// Removes a session from every lobby and reports the populations it left,
    /// together with the sessions that left them. The caller holds the gate.
    /// </summary>
    /// <param name="session">Session to remove.</param>
    private (
        List<(int LobbyIdentifier, TcpSession Session)> Left,
        List<(int LobbyIdentifier, int Count)> Changed) LeaveLobbyCore(TcpSession session)
    {
        var left = new List<(int LobbyIdentifier, TcpSession Session)>();
        var changed = new List<(int LobbyIdentifier, int Count)>();

        foreach (var (lobbyIdentifier, sessions) in sessionsByLobby)
        {
            if (sessions.Remove(session))
            {
                left.Add((lobbyIdentifier, session));
                changed.Add((lobbyIdentifier, sessions.Count));
            }
        }

        return (left, changed);
    }

    /// <summary>Reports a character that entered a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="session">Session that entered it.</param>
    private void PublishConnected(int lobbyIdentifier, TcpSession session)
    {
        // A session that has not named a character is not a player yet, so
        // there is nobody to report and nobody to count as one later.
        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            presencePublisher.PlayerConnected(lobbyIdentifier, characterIdentifier);
        }
    }

    /// <summary>Reports the characters that left the lobbies of a session.</summary>
    /// <param name="left">Lobbies the session left, with the session.</param>
    private void PublishDisconnected(List<(int LobbyIdentifier, TcpSession Session)> left)
    {
        foreach (var (lobbyIdentifier, session) in left)
        {
            if (session.CharacterIdentifier is { } characterIdentifier)
            {
                presencePublisher.PlayerDisconnected(lobbyIdentifier, characterIdentifier);
            }
        }
    }

    /// <summary>
    /// Records that a session's character is now in a lobby. A session that has
    /// not named a character is not a player yet, so there is no presence to
    /// record — the row follows the character, not the socket.
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the session joined.</param>
    /// <param name="session">Session that joined it.</param>
    private void RecordPresence(int lobbyIdentifier, TcpSession session)
    {
        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            RunPresenceAsync(
                presenceService.EnterAsync(characterIdentifier, lobbyIdentifier),
                $"record character {characterIdentifier} in lobby {lobbyIdentifier}");
        }
    }

    /// <summary>
    /// Releases the presence of every character that left a lobby, checking per
    /// lobby that the one being left is still the one recorded: a session that
    /// hopped while its old disconnect was still in flight must not have the
    /// arrival erased by the departure that follows it.
    /// </summary>
    /// <param name="left">Lobbies the sessions left, with the sessions.</param>
    private void ReleasePresence(List<(int LobbyIdentifier, TcpSession Session)> left)
    {
        foreach (var (lobbyIdentifier, session) in left)
        {
            if (session.CharacterIdentifier is { } characterIdentifier)
            {
                RunPresenceAsync(
                    presenceService.LeaveAsync(characterIdentifier, lobbyIdentifier),
                    $"release character {characterIdentifier} from lobby {lobbyIdentifier}");
            }
        }
    }

    /// <summary>
    /// Runs a presence write without holding up the caller.
    /// <para>
    /// The writes are started on the path that accepts and drops connections,
    /// which must not wait on a database round trip: a join is answered and a
    /// disconnect is closed whether or not the record lands. A failure is
    /// logged and swallowed, because a list that is missing a player for a
    /// moment is a lesser failure than a connection that is refused or a socket
    /// that is never released.
    /// </para>
    /// </summary>
    /// <param name="operation">Write to run.</param>
    /// <param name="description">What the write does, for the log.</param>
    private void RunPresenceAsync(Task operation, string description)
    {
        _ = operation.ContinueWith(
            completed => logger.LogError(
                completed.Exception,
                "Failed to {Description}",
                description),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
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
