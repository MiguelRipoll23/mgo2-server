using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Shared.Domain.Automatch;

/// <summary>
/// Bridges the automatch queue to the game layer: the two questions the
/// matchmaker has to ask the domain that owns the rooms, rather than reading the
/// rooms itself.
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
public sealed class AutomatchHooksService(GameService gameService) : IAutomatchHooks
{
    /// <inheritdoc />
    public async Task<int> FindHostedGameIdentifierAsync(
        int lobbyIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        var games = await gameService.FindByLobbyAsync(lobbyIdentifier, cancellationToken);
        return games.FirstOrDefault(game => game.HostIdentifier == characterIdentifier)?.Identifier ?? 0;
    }

    /// <inheritdoc />
    public Task RenameAndUnlockAsync(
        int gameIdentifier,
        string name,
        CancellationToken cancellationToken) =>
        gameService.UpdateAsync(
            gameIdentifier,
            room =>
            {
                room.Name = name;
                // A password on an automatch room would lock out the group the
                // matchmaker has just put in it.
                room.Password = string.Empty;
            },
            cancellationToken);
}
