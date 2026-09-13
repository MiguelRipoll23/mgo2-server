using Mgo2Server.Shared.Errors;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>Lobby as published to clients.</summary>
/// <param name="Identifier">Identifier of the lobby.</param>
/// <param name="Type">Kind of service the lobby describes.</param>
/// <param name="SubtypeIdentifier">Game type of the lobby.</param>
/// <param name="Name">Display name of the lobby.</param>
/// <param name="IpAddress">Address clients are told to connect to.</param>
/// <param name="Port">Port the lobby listens on.</param>
/// <param name="PlayersCount">Number of players currently in the lobby.</param>
/// <param name="BeginnerOnly">Whether the lobby only accepts beginners.</param>
/// <param name="ExpansionOnly">Whether the lobby only accepts expansion owners.</param>
/// <param name="NoHeadshot">Whether the lobby disables headshots.</param>
/// <param name="ReplaysOnly">Whether the lobby only accepts replays.</param>
public sealed record LobbyResponse(
    int Identifier,
    LobbyType Type,
    int SubtypeIdentifier,
    string Name,
    string IpAddress,
    int Port,
    int PlayersCount,
    bool BeginnerOnly,
    bool ExpansionOnly,
    bool NoHeadshot,
    bool ReplaysOnly);

/// <summary>Fields accepted when a lobby is created.</summary>
/// <param name="Type">Kind of service the lobby describes.</param>
/// <param name="SubtypeIdentifier">Game type of the lobby.</param>
/// <param name="Name">Display name of the lobby.</param>
/// <param name="IpAddress">Address clients are told to connect to.</param>
/// <param name="Port">Port the lobby listens on.</param>
/// <param name="PlayersCount">Initial player count.</param>
public sealed record LobbyCreateInput(
    LobbyType Type,
    string Name,
    string IpAddress,
    int Port,
    int SubtypeIdentifier = 1,
    int PlayersCount = 0);

/// <summary>
/// Owns the lobby configuration and its in-memory cache. The cache is rebuilt
/// from the database periodically, and player counts are summed across every
/// non-stale instance so horizontally scaled servers publish a shared total.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="options">Server options.</param>
/// <param name="gameTypeService">Service that resolves the game type of a published lobby.</param>
public sealed partial class LobbyService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    IOptions<ServerOptions> options,
    LobbyGameTypeService gameTypeService) : DomainService(contextFactory)
{
    private readonly ServerOptions options = options.Value;
    private readonly LobbyGameTypeService gameTypeService = gameTypeService;
    private List<LobbyResponse>? cache;

    /// <summary>Replaces the cache with a set of rows.</summary>
    /// <param name="rows">Lobbies to cache.</param>
    public void SetCache(IReadOnlyList<LobbyResponse> rows) => cache = [.. rows];

    /// <summary>
    /// Returns the cached lobbies, or an empty list when the cache has not been
    /// loaded yet. Returning empty rather than throwing keeps every caller's
    /// failure mode the same on a startup-order change.
    /// </summary>
    public IReadOnlyList<LobbyResponse> GetCached() => cache ?? [];

    /// <summary>
    /// Reloads the cache from the database. Rows are ordered by identifier, not
    /// by name, because the client expects the list index and the lobby type to
    /// coincide. A gameplay lobby is cached only while its heartbeat is recent,
    /// so a lobby whose server stopped stops being served; the gate and the
    /// account server are permanent and always cached.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-options.LobbyStaleSeconds);

        var rows = await context.Lobbies
            .AsNoTracking()
            .Where(lobby => lobby.Type != LobbyType.Game || lobby.UpdatedAt > cutoff)
            .OrderBy(lobby => lobby.Identifier)
            .ToListAsync(cancellationToken);

        cache = [.. rows.Select(lobby => ToResponse(lobby, null))];
    }

    /// <summary>Lists every lobby.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<LobbyResponse>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await context.Lobbies
            .AsNoTracking()
            .OrderBy(lobby => lobby.Identifier)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(lobby => ToResponse(lobby, null))];
    }

    /// <summary>Finds one lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the lobby does not exist.</exception>
    public async Task<LobbyResponse> FindByIdAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var lobby = await context.Lobbies
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Identifier == lobbyIdentifier, cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "Lobby not found", 404);
        return ToResponse(lobby, null);
    }

    /// <summary>Creates a lobby.</summary>
    /// <param name="input">Fields of the new lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<LobbyResponse> CreateAsync(LobbyCreateInput input, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var lobby = new Lobby
        {
            Type = input.Type,
            Name = input.Name,
            IpAddress = input.IpAddress,
            Port = input.Port,
            SubtypeIdentifier = input.SubtypeIdentifier,
            PlayersCount = input.PlayersCount,
        };

        context.Lobbies.Add(lobby);
        await context.SaveChangesAsync(cancellationToken);
        return ToResponse(lobby, null);
    }

    /// <summary>Updates a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="update">Changes to apply.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the lobby does not exist.</exception>
    public async Task<LobbyResponse> UpdateAsync(
        int lobbyIdentifier,
        Action<Lobby> update,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var lobby = await context.Lobbies
            .FirstOrDefaultAsync(row => row.Identifier == lobbyIdentifier, cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "Lobby not found", 404);

        update(lobby);
        await context.SaveChangesAsync(cancellationToken);
        return ToResponse(lobby, null);
    }

    /// <summary>Deletes a lobby.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the lobby does not exist.</exception>
    public async Task RemoveAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var removed = await context.Lobbies
            .Where(lobby => lobby.Identifier == lobbyIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed == 0)
        {
            throw new ServerException("NOT_FOUND", "Lobby not found", 404);
        }
    }

    /// <summary>Stores the player count of a lobby directly.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="count">Player count to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task UpdatePlayerCountAsync(
        int lobbyIdentifier,
        int count,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Lobbies
            .Where(lobby => lobby.Identifier == lobbyIdentifier)
            .ExecuteUpdateAsync(setters => setters.SetProperty(lobby => lobby.PlayersCount, count), cancellationToken);
    }

    private LobbyResponse ToResponse(Lobby lobby, int? playerCount)
    {
        var ipAddress = options.OverrideIpAddress ?? lobby.IpAddress;
        return new LobbyResponse(
            lobby.Identifier,
            lobby.Type,
            lobby.SubtypeIdentifier,
            lobby.Name,
            ipAddress,
            lobby.Port,
            playerCount ?? lobby.PlayersCount,
            lobby.BeginnerOnly,
            lobby.ExpansionOnly,
            lobby.NoHeadshot,
            lobby.ReplaysOnly);
    }
}
