using System.Text.Json;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// Owns the match a dedicated host publishes in its lobby. The row is created
/// on the first run, heartbeated so that it stays in the room list, and the
/// matches of the hosts that stopped are expired, which is what removes a
/// dedicated host that was killed without shutting down from the list.
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="accountService">Service that owns the dedicated host account.</param>
/// <param name="hostIdentity">Identity the dedicated host presents.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class DedicatedHostMatchService(
    GameService gameService,
    LobbyService lobbyService,
    DedicatedHostAccountService accountService,
    HostIdentityService hostIdentity,
    IOptions<ServerOptions> options,
    ILogger<DedicatedHostMatchService> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.LobbyHeartbeatIntervalSeconds), logger)
{
    /// <summary>How long to wait before looking again for the lobby of the match.</summary>
    private static readonly TimeSpan LobbyLookupRetryDelay = TimeSpan.FromSeconds(5);

    private readonly ServerOptions options = options.Value;
    private readonly int port = options.Value.DedicatedHostPort;
    private int matchIdentifier;

    /// <summary>Identifier of the match this host published, or zero until it exists.</summary>
    public int MatchIdentifier => matchIdentifier;

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await accountService.EnsureAccountAsync((int)hostIdentity.PeerIdentifier, cancellationToken);

        while (matchIdentifier == 0)
        {
            matchIdentifier = await EnsureMatchAsync(cancellationToken);

            if (matchIdentifier == 0)
            {
                // The lobby this host publishes in is owned by a game lobby
                // server, which may still be starting, so the lookup is retried
                // instead of ending the host.
                logger.LogWarning(
                    "No gameplay lobby named '{LobbyName}' has been published yet; retrying in {Delay}",
                    options.DedicatedHostLobbyName,
                    LobbyLookupRetryDelay);

                await Task.Delay(LobbyLookupRetryDelay, cancellationToken);
            }
        }

        await gameService.HeartbeatAsync(matchIdentifier, cancellationToken);

        var expired = await gameService.RemoveStaleHostedMatchesAsync(
            (int)hostIdentity.PeerIdentifier,
            TimeSpan.FromSeconds(options.LobbyStaleSeconds),
            cancellationToken);

        if (expired > 0)
        {
            logger.LogInformation("Expired {Count} stale dedicated-host match(es)", expired);
        }
    }

    /// <summary>
    /// Finds the lobby this host publishes in and creates the match when it is
    /// missing, so restarting the container reuses the match it left behind.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Identifier of the match, or zero while the lobby is missing.</returns>
    private async Task<int> EnsureMatchAsync(CancellationToken cancellationToken)
    {
        var lobby = (await lobbyService.FindActiveGameLobbiesAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Name == options.DedicatedHostLobbyName);

        if (lobby is null)
        {
            return 0;
        }

        var name = $"server-{port}";
        var hostIdentifier = (int)hostIdentity.PeerIdentifier;
        var existing = (await gameService.FindByLobbyAsync(lobby.Identifier, cancellationToken))
            .FirstOrDefault(game => game.HostIdentifier == hostIdentifier && game.Name == name);

        if (existing is not null)
        {
            return existing.Identifier;
        }

        var game = await gameService.CreateAsync(room =>
        {
            room.HostIdentifier = hostIdentifier;
            room.LobbyIdentifier = lobby.Identifier;
            room.Name = name;
            room.Password = string.Empty;
            room.Comment = "Dedicated host";
            room.MaximumPlayers = 8;
            room.Games = JsonSerializer.Serialize(new[] { new[] { 1, 0, 0 } });
        }, cancellationToken);

        await gameService.AddPlayerAsync(game.Identifier, hostIdentifier, cancellationToken);
        logger.LogInformation(
            "Created match {GameIdentifier} ({Name}) in lobby {LobbyIdentifier}",
            game.Identifier,
            name,
            lobby.Identifier);

        return game.Identifier;
    }
}
