using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The round half of the game service: the latency the host reports and the
/// roster snapshot that end-of-round statistics are attributed against.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Returns the reported ping of every player in a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Dictionary<int, int>> GetPlayerPingsAsync(
        int gameIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GamePlayers
            .AsNoTracking()
            .Where(player => player.GameIdentifier == gameIdentifier)
            .ToDictionaryAsync(player => player.CharacterIdentifier, player => player.Ping, cancellationToken);
    }

    /// <summary>
    /// Stores the host's latency report: its own ping on the room row, which the
    /// browser shows, and each player's ping on their roster row, which the
    /// player list shows.
    /// </summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="hostPing">Ping reported for the host.</param>
    /// <param name="playerPings">Ping reported for each player.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdatePingsAsync(
        int gameIdentifier,
        int hostPing,
        IReadOnlyDictionary<int, int> playerPings,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Games
            .Where(game => game.Identifier == gameIdentifier)
            .ExecuteUpdateAsync(setters => setters.SetProperty(game => game.Ping, hostPing), cancellationToken);

        foreach (var (characterIdentifier, ping) in playerPings)
        {
            if (characterIdentifier <= 0)
            {
                continue;
            }

            var clampedPing = Math.Clamp(ping, 0, 0xffff);
            await context.GamePlayers
                .Where(player => player.GameIdentifier == gameIdentifier && player.CharacterIdentifier == characterIdentifier)
                .ExecuteUpdateAsync(setters => setters.SetProperty(player => player.Ping, clampedPing), cancellationToken);
        }
    }

    /// <summary>
    /// Snapshots the roster at round start. Insert-only: rows accumulate
    /// everyone who has been in the room, so the statistics of a player who
    /// left mid-round still apply.
    /// </summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task MarkRoundPlayersAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var roster = await context.GamePlayers
            .AsNoTracking()
            .Where(player => player.GameIdentifier == gameIdentifier)
            .Select(player => player.CharacterIdentifier)
            .ToListAsync(cancellationToken);

        if (roster.Count == 0)
        {
            return;
        }

        // Both values come from the database as integers, so the statement
        // cannot carry anything but identifiers.
        var values = string.Join(
            ", ",
            roster.Select(characterIdentifier => $"({gameIdentifier}, {characterIdentifier})"));
        var statement = "INSERT INTO game_rounds (game_id, character_id) VALUES " + values +
                        " ON CONFLICT DO NOTHING";

        await context.Database.ExecuteSqlRawAsync(statement, cancellationToken);
    }

    /// <summary>Returns whether a character was in the room when a round started.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> PlayedLastRoundAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GameRounds
            .AsNoTracking()
            .AnyAsync(
                round => round.GameIdentifier == gameIdentifier && round.CharacterIdentifier == characterIdentifier,
                cancellationToken);
    }

    /// <summary>
    /// Roster or round-snapshot membership. This is the attribution check for
    /// host-reported statistics: the host reports on behalf of everyone, so the
    /// character identifier carried by the packet must be validated against the
    /// room rather than trusted.
    /// </summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="hostCharacterIdentifier">Identifier of the hosting character.</param>
    /// <param name="characterIdentifier">Identifier of the reported character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> IsInGameAsync(
        int gameIdentifier,
        int hostCharacterIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return false;
        }

        if (characterIdentifier == hostCharacterIdentifier)
        {
            return true;
        }

        if (await IsOnRosterAsync(gameIdentifier, characterIdentifier, cancellationToken))
        {
            return true;
        }

        return await PlayedLastRoundAsync(gameIdentifier, characterIdentifier, cancellationToken);
    }

    private async Task<bool> IsOnRosterAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GamePlayers
            .AsNoTracking()
            .AnyAsync(
                player => player.GameIdentifier == gameIdentifier && player.CharacterIdentifier == characterIdentifier,
                cancellationToken);
    }
}
