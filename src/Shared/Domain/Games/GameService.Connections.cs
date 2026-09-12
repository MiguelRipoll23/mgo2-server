using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The connection half of the game service: the peer-to-peer endpoints the
/// players register and the roster of each room.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Stores the peer-to-peer endpoint of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="information">Endpoint to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SaveConnectionInformationAsync(
        int characterIdentifier,
        ConnectionInformation information,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var updatedAt = DateTimeOffset.UtcNow;

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO character_connections
                 (character_id, public_ip, public_port, private_ip, private_port, updated_at)
             VALUES
                 ({characterIdentifier}, {information.PublicIpAddress}, {information.PublicPort},
                  {information.PrivateIpAddress}, {information.PrivatePort}, {updatedAt})
             ON CONFLICT (character_id)
             DO UPDATE SET public_ip = {information.PublicIpAddress},
                           public_port = {information.PublicPort},
                           private_ip = {information.PrivateIpAddress},
                           private_port = {information.PrivatePort},
                           updated_at = {updatedAt}
             """,
            cancellationToken);
    }

    /// <summary>Returns the peer-to-peer endpoint of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<ConnectionInformation?> GetConnectionInformationAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var connection = await context.CharacterConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.CharacterIdentifier == characterIdentifier, cancellationToken);

        return connection is null
            ? null
            : new ConnectionInformation(
                connection.PublicIpAddress,
                connection.PublicPort,
                connection.PrivateIpAddress,
                connection.PrivatePort);
    }

    /// <summary>Adds a character to the roster of a room; the host is always a member of its own room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task AddPlayerAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO game_players (game_id, character_id)
             VALUES ({gameIdentifier}, {characterIdentifier})
             ON CONFLICT (game_id, character_id) DO NOTHING
             """,
            cancellationToken);
    }

    /// <summary>Removes a character from the roster of a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RemovePlayerAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.GamePlayers
            .Where(player => player.GameIdentifier == gameIdentifier && player.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Returns the roster of a room, host first, which is the order the player list expects.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="hostCharacterIdentifier">Identifier of the hosting character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<int>> GetPlayersAsync(
        int gameIdentifier,
        int hostCharacterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var players = await context.GamePlayers
            .AsNoTracking()
            .Where(player => player.GameIdentifier == gameIdentifier)
            .Select(player => player.CharacterIdentifier)
            .ToListAsync(cancellationToken);

        var others = players.Where(identifier => identifier != hostCharacterIdentifier).ToList();
        return hostCharacterIdentifier > 0 ? [hostCharacterIdentifier, .. others] : others;
    }

    /// <summary>Counts the players in a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> CountPlayersAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GamePlayers
            .AsNoTracking()
            .CountAsync(player => player.GameIdentifier == gameIdentifier, cancellationToken);
    }
}
