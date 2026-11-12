using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.MatchRecords;
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
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class MatchService(
    GameService gameService,
    LobbyService lobbyService,
    HostIdentityService hostIdentity,
    MatchRoundStateService roundState,
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
    /// Player cap of the room this instance publishes, as its own row states it,
    /// or zero until one has been published this run.
    /// </summary>
    /// <remarks>
    /// <b>It is the row's number and not the channel's.</b> The roster can hold
    /// twenty-three and the room is advertised with this one, so a population
    /// sized from the channel fills seats the client was never told it had. It is
    /// this instance's configured size (<see cref="options"/>) and not a number of
    /// its own: the host's settings row already states
    /// <c>GameplayServerMaxPlayers</c> (<c>MatchSettingsUtils</c>), and a room that
    /// advertised eight while that row said seventeen told a browsing client two
    /// room sizes.
    /// <para>
    /// <b>A room too small for its own players refuses the player it is hosting.</b>
    /// An eight-seat room filled by bots writes eight players into the join's count
    /// record, and the client will not place itself in a room that names every seat
    /// it holds — the capture of 2026-11-10 20:38 does exactly that. The reference
    /// room the captures are measured against holds more than nine, which is why its
    /// join answer names a full roster and is still entered.
    /// </para>
    /// </remarks>
    public int RoomCapacity { get; private set; }

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
    private int rotationRoundCount;
    private int publishedCurrentGame = -1;

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
        await PublishCurrentGameAsync(cancellationToken);
    }

    /// <summary>
    /// Keeps the row's staged rotation entry on the round this host is about to
    /// open, advancing it as rounds do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the entry a joining client stages, so it must be the one the host is
    /// playing next.</b> A room screen draws that entry's rule and map, the join
    /// result hands the index to the client, and the client loads the map it names
    /// while the room's own records are written on the map of the round the host
    /// will open (<see cref="MatchSettingsService"/>). Left at zero it named the
    /// rotation's first entry for the life of the container, so from the second round
    /// on the details screen and the map the joiner loaded were different rounds —
    /// the room said one map and the client stood on another.
    /// </para>
    /// <para>
    /// The index is the count of rounds opened, taken modulo the rotation: round one
    /// is entry zero and a rotation that runs out starts again, which is the walk
    /// <see cref="MatchSettings.Round"/> plays. It is written only when it changes,
    /// because the beat runs every few seconds and the column is not what keeps the
    /// row alive.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task PublishCurrentGameAsync(CancellationToken cancellationToken)
    {
        if (rotationRoundCount == 0)
        {
            return;
        }

        var index = roundState.RoundNumber % rotationRoundCount;
        if (index == publishedCurrentGame)
        {
            return;
        }

        await gameService.UpdateAsync(
            matchIdentifier,
            room => room.CurrentGame = index,
            cancellationToken);
        publishedCurrentGame = index;
        logger.LogDebug(
            "Room {GameIdentifier} stages rotation entry {RoundIndex} for round {RoundNumber}",
            matchIdentifier,
            index,
            roundState.RoundNumber + 1);
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

        // The size is the role's size, decided once: every start states it, so the
        // row below and a fresh one cannot disagree. A Free Battle host plays at the
        // size this instance is configured for, which is the same figure its own
        // settings row states.
        var maximumPlayers = EventHostEligibilityUtils.IsHostRoleMode(lobby.SubtypeIdentifier)
            ? EventHostEligibilityUtils.MatchPlayerCapacity
            : Math.Max(1, options.GameplayServerMaxPlayers);
        // The row's number is the players' capacity, and the host's seat sits
        // outside it. `.pcapngs/actions/actions.pcapng` states sixteen over TCP and
        // then holds exactly sixteen players — `BOT-01` to `BOT-15` in slots 1 to
        // 15 and the human in 16 — with the host's own entry at slot 0 above them,
        // drawn by nothing. The fill is therefore the row's number less the humans,
        // and the host's seat is not subtracted anywhere: it is not in the table the
        // client counts. Subtracting it twice is what left the room one bot short.
        RoomCapacity = maximumPlayers;

        // A name is this instance's own, so a second room under it is a row left by
        // an earlier process. Two of them are two rooms a client can tell apart only
        // by identifier: it reads one room's details and binds to the other, which
        // is a map the room it looked at never named. The newest row is kept and the
        // rest are deleted before anything is served from them.
        var matches = (await gameService.FindByLobbyAsync(lobby.Identifier, cancellationToken))
            .Where(game => game.HostIdentifier == hostIdentifier && game.Name == name)
            .OrderBy(game => game.Identifier)
            .ToList();
        foreach (var stale in matches.Take(Math.Max(0, matches.Count - 1)))
        {
            await gameService.DeleteAsync(stale.Identifier, cancellationToken);
            logger.LogInformation(
                "Deleted room {GameIdentifier} ({GameName}): a second room under this host's name, left "
                + "by an earlier process",
                stale.Identifier,
                stale.Name);
        }

        var existing = matches.Count == 0 ? null : matches[^1];

        // The room plays the rotation this instance states, round for round, and
        // the row stores that same list in the form the lobby's `0x4313` reply
        // hands a client. It is resolved once so the same value is written to
        // either branch below, and `MatchSettingsService` answers a round out of
        // the same setting, so what a client is handed and what this host plays
        // are one rotation.
        var rounds = MatchSettingsUtils.Rotation(options);
        var rotation = MatchSettingsUtils.StoredRotationJson(rounds);
        var first = rounds[0];
        rotationRoundCount = rounds.Count;

        if (existing is not null)
        {
            // The row is restated whole, the way the create branch below writes a
            // fresh one, and not just its rotation: the row is what the lobby's
            // `0x4313` reply hands a browser and what a joining client reads its map
            // off, so a comment, a size, a mode or a staged round kept from an
            // earlier build is a room this instance does not run. It is a property
            // of the start rather than of a comparison, so a row rewritten by
            // anything else in between is corrected here instead of being kept.
            await gameService.UpdateAsync(
                existing.Identifier,
                room =>
                {
                    room.HostIdentifier = hostIdentifier;
                    room.LobbyIdentifier = lobby.Identifier;
                    room.Name = name;
                    room.LobbySubtype = lobby.SubtypeIdentifier;
                    room.Password = string.Empty;
                    room.Comment = "Gameplay server";
                    room.MaximumPlayers = maximumPlayers;
                    room.Games = rotation;
                    room.Ping = ReportedPingMilliseconds;
                    room.Stance = 0;
                    room.Status = 0;

                    // The row's current round is the round this instance is on, and
                    // the room's current rule and map are the triple it names: the
                    // lobby reads the stored rotation at this index to answer a
                    // browse and to fill a join, so a row left at zero told every
                    // client the rotation's first round while this host played
                    // another.
                    room.CurrentGame = roundState.RoundNumber % rounds.Count;
                },
                cancellationToken);

            // The row's own staged round is written from the round this process is
            // on rather than left as it was, so a restart does not keep pointing a
            // joiner at a round that is over.
            publishedCurrentGame = -1;
            logger.LogInformation(
                "Room {GameIdentifier} ({GameName}) restated from this instance's settings: {RoundCount} "
                + "round(s), starting with rule {Rule} on map {Map}, {MaximumPlayers} player(s)",
                existing.Identifier,
                name,
                rounds.Count,
                first.Rule,
                first.Map,
                maximumPlayers);

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
            room.MaximumPlayers = maximumPlayers;
            room.Games = rotation;

            // A gameplay server hosts rather than plays, so its room is a
            // dedicated host: the flag that says so is written against the host
            // character at startup, and the room's mode is the mode of the lobby
            // it is published in. Neither the name nor the lobby decides the
            // role on its own.
            room.Ping = ReportedPingMilliseconds;

            // The same staged round the restated branch writes, so a room created
            // now and a room restated after a restart state one current round.
            room.CurrentGame = roundState.RoundNumber % rounds.Count;
        }, cancellationToken);

        await gameService.AddPlayerAsync(game.Identifier, hostIdentifier, cancellationToken);
        logger.LogInformation(
            "Created match {GameIdentifier} ({Name}) in lobby {LobbyIdentifier} with {RoundCount} "
            + "round(s), starting with rule {Rule} on map {Map}, reporting {Ping} ms",
            game.Identifier,
            name,
            lobby.Identifier,
            rounds.Count,
            first.Rule,
            first.Map,
            ReportedPingMilliseconds);

        return game.Identifier;
    }
}
