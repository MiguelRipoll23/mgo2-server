using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Counts the sessions each gameplay lobby holds in this process and publishes
/// those counts onto the lobby row. A lobby is served by exactly one process, so
/// the published count is this process's population and nothing is shared
/// between instances.
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
public sealed class LobbyTrackerService(LobbyService lobbyService)
{
    private readonly Dictionary<int, HashSet<TcpSession>> sessionsByLobby = [];

    /// <summary>Records that a session joined a lobby.</summary>
    /// <param name="session">Session that joined.</param>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public void JoinLobby(TcpSession session, int lobbyIdentifier)
    {
        LeaveLobby(session);

        if (!sessionsByLobby.TryGetValue(lobbyIdentifier, out var sessions))
        {
            sessions = [];
            sessionsByLobby[lobbyIdentifier] = sessions;
        }

        sessions.Add(session);
    }

    /// <summary>Records that a session left whatever lobby it was in.</summary>
    /// <param name="session">Session that left.</param>
    public void LeaveLobby(TcpSession session)
    {
        foreach (var sessions in sessionsByLobby.Values)
        {
            sessions.Remove(session);
        }
    }

    /// <summary>Returns how many sessions this process holds in a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public int GetPlayerCount(int lobbyIdentifier) =>
        sessionsByLobby.TryGetValue(lobbyIdentifier, out var sessions) ? sessions.Count : 0;

    /// <summary>Publishes the player count of every lobby this process serves.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SynchronizeAllLobbyCountsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (lobbyIdentifier, sessions) in sessionsByLobby)
        {
            await lobbyService.UpdatePlayerCountAsync(lobbyIdentifier, sessions.Count, cancellationToken);
        }
    }
}
