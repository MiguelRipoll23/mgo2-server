namespace Mgo2Server.Http.Coordination;

/// <summary>
/// A game that was created in a gameplay lobby, or that a player entered, as
/// the lobby reported it.
/// </summary>
/// <param name="LobbyIdentifier">Lobby the game is in.</param>
/// <param name="LobbyName">Name the lobby reported itself under.</param>
/// <param name="CharacterName">Name of the character behind the game, read from the database.</param>
/// <param name="GameName">Name the game was given.</param>
/// <param name="Created">Whether the game was created rather than joined.</param>
public sealed record GameActivityNotification(
    int LobbyIdentifier,
    string LobbyName,
    string CharacterName,
    string GameName,
    bool Created);

/// <summary>
/// A destination of the game activity of the deployment. The Discord
/// integration is one.
/// </summary>
/// <remarks>
/// The observers are told about a game rather than asked for one, so a
/// destination that is slow or unavailable cannot hold up the coordination
/// stream of a lobby. Every call is expected to report its own failures
/// instead of throwing them into the coordinator.
/// </remarks>
public interface IGameActivityObserver
{
    /// <summary>Reports that a game was created in a lobby or that a player entered one.</summary>
    /// <param name="notification">Game that was created or joined.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    Task GameActivityReportedAsync(
        GameActivityNotification notification,
        CancellationToken cancellationToken);
}