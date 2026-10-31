using Mgo2Server.GameplayServer.Identity;
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
/// <param name="hostIdentity">Identity the gameplay server presents.</param>
/// <param name="mapRotation">The map this start's room plays.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class MatchService(
    GameService gameService,
    LobbyService lobbyService,
    HostIdentityService hostIdentity,
    MatchMapRotationService mapRotation,
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
    /// Player cap a Free Battle host's room advertises. It is unrelated to the
    /// event capacity: this host plays with whoever joins it, so it keeps a
    /// normal room's size.
    /// </summary>
    private const int FreeBattleRoomCapacity = 8;

    /// <summary>
    /// The latency this host's room reports, in milliseconds.
    /// <para>
    /// The unit is milliseconds and the field is the host's own reported
    /// round-trip time: the client's room picker buckets it against 20 and 80
    /// (<c>docs/proto/outbound/mgo2_cmd_4302_s2c.ksy</c>, <c>ping</c>), and the
    /// capture's rooms report 10, 50 and 100. A gameplay server hosts from
    /// wherever it is deployed and has no client to measure a ping from, so it
    /// reports the value the captured dedicated host reported — 100 — rather
    /// than the zero an unreported field would carry. Zero is not "a fast
    /// host": it is the absence of a measurement, and it is what the room list
    /// draws as the worst of the three readings.
    /// </para>
    /// </summary>
    private const int ReportedPingMilliseconds = 100;

    private readonly ServerOptions options = options.Value;
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
        while (matchIdentifier == 0)
        {
            logger.LogDebug(
                "Looking for gameplay lobby '{LobbyName}' and its dedicated host match",
                options.GameplayLobbyName);
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
            logger.LogDebug(
                "Gameplay lobby '{LobbyName}' is not currently available",
                options.GameplayLobbyName);
            return 0;
        }
        logger.LogDebug(
            "Found gameplay lobby {LobbyIdentifier} ({LobbyName}, subtype {SubtypeIdentifier})",
            lobby.Identifier,
            lobby.Name,
            lobby.SubtypeIdentifier);

        var name = options.GameplayServerRoomName;
        var hostIdentifier = (int)hostIdentity.PeerIdentifier;
        var existing = (await gameService.FindByLobbyAsync(lobby.Identifier, cancellationToken))
            .FirstOrDefault(game => game.HostIdentifier == hostIdentifier && game.Name == name);

        // The map advances at every start, and the row found above is what says
        // where the last one left it: its stored rotation names the map that run
        // played, so this run plays the next. A room with no row behind it — the
        // first start on a fresh database — plays the configured map instead, and
        // `MatchMapRotationService` is where the answer is kept, because
        // `MatchSettingsService` builds every gameplay record from it and the two
        // must be the same map.
        var map = MatchSettingsUtils.StoredMap(existing?.Games) is { } played
            ? MatchMapUtils.Next(played)
            : MatchMapUtils.Parse(options.GameplayServerMap);
        mapRotation.Select(map);

        // The rotation is this start's map beside this instance's rule, in the form
        // the room row stores and the lobby's `0x4313` reply hands a client, and it
        // is resolved once so the same value is written to either branch below. It
        // is the one `MatchSettingsService` answers a round with, so what a client
        // is handed and what this host plays are one rotation.
        var round = MatchSettingsUtils.Round(options, map);
        var rotation = MatchSettingsUtils.StoredRotationJson([round]);

        if (existing is not null)
        {
            // The row states this start's rotation, always and before anything is
            // served: it is the column the lobby's `0x4313` reply reads, so a row
            // left on a previous start's map is a client told one map while this
            // host plays another. The advance above already reads that column to
            // decide which map this start plays, and this write is what makes the
            // column follow it — a property of the start rather than of a
            // comparison, so a row rewritten by anything else in between is
            // corrected here instead of being kept.
            var previousMap = MatchSettingsUtils.StoredMap(existing.Games);
            await gameService.UpdateAsync(
                existing.Identifier,
                room => room.Games = rotation,
                cancellationToken);
            if (existing.Games != rotation)
            {
                logger.LogInformation(
                    "Room {GameIdentifier} ({GameName}) states this start's own rotation: rule {Rule} on "
                    + "map {Map}, from map {PreviousMap}",
                    existing.Identifier,
                    existing.Name,
                    round.Rule,
                    round.Map,
                    previousMap);
            }

            logger.LogDebug(
                "Reusing existing gameplay match {GameIdentifier} ({GameName}) in lobby {LobbyIdentifier}",
                existing.Identifier,
                existing.Name,
                lobby.Identifier);
            return existing.Identifier;
        }

        var game = await gameService.CreateAsync(room =>
        {
            room.HostIdentifier = hostIdentifier;
            room.LobbyIdentifier = lobby.Identifier;
            room.Name = name;
            // The room runs the mode of the lobby it is published in, which is
            // what makes it a host for that mode: the Survival host publishes in
            // the Survival hosts lobby, so its room is a Survival room without
            // the name having to say so.
            room.LobbySubtype = lobby.SubtypeIdentifier;
            room.Password = string.Empty;
            room.Comment = "Gameplay server";
            // The room's size is the role's size. A Free Battle host advertises
            // the small cap it plays at; a host that serves an event has to seat
            // the match it can be leased, which is the event's own player
            // capacity. Sizing a Survival host at the Free Battle cap made the
            // host ineligible for every match bigger than seven players, so a
            // team that found an opponent was never assigned one.
            room.MaximumPlayers = EventHostEligibilityUtils.IsHostRoleMode(lobby.SubtypeIdentifier)
                ? EventHostEligibilityUtils.MatchPlayerCapacity
                : FreeBattleRoomCapacity;
            room.Games = rotation;

            // A gameplay server hosts rather than plays, so its room is a
            // dedicated host: the flag that says so is written against the host
            // character at startup, and the room's mode is the mode of the lobby
            // it is published in. Neither the name nor the lobby decides the
            // role on its own.
            room.Ping = ReportedPingMilliseconds;
        }, cancellationToken);

        await gameService.AddPlayerAsync(game.Identifier, hostIdentifier, cancellationToken);
        logger.LogInformation(
            "Created match {GameIdentifier} ({Name}) in lobby {LobbyIdentifier} on rule {Rule}, map {Map}, "
            + "reporting {Ping} ms",
            game.Identifier,
            name,
            lobby.Identifier,
            round.Rule,
            round.Map,
            ReportedPingMilliseconds);

        return game.Identifier;
    }
}
