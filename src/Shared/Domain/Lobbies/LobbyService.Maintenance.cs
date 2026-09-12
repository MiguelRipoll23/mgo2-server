using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// The lifecycle half of the lobby service: the row a game lobby server
/// registers for itself, the heartbeat that keeps it alive and the cleanup that
/// removes the rows of the servers that stopped.
/// </summary>
public sealed partial class LobbyService
{
    /// <summary>
    /// Registers the gameplay lobby this instance hosts. The row is keyed by
    /// port, so restarting a container refreshes its own row instead of
    /// accumulating one per start, and a lobby whose port changed leaves its old
    /// row behind to be cleaned up as stale.
    /// </summary>
    /// <param name="lobby">Identity and attributes of the hosted lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the configuration or the subtype is invalid.</exception>
    public async Task<LobbyResponse> RegisterGameLobbyAsync(
        LobbyOptions lobby,
        CancellationToken cancellationToken = default)
    {
        lobby.Validate();
        var gameType = await gameTypeService.ResolveAsync(lobby.Subtype, cancellationToken);

        await using var context = await CreateContextAsync(cancellationToken);
        var registered = await context.Lobbies
            .FirstOrDefaultAsync(row => row.Port == lobby.Port, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (registered is null)
        {
            registered = new Lobby
            {
                Type = LobbyType.Game,
                SubtypeIdentifier = gameType.Identifier,
                Name = lobby.Name,
                IpAddress = options.ListeningIpAddress,
                Port = lobby.Port,
                CreatedAt = now,
                UpdatedAt = now,
            };

            context.Lobbies.Add(registered);
        }

        registered.Type = LobbyType.Game;
        registered.SubtypeIdentifier = gameType.Identifier;
        registered.Name = lobby.Name;
        registered.IpAddress = options.ListeningIpAddress;
        registered.BeginnerOnly = lobby.BeginnerOnly;
        registered.ExpansionOnly = lobby.ExpansionOnly;
        registered.NoHeadshot = lobby.NoHeadshot;
        registered.ReplaysOnly = lobby.ReplaysOnly;
        registered.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(registered, null);
    }

    /// <summary>Refreshes the heartbeat of a lobby, which keeps it served.</summary>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HeartbeatAsync(int lobbyIdentifier, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Lobbies
            .Where(lobby => lobby.Identifier == lobbyIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(lobby => lobby.UpdatedAt, now),
                cancellationToken);
    }

    /// <summary>
    /// Lists the gameplay lobbies whose heartbeat is recent. The gate publishes
    /// these, and a gameplay server picks the lobby its match belongs to among them.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<LobbyResponse>> FindActiveGameLobbiesAsync(
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-options.LobbyStaleSeconds);

        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await context.Lobbies
            .AsNoTracking()
            .Where(lobby => lobby.Type == LobbyType.Game && lobby.UpdatedAt > cutoff)
            .OrderBy(lobby => lobby.Identifier)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(lobby => ToResponse(lobby, null))];
    }

    /// <summary>
    /// Deletes the gameplay lobbies that stopped being heartbeated. The rooms of
    /// such a lobby cascade away with it, and the characters that last selected
    /// it are detached first, because the character reference does not cascade.
    /// </summary>
    /// <param name="staleAfter">Age at which a lobby is considered abandoned.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>How many lobbies were removed.</returns>
    public async Task<int> RemoveStaleGameLobbiesAsync(
        TimeSpan staleAfter,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - staleAfter;

        await using var context = await CreateContextAsync(cancellationToken);
        var stale = await context.Lobbies
            .Where(lobby => lobby.Type == LobbyType.Game && lobby.UpdatedAt < cutoff)
            .Select(lobby => lobby.Identifier)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        await context.Characters
            .Where(character =>
                character.LobbyIdentifier != null && stale.Contains(character.LobbyIdentifier.Value))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(character => character.LobbyIdentifier, (int?)null),
                cancellationToken);

        return await context.Lobbies
            .Where(lobby => stale.Contains(lobby.Identifier))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
