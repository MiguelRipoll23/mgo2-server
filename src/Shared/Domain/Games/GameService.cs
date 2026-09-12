using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>Peer-to-peer endpoint a client listens on.</summary>
/// <param name="PublicIpAddress">Address as seen by the server.</param>
/// <param name="PublicPort">Public port.</param>
/// <param name="PrivateIpAddress">Address the client reported for its local network.</param>
/// <param name="PrivatePort">Port on the local network.</param>
public sealed record ConnectionInformation(
    string PublicIpAddress,
    int PublicPort,
    string PrivateIpAddress,
    int PrivatePort);

/// <summary>Lobby summary used when a report is stamped or a label is derived.</summary>
/// <param name="Identifier">Identifier of the lobby.</param>
/// <param name="SubtypeIdentifier">Game type of the lobby.</param>
/// <param name="Name">Display name of the lobby.</param>
public sealed record LobbySummary(int Identifier, int SubtypeIdentifier, string Name);

/// <summary>Aggregated host ratings of one character.</summary>
/// <param name="RatingSum">Sum of the ratings the host received.</param>
/// <param name="Votes">Number of votes the host received.</param>
public readonly record struct HostRatingSummary(int RatingSum, int Votes);

/// <summary>
/// Owns the game rooms: their roster, the peer-to-peer endpoints of their
/// players, the round snapshots used for stat attribution and the host ratings.
/// The connection, round and rating operations live in the other halves of this
/// class.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed partial class GameService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Lowest rating the client's star picker sends.</summary>
    public const int MinimumHostRating = 1;

    /// <summary>Highest rating the client's star picker sends.</summary>
    public const int MaximumHostRating = 5;

    /// <summary>Lists every room.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Game>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Games
            .AsNoTracking()
            .OrderBy(game => game.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists the rooms of a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Game>> FindByLobbyAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Games
            .AsNoTracking()
            .Where(game => game.LobbyIdentifier == lobbyIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Finds a room by its identifier.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Game?> FindByIdAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(game => game.Identifier == gameIdentifier, cancellationToken);
    }

    /// <summary>Creates a room.</summary>
    /// <param name="configure">Applies the fields of the new room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Game> CreateAsync(Action<Game> configure, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var game = new Game { Name = string.Empty };
        configure(game);
        context.Games.Add(game);
        await context.SaveChangesAsync(cancellationToken);
        return game;
    }

    /// <summary>Updates a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="update">Changes to apply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The updated room, or <c>null</c> when it does not exist.</returns>
    public async Task<Game?> UpdateAsync(
        int gameIdentifier,
        Action<Game> update,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var game = await context.Games
            .FirstOrDefaultAsync(row => row.Identifier == gameIdentifier, cancellationToken);

        if (game is null)
        {
            return null;
        }

        update(game);
        await context.SaveChangesAsync(cancellationToken);
        return game;
    }

    /// <summary>Deletes a room. The roster and round snapshots cascade with it; votes outlive it.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task DeleteAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Games
            .Where(game => game.Identifier == gameIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Deletes every room of a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task ClearLobbyGamesAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Games
            .Where(game => game.LobbyIdentifier == lobbyIdentifier)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Returns the lobby summary behind an identifier.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<LobbySummary?> FindLobbyAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Lobbies
            .AsNoTracking()
            .Where(lobby => lobby.Identifier == lobbyIdentifier)
            .Select(lobby => new LobbySummary(lobby.Identifier, lobby.SubtypeIdentifier, lobby.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Returns the room a character is currently in, whichever slot it occupies.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Game?> GameContainingAsync(int characterIdentifier, CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return null;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GamePlayers
            .AsNoTracking()
            .Where(player => player.CharacterIdentifier == characterIdentifier)
            .Join(
                context.Games,
                player => player.GameIdentifier,
                game => game.Identifier,
                (_, game) => game)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
