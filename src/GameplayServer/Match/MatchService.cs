using System.Text.Json;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// Owns the game a gameplay server publishes in its lobby. The row is created
/// on the first run and heartbeated so that it stays in the room list. Stale
/// game cleanup is handled centrally by the game lobby server.
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="accountService">Service that owns the gameplay server account.</param>
/// <param name="hostIdentity">Identity the gameplay server presents.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class MatchService(
    GameService gameService,
    LobbyService lobbyService,
    AccountService accountService,
    HostIdentityService hostIdentity,
    IOptions<ServerOptions> options,
    ILogger<MatchService> logger)
    : PeriodicWorker(
        TimeSpan.FromSeconds(options.Value.MatchHeartbeatIntervalSeconds),
        logger,
        PeriodicWorker.NoBackoff)
{
    /// <summary>How long to wait before looking again for the lobby of the match.</summary>
    private static readonly TimeSpan LobbyLookupRetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The rule this host's room runs, which is the client's Free Battle: the
    /// one rule every client can play and the one the dedicated host exists to
    /// offer. It is the same number the client's own join command defaults to
    /// (`docs/BUILD_1_36.md`, "0x4320's third field").
    /// </summary>
    private const int FreeBattleRule = 1;

    /// <summary>
    /// Prefix of a character name that marks its room as a dedicated host. A
    /// gameplay server's character is named for what it is, and the name is the
    /// only record the room needs: the room it publishes is built from the name
    /// rather than from a settings row the host would otherwise have to write.
    /// </summary>
    private const string DedicatedCharacterNamePrefix = "server";

    /// <summary>
    /// The single round this host's room advertises, as a `[rule, map, flag]`
    /// triple with the map drawn from the disc's five shipping stages.
    /// <para>
    /// The map used to be written as zero, and a zero map is not a map. The
    /// client reads the triple its rotation index names and, per
    /// `docs/AUTOMATCH.md`, **discards the index and falls back to entry 0 when
    /// `maps[idx] == 0`** — so the one entry that named no map named no map by
    /// the only route a joiner is given, since the map reaches the joiner over
    /// TCP with the rest of the room rather than over the peer session
    /// (`docs/protocol/UDP_P2P_PROTOCOL.md`). Zero is the client's own literal
    /// for "None" (`maps.h`, index 0 of the display table), so the room was
    /// advertising itself as being on no map at all.
    /// </para>
    /// <para>
    /// It is one entry rather than a rotation because the client requires the
    /// list to be contiguous from index 0 with nonzero maps, and a host that
    /// never changes map has nothing to rotate through. The pool is automatch's
    /// own — the five stages the disc ships and the only ones any capture has
    /// confirmed loading — so a dedicated host offers the same maps the queue
    /// does, and the draw is per process rather than per room: the row is
    /// created once and then heartbeated for the life of the container, so a
    /// room-level draw would never be seen again after the first.
    /// </para>
    /// </summary>
    private static int[][] StageRotation()
    {
        var map = AutomatchConstants.MapPool[Random.Shared.Next(AutomatchConstants.MapPool.Length)];

        return [[FreeBattleRule, map, 0]];
    }

    private readonly ServerOptions options = options.Value;
    private readonly int port = options.Value.GameplayServerPort;
    private int matchIdentifier;

    /// <summary>Identifier of the match this host published, or zero until it exists.</summary>
    public int MatchIdentifier => matchIdentifier;

    // The heartbeat of the match row does not back off: the row leaves the window
    // it is listed within on the same interval this worker beats at, so any wait
    // beyond the interval would take the match off the room list before the next
    // beat could put it back.

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
                    options.GameplayLobbyName,
                    LobbyLookupRetryDelay);

                await Task.Delay(LobbyLookupRetryDelay, cancellationToken);
            }
        }

        await gameService.HeartbeatAsync(matchIdentifier, cancellationToken);
    }

    /// <summary>
    /// Whether this host's character names it a dedicated host.
    /// </summary>
    private bool IsDedicatedCharacterName =>
        options.GameplayServerCharacterName.StartsWith(
            DedicatedCharacterNamePrefix,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the lobby this host publishes in and creates the match when it is
    /// missing, so restarting the container reuses the match it left behind.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Identifier of the match, or zero while the lobby is missing.</returns>
    private async Task<int> EnsureMatchAsync(CancellationToken cancellationToken)
    {
        var lobby = (await lobbyService.FindActiveGameLobbiesAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.Name == options.GameplayLobbyName);

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

        // Drawn before the row is built rather than inside it, so the map this
        // host settles on can be logged below: the row is heartbeated for the
        // life of the container and is never rebuilt, so the log line at
        // creation is the only place the choice is ever visible.
        var rotation = StageRotation();

        var game = await gameService.CreateAsync(room =>
        {
            room.HostIdentifier = hostIdentifier;
            room.LobbyIdentifier = lobby.Identifier;
            room.Name = name;
            room.Password = string.Empty;
            room.Comment = "Gameplay server";
            room.MaximumPlayers = 8;
            room.Games = JsonSerializer.Serialize(rotation);

            // A gameplay server hosts rather than plays, so its room is a
            // dedicated host, and the character name it publishes under is what
            // says so. The flag travels into the room settings the other readers
            // look at; nothing is stored against the host to carry it, because
            // the name already does.
            room.Common = EventHostRoomSettingsUtils.Compose(IsDedicatedCharacterName, null);
        }, cancellationToken);

        await gameService.AddPlayerAsync(game.Identifier, hostIdentifier, cancellationToken);
        logger.LogInformation(
            "Created match {GameIdentifier} ({Name}) in lobby {LobbyIdentifier} on map {Map}",
            game.Identifier,
            name,
            lobby.Identifier,
            rotation[0][1]);

        return game.Identifier;
    }
}
