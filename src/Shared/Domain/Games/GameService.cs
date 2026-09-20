using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
/// Presence-accumulated seconds a character spent in the training lobbies, split
/// the three ways the stats tail reports them. Owned here because it is measured
/// from the roster: the interval between joining a room and leaving it is the
/// whole measurement, since a training session reports nothing at all.
/// </summary>
/// <param name="TrainingMode">Seconds spent in a training lobby.</param>
/// <param name="Instructor">Seconds spent hosting a combat training session.</param>
/// <param name="Student">Seconds spent as a student in one.</param>
public readonly record struct TrainingSeconds(long TrainingMode, long Instructor, long Student);

/// <summary>
/// Owns the game rooms: their roster, the peer-to-peer endpoints of their
/// players, the round snapshots used for stat attribution and the host ratings.
/// The connection, round and rating operations live in the other halves of this
/// class.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="metricsService">Service the new match total is reported to.</param>
/// <param name="options">Server options, for the stale window a room is listed within.</param>
public sealed partial class GameService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    ServerMetricsService metricsService,
    IOptions<ServerOptions> options)
    : DomainService(contextFactory)
{
    private readonly ServerOptions options = options.Value;

    /// <summary>Lowest rating the client's star picker sends.</summary>
    public const int MinimumHostRating = 1;

    /// <summary>Highest rating the client's star picker sends.</summary>
    public const int MaximumHostRating = 5;

    /// <summary>Lists every room that is still being published.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Game>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await StillPublished(context.Games.AsNoTracking())
            .OrderBy(game => game.Identifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Lists the rooms of a lobby that are still being published.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<Game>> FindByLobbyAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await StillPublished(context.Games.AsNoTracking())
            .Where(game => game.LobbyIdentifier == lobbyIdentifier)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Narrows a room query to the rooms that are still being published: those
    /// whose host has written to them inside <see cref="ServerOptions.GameStaleSeconds"/>.
    /// A room whose host stopped is deleted by the daily cleanup, but it stops
    /// being listed the moment its <c>updated_at</c> leaves the window, so a client
    /// never waits on that delete to stop seeing a room that is gone.
    /// </summary>
    /// <param name="games">Query to narrow.</param>
    private IQueryable<Game> StillPublished(IQueryable<Game> games)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-options.GameStaleSeconds);
        return games.Where(game => game.UpdatedAt > cutoff);
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

        // The match count of the lobby changed, so its new total is published
        // rather than polled for on a timer.
        await ReportLobbyMatchesAsync(game.LobbyIdentifier, cancellationToken);
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

        // The lobby is read first, because the total it is reported under cannot
        // be known once the row is gone.
        var lobbyIdentifier = await context.Games
            .Where(game => game.Identifier == gameIdentifier)
            .Select(game => (int?)game.LobbyIdentifier)
            .FirstOrDefaultAsync(cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // The roster goes with the room, so everyone still on it is credited first:
        // a room torn down with players in it — the host quitting, most often — is
        // exactly the case a presence counter is for.
        await CreditTrainingTimeAsync(context, [gameIdentifier], 0, cancellationToken);

        await context.Games
            .Where(game => game.Identifier == gameIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (lobbyIdentifier is { } lobby)
        {
            await ReportLobbyMatchesAsync(lobby, cancellationToken);
        }
    }

    /// <summary>Deletes every room of a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task ClearLobbyGamesAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Read the roster's rooms before they go: the presence they hold is the only
        // reason this pass exists.
        var gameIdentifiers = await GameIdentifiersOfLobbyAsync(context, lobbyIdentifier, cancellationToken);
        await CreditTrainingTimeAsync(context, gameIdentifiers, 0, cancellationToken);

        await context.Games
            .Where(game => game.LobbyIdentifier == lobbyIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        await ReportLobbyMatchesAsync(lobbyIdentifier, cancellationToken);
    }

    /// <summary>
    /// Counts the matches of one lobby and publishes the new total, labelled with
    /// the lobby so a dashboard can filter the metric by lobby.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task ReportLobbyMatchesAsync(int lobbyIdentifier, CancellationToken cancellationToken)
    {
        var lobby = await FindLobbyAsync(lobbyIdentifier, cancellationToken);
        await metricsService.ReportTotalMatchesAsync(lobbyIdentifier, lobby?.Name, cancellationToken);
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
