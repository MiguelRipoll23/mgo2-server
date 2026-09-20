using Mgo2Server.Shared.Errors;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// <param name="BeginnersOnly">Whether the lobby only accepts beginners.</param>
/// <param name="ExpansionRequired">Whether the lobby only accepts expansion owners.</param>
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
    bool BeginnersOnly,
    bool ExpansionRequired,
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
/// Owns the lobby configuration and its in-memory cache. The cache is read from
/// the database when it is asked for and held for a lifetime of its own, so an
/// instance that nobody is listing reads nothing, and player counts are summed
/// across every non-stale instance so horizontally scaled servers publish a
/// shared total.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="options">Server options.</param>
/// <param name="gameTypeService">Service that resolves the game type of a published lobby.</param>
/// <param name="logger">Logger of the service.</param>
public sealed partial class LobbyService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    IOptions<ServerOptions> options,
    LobbyGameTypeService gameTypeService,
    ILogger<LobbyService> logger) : DomainService(contextFactory)
{
    private readonly ServerOptions options = options.Value;
    private readonly LobbyGameTypeService gameTypeService = gameTypeService;
    private readonly SemaphoreSlim cacheGate = new(1, 1);
    private List<LobbyResponse>? cache;
    private DateTimeOffset cacheReadAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Returns the lobbies as of the last read, without reading them. For a caller
    /// that wants a list it can afford to be late — a label, a fallback — rather
    /// than the list a client is waiting for.
    /// </summary>
    public IReadOnlyList<LobbyResponse> GetCached() => cache ?? [];

    /// <summary>
    /// Returns the published lobbies, reading them from the database when the list
    /// that is held has reached the end of its life.
    /// <para>
    /// The list is read when it is asked for rather than kept warm on a timer, so
    /// an instance nobody is listing reads nothing at all, and one read serves
    /// every caller that arrives while it is being made.
    /// </para>
    /// <para>
    /// A read that fails is answered with the list already held. It is a few
    /// minutes old at most, it still names lobbies that are running, and it is what
    /// the client can still connect to — where an error would leave it with no
    /// lobbies at all.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<IReadOnlyList<LobbyResponse>> GetLobbiesAsync(CancellationToken cancellationToken = default)
    {
        if (cache is { } held && IsFresh())
        {
            return held;
        }

        await cacheGate.WaitAsync(cancellationToken);
        try
        {
            // Read again under the gate: the wait may have been for a read that a
            // caller ahead of this one has just finished.
            if (cache is { } reloaded && IsFresh())
            {
                return reloaded;
            }

            cache = await ReadLobbiesAsync(cancellationToken);
            cacheReadAt = DateTimeOffset.UtcNow;
            return cache;
        }
        catch (Exception exception) when (cache is not null && exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "The lobby list could not be read; serving the one read {Age}s ago",
                (int)(DateTimeOffset.UtcNow - cacheReadAt).TotalSeconds);
            return cache;
        }
        finally
        {
            cacheGate.Release();
        }
    }

    /// <summary>
    /// Reads every published lobby. Rows are ordered by type before identifier,
    /// because the client expects the first two list entries to be the permanent
    /// endpoints: the gate first and the account server second, however the
    /// identifiers of those rows were generated, followed by the gameplay lobbies.
    /// A gameplay lobby is listed only while its heartbeat is recent, so a lobby
    /// whose server stopped stops being served; the gate and the account server are
    /// permanent and always listed.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<List<LobbyResponse>> ReadLobbiesAsync(CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await StillServed(context.Lobbies.AsNoTracking())
            .OrderBy(lobby => lobby.Type)
            .ThenBy(lobby => lobby.Identifier)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(lobby => ToResponse(lobby, null))];
    }

    /// <summary>
    /// Narrows a lobby query to the rows that are still being served: everything
    /// except a gameplay lobby whose heartbeat has aged out. The gate and the
    /// account server are permanent endpoints and are always listed.
    /// <para>
    /// Every list read goes through this, and it is the reason a stale lobby is
    /// never shown even though its row is only deleted once a day: the window, not
    /// the delete, is what keeps a lobby whose server stopped off a screen.
    /// </para>
    /// </summary>
    /// <param name="lobbies">Query to narrow.</param>
    private IQueryable<Lobby> StillServed(IQueryable<Lobby> lobbies)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-options.LobbyStaleSeconds);
        return lobbies.Where(lobby => lobby.Type != LobbyType.Game || lobby.UpdatedAt > cutoff);
    }

    /// <summary>
    /// Whether the list that is held is still within its life. That life is the
    /// heartbeat interval, which is the rate at which the rows themselves can
    /// change: a gameplay lobby that stopped is not dropped until its heartbeat ages
    /// out, and a list read between two heartbeats finds the same rows.
    /// </summary>
    private bool IsFresh() =>
        DateTimeOffset.UtcNow - cacheReadAt < TimeSpan.FromSeconds(options.LobbyHeartbeatIntervalSeconds);

    /// <summary>
    /// Lists every lobby that is still being served, ordered by identifier. This
    /// is the HTTP view of the list the gate builds, so it applies the same
    /// window: a gameplay lobby whose <c>updated_at</c> has left the stale window
    /// is not returned, whether or not the daily cleanup has removed it yet.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<LobbyResponse>> FindAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await StillServed(context.Lobbies.AsNoTracking())
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
            lobby.BeginnersOnly,
            lobby.ExpansionRequired,
            lobby.NoHeadshot,
            lobby.ReplaysOnly);
    }
}
