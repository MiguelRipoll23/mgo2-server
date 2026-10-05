using Mgo2Server.Shared.Domain.Characters;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Turns the games a gameplay lobby reports into notifications and hands them
/// to every destination that reports what the players are doing.
/// </summary>
/// <remarks>
/// The coordinator keeps no state of a game: nothing here is counted, and a
/// game event that is dropped costs a line in a channel rather than a
/// discrepancy in the player count.
/// </remarks>
/// <param name="presence">Counts the coordinator owns, read for the lobby name.</param>
/// <param name="characterService">Service the character names are read from.</param>
/// <param name="observers">Destinations the games are reported to.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class GameActivityNotificationService(
    LobbyPresenceService presence,
    CharacterService characterService,
    IEnumerable<IGameActivityObserver> observers,
    ILogger<GameActivityNotificationService> logger)
{
    /// <summary>Name reported for a character the database does not know.</summary>
    private const string UnknownCharacterName = "unknown character";

    /// <summary>Reports that a game was created in a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the host that opened it.</param>
    /// <param name="gameName">Name the host gave it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task GameCreatedAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        string gameName,
        CancellationToken cancellationToken) =>
        NotifyAsync(lobbyIdentifier, characterIdentifier, gameName, created: true, cancellationToken);

    /// <summary>Reports that a player entered a game.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character that joined.</param>
    /// <param name="gameName">Name of the game that was joined.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task GameJoinedAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        string gameName,
        CancellationToken cancellationToken) =>
        NotifyAsync(lobbyIdentifier, characterIdentifier, gameName, created: false, cancellationToken);

    /// <summary>Builds one notification and hands it to every destination.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character behind the game.</param>
    /// <param name="gameName">Name of the game.</param>
    /// <param name="created">Whether the game was created rather than joined.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task NotifyAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        string gameName,
        bool created,
        CancellationToken cancellationToken)
    {
        var notification = new GameActivityNotification(
            lobbyIdentifier,
            presence.GetLobbyName(lobbyIdentifier),
            await CharacterNameAsync(characterIdentifier, cancellationToken),
            gameName,
            created);

        logger.LogInformation(
            "{CharacterName} {Action} game {GameName} in lobby {LobbyIdentifier}",
            notification.CharacterName,
            created ? "created" : "joined",
            gameName,
            lobbyIdentifier);

        foreach (var observer in observers)
        {
            try
            {
                await observer.GameActivityReportedAsync(notification, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A destination that fails is its own problem: the lobby that
                // reported the game is not held up by it.
                logger.LogError(
                    exception,
                    "The game activity observer {Observer} failed",
                    observer.GetType().Name);
            }
        }
    }

    /// <summary>Reads the name of a character, falling back when it is unknown.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<string> CharacterNameAsync(
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        try
        {
            var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
            return string.IsNullOrWhiteSpace(character?.Name)
                ? $"{UnknownCharacterName} #{characterIdentifier}"
                : character.Name;
        }
        catch (Exception exception)
        {
            // The game is the point of the event; the name is decoration.
            logger.LogWarning(
                exception,
                "The name of character {CharacterIdentifier} could not be read",
                characterIdentifier);
            return $"{UnknownCharacterName} #{characterIdentifier}";
        }
    }
}