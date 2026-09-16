using Mgo2Server.Shared.Domain.Characters;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Keeps the coordinator's player count in step with the presence events the
/// gameplay lobbies report, and tells every observer about the changes: a
/// character that connected or disconnected, and a total that moved because a
/// whole lobby went away.
/// </summary>
/// <param name="presence">Counts the coordinator owns.</param>
/// <param name="characterService">Service the character names are read from.</param>
/// <param name="observers">Destinations the changes are reported to.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class PlayerPresenceNotificationService(
    LobbyPresenceService presence,
    CharacterService characterService,
    IEnumerable<IPlayerPresenceObserver> observers,
    ILogger<PlayerPresenceNotificationService> logger)
{
    /// <summary>Name reported for a character the database does not know.</summary>
    private const string UnknownCharacterName = "unknown character";

    /// <summary>Replaces the population of a lobby with the one it registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="lobbyName">Name the lobby reports itself under.</param>
    /// <param name="characterIdentifiers">Characters connected to it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RegisterLobbyAsync(
        int lobbyIdentifier,
        string lobbyName,
        IEnumerable<int> characterIdentifiers,
        CancellationToken cancellationToken)
    {
        var totalPlayers = presence.RegisterLobby(lobbyIdentifier, lobbyName, characterIdentifiers);
        logger.LogInformation(
            "Lobby {LobbyIdentifier} ({LobbyName}) is connected with {PlayerCount} players; {TotalPlayers} in total",
            lobbyIdentifier,
            lobbyName,
            presence.GetPlayerCount(lobbyIdentifier),
            totalPlayers);

        await NotifyTotalAsync(totalPlayers, cancellationToken);
    }

    /// <summary>Records that a character entered a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task PlayerConnectedAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken) =>
        ApplyPresenceAsync(lobbyIdentifier, characterIdentifier, connected: true, cancellationToken);

    /// <summary>Records that a character left a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task PlayerDisconnectedAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken) =>
        ApplyPresenceAsync(lobbyIdentifier, characterIdentifier, connected: false, cancellationToken);

    /// <summary>
    /// Forgets a lobby and the players it held, which is what a stream that
    /// ends means: the lobby is not reachable any more, so nobody is in it.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RemoveLobbyAsync(int lobbyIdentifier, CancellationToken cancellationToken)
    {
        if (!presence.RemoveLobby(lobbyIdentifier, out var totalPlayers))
        {
            return;
        }

        logger.LogInformation(
            "Lobby {LobbyIdentifier} is no longer connected; {TotalPlayers} players in total",
            lobbyIdentifier,
            totalPlayers);

        await NotifyTotalAsync(totalPlayers, cancellationToken);
    }

    /// <summary>Turns one presence event into a count and a notification.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="connected">Whether the character connected rather than left.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ApplyPresenceAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        bool connected,
        CancellationToken cancellationToken)
    {
        int totalPlayers;
        var changed = connected
            ? presence.AddPlayer(lobbyIdentifier, characterIdentifier, out totalPlayers)
            : presence.RemovePlayer(lobbyIdentifier, characterIdentifier, out totalPlayers);

        if (!changed)
        {
            // A lobby reports a departure twice when the client leaves the lobby
            // and the socket is torn down right after it. Only the first one is
            // a player leaving.
            logger.LogDebug(
                "Lobby {LobbyIdentifier} reported character {CharacterIdentifier} as {State} twice; ignored",
                lobbyIdentifier,
                characterIdentifier,
                connected ? "connected" : "disconnected");
            return;
        }

        var notification = new PlayerPresenceNotification(
            lobbyIdentifier,
            presence.GetLobbyName(lobbyIdentifier),
            characterIdentifier,
            await CharacterNameAsync(characterIdentifier, cancellationToken),
            connected,
            totalPlayers);

        logger.LogInformation(
            "{CharacterName} {State} lobby {LobbyIdentifier}; {TotalPlayers} players in total",
            notification.CharacterName,
            connected ? "connected to" : "disconnected from",
            lobbyIdentifier,
            totalPlayers);

        foreach (var observer in observers)
        {
            try
            {
                await observer.PlayerPresenceChangedAsync(notification, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A destination that fails is its own problem: the coordinator
                // and the lobbies carry on counting.
                logger.LogError(
                    exception,
                    "The player presence observer {Observer} failed",
                    observer.GetType().Name);
            }
        }
    }

    /// <summary>Reports a total that moved on its own.</summary>
    /// <param name="totalPlayers">Total after the change.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task NotifyTotalAsync(int totalPlayers, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PlayerTotalChangedAsync(totalPlayers, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "The player presence observer {Observer} failed",
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
            // The count is the point of the event; the name is decoration.
            logger.LogWarning(
                exception,
                "The name of character {CharacterIdentifier} could not be read",
                characterIdentifier);
            return $"{UnknownCharacterName} #{characterIdentifier}";
        }
    }
}
