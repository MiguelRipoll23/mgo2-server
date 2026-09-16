namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Owns the population of every gameplay lobby the coordinator is connected
/// to: which characters sit in which lobby, and the global total across all of
/// them.
/// </summary>
/// <remarks>
/// The lobby owns its own state, so this is not a second source of truth about
/// who is in a lobby — it is the coordinator's view of the numbers the lobbies
/// report, which is what the global count and the Discord channel are built
/// from. Every method reports whether the population actually changed, because
/// a repeated event (a lobby that reports a departure twice) must not count
/// twice or announce twice.
/// </remarks>
public sealed class LobbyPresenceService
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, HashSet<int>> charactersByLobby = [];
    private readonly Dictionary<int, string> namesByLobby = [];
    private int totalPlayerCount;

    /// <summary>Number of connected players across every lobby.</summary>
    public int TotalPlayers
    {
        get
        {
            lock (gate)
            {
                return totalPlayerCount;
            }
        }
    }

    /// <summary>Number of connected players of one lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public int GetPlayerCount(int lobbyIdentifier)
    {
        lock (gate)
        {
            return charactersByLobby.TryGetValue(lobbyIdentifier, out var characters) ? characters.Count : 0;
        }
    }

    /// <summary>Returns the name a lobby registered with.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    public string GetLobbyName(int lobbyIdentifier)
    {
        lock (gate)
        {
            return namesByLobby.TryGetValue(lobbyIdentifier, out var name) ? name : string.Empty;
        }
    }

    /// <summary>Lists the lobbies the coordinator holds.</summary>
    public List<LobbyPresenceSummary> ListLobbies()
    {
        lock (gate)
        {
            return
            [
                .. charactersByLobby.Select(pair => new LobbyPresenceSummary(
                    pair.Key,
                    namesByLobby.TryGetValue(pair.Key, out var name) ? name : string.Empty,
                    pair.Value.Count)),
            ];
        }
    }

    /// <summary>
    /// Replaces everything known about a lobby with the population its
    /// registration carries. A lobby that reconnects after the coordinator
    /// restarted is back at the truth instead of at the deltas it missed.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="lobbyName">Name the lobby reports itself under.</param>
    /// <param name="characterIdentifiers">Characters connected to it.</param>
    /// <returns>The global total after the registration.</returns>
    public int RegisterLobby(
        int lobbyIdentifier,
        string lobbyName,
        IEnumerable<int> characterIdentifiers)
    {
        lock (gate)
        {
            if (charactersByLobby.TryGetValue(lobbyIdentifier, out var previous))
            {
                totalPlayerCount -= previous.Count;
            }

            var characters = new HashSet<int>(characterIdentifiers);
            charactersByLobby[lobbyIdentifier] = characters;
            namesByLobby[lobbyIdentifier] = lobbyName;
            totalPlayerCount += characters.Count;

            return totalPlayerCount;
        }
    }

    /// <summary>Records that a character entered a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="totalPlayers">Global total after the change.</param>
    /// <returns>Whether the character was not already counted in that lobby.</returns>
    public bool AddPlayer(int lobbyIdentifier, int characterIdentifier, out int totalPlayers)
    {
        lock (gate)
        {
            var characters = CharactersOf(lobbyIdentifier);
            if (!characters.Add(characterIdentifier))
            {
                totalPlayers = totalPlayerCount;
                return false;
            }

            totalPlayerCount++;
            totalPlayers = totalPlayerCount;
            return true;
        }
    }

    /// <summary>Records that a character left a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="totalPlayers">Global total after the change.</param>
    /// <returns>Whether the character was counted in that lobby.</returns>
    public bool RemovePlayer(int lobbyIdentifier, int characterIdentifier, out int totalPlayers)
    {
        lock (gate)
        {
            if (!charactersByLobby.TryGetValue(lobbyIdentifier, out var characters) ||
                !characters.Remove(characterIdentifier))
            {
                totalPlayers = totalPlayerCount;
                return false;
            }

            totalPlayerCount--;
            totalPlayers = totalPlayerCount;
            return true;
        }
    }

    /// <summary>
    /// Forgets a lobby and its players. A lobby that is no longer connected
    /// cannot have anybody in it, so its population leaves with it.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="totalPlayers">Global total after the change.</param>
    /// <returns>Whether the lobby was known.</returns>
    public bool RemoveLobby(int lobbyIdentifier, out int totalPlayers)
    {
        lock (gate)
        {
            if (charactersByLobby.Remove(lobbyIdentifier, out var characters))
            {
                totalPlayerCount -= characters.Count;
            }

            namesByLobby.Remove(lobbyIdentifier);
            totalPlayers = totalPlayerCount;
            return characters is not null;
        }
    }

    /// <summary>Returns the population of a lobby, creating it when it is new.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    private HashSet<int> CharactersOf(int lobbyIdentifier)
    {
        if (!charactersByLobby.TryGetValue(lobbyIdentifier, out var characters))
        {
            characters = [];
            charactersByLobby[lobbyIdentifier] = characters;
        }

        return characters;
    }
}

/// <summary>Population of one lobby.</summary>
/// <param name="LobbyIdentifier">Identifier of the lobby.</param>
/// <param name="LobbyName">Name the lobby reports itself under.</param>
/// <param name="PlayerCount">Number of characters in it.</param>
public sealed record LobbyPresenceSummary(int LobbyIdentifier, string LobbyName, int PlayerCount);
