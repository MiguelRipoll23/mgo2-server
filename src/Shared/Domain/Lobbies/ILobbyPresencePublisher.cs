namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Receives the player presence of a gameplay lobby. The lobby tracker calls it
/// whenever a character enters or leaves a lobby, which is the moment the
/// population of that lobby changes.
/// </summary>
/// <remarks>
/// The lobby owns its own population, so the tracker is the only place that
/// knows a player arrived or left. Whoever is interested in that — the
/// coordinator that keeps the global player count, for example — receives it
/// through this contract rather than reading the lobby's state.
/// </remarks>
public interface ILobbyPresencePublisher
{
    /// <summary>Reports that a character entered a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    void PlayerConnected(int lobbyIdentifier, int characterIdentifier);

    /// <summary>Reports that a character left a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    void PlayerDisconnected(int lobbyIdentifier, int characterIdentifier);
}
