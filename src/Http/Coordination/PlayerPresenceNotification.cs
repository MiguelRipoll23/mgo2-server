namespace Mgo2Server.Http.Coordination;

/// <summary>
/// One player of a gameplay lobby that connected or disconnected, together with
/// the global total after it happened.
/// </summary>
/// <param name="LobbyIdentifier">Lobby the player is in.</param>
/// <param name="LobbyName">Name the lobby reported itself under.</param>
/// <param name="CharacterIdentifier">Identifier of the character.</param>
/// <param name="CharacterName">Name of the character, read from the database.</param>
/// <param name="Connected">Whether the character connected rather than left.</param>
/// <param name="TotalPlayers">Connected players across every lobby after the change.</param>
public sealed record PlayerPresenceNotification(
    int LobbyIdentifier,
    string LobbyName,
    int CharacterIdentifier,
    string CharacterName,
    bool Connected,
    int TotalPlayers);

/// <summary>
/// A destination of the player presence of the deployment. The Discord
/// integration is one; the coordinator itself keeps another and needs no
/// contract for it.
/// </summary>
/// <remarks>
/// The observers are told about a change rather than asked for one, so a
/// destination that is slow or unavailable cannot hold up the coordination
/// stream. Every call is expected to report its own failures instead of
/// throwing them into the coordinator.
/// </remarks>
public interface IPlayerPresenceObserver
{
    /// <summary>Reports that a character connected to or disconnected from a lobby.</summary>
    /// <param name="notification">Change that happened.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    Task PlayerPresenceChangedAsync(
        PlayerPresenceNotification notification,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reports that the total changed without a character behind it, which is
    /// what happens when a whole lobby goes away.
    /// </summary>
    /// <param name="totalPlayers">Total after the change.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    Task PlayerTotalChangedAsync(int totalPlayers, CancellationToken cancellationToken);
}
