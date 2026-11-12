using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Shared.Options;

/// <summary>
/// Options that describe how this server runs. Every one of them is read from
/// a single upper-case environment variable.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// The recording's own rotation, round for round: nine rounds over five rules
    /// and seven maps, as <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §1 reads the
    /// <c>0x4313</c> reply's sixteen triples.
    /// </summary>
    private const string DefaultRotation =
        "BASE@Midtown, BOMB@Gronznyj Grad, CAP@Coppertown, RACE@Ambush Alley, "
        + "CAP@Blood Bath, TDM@Urban Ultimatum, RES@Virtuous Vista, TDM@Forest Firefight, "
        + "BASE@Hazard House";

    /// <summary>
    /// The recording's own seventeen per-rule words: five minutes and two rounds for
    /// every rule the reply states a pair for, a fifty-ticket TDM and a thirty-ticket
    /// DM.
    /// </summary>
    private const string DefaultRuleTimers = "5,2,5,2,5,2,5,2,50,5,30,5,2,5,2,5,2";

    /// <summary>Backing field for <see cref="GameplayServerCharacterName"/>.</summary>
    private string? gameplayServerCharacterName;

    /// <summary>
    /// Private address of this machine on the network the game clients share.
    /// Leaving it unset answers every local domain with the wildcard address,
    /// which only reaches a client running on the same machine.
    /// </summary>
    public string? AdvertisedAddress { get; set; }

    /// <summary>
    /// Hostname of the site a host signs in at to make their game joinable from
    /// outside the network the players share, with no trailing slash. It has no
    /// default: a deployment without such a site is never told about one it does
    /// not have, and the line is simply not sent.
    /// </summary>
    public string? ExternalWebHostname { get; set; }

    /// <summary>
    /// Interval in minutes between two heartbeats of a lobby row, and how long a
    /// lobby list that was read is served before it is read again. Every other
    /// timing of the lobby lifecycle is derived from it. It is the slowest beat
    /// this server runs, because a lobby container is a process that answers for
    /// itself; a room is kept alive by its host's ping report and a player by a
    /// much shorter one, so neither borrows this interval.
    /// </summary>
    public int LobbiesRefreshIntervalMinutes { get; set; } = 60;

    /// <summary>Port of the dedicated UDP gameplay host, when this instance runs one.</summary>
    public int GameplayServerPort { get; set; } = 5731;

    /// <summary>
    /// Address the dedicated host announces to peers. Defaults to the advertised
    /// address of this instance, and to loopback when no advertised address is
    /// configured.
    /// </summary>
    public string? PublicHostAddress { get; set; }

    /// <summary>
    /// Address the gameplay host puts in its own handshake and publishes as its
    /// peer-to-peer endpoint. It is derived from <see cref="PublicHostAddress"/>
    /// so the address a peer is told in a handshake and the one a joining client
    /// is handed in the join result cannot drift apart.
    /// <para>
    /// Neither may be the address a datagram arrived from. Behind a load
    /// balancer that is the balancer's own address, which a peer cannot dial
    /// back; the wildcard falls back to loopback because it is no more dialable.
    /// </para>
    /// </summary>
    public string GameplayServerAdvertisedAddress =>
        PublicHostAddress ?? (AnnouncedIpAddress is "0.0.0.0" ? "127.0.0.1" : AnnouncedIpAddress);

    /// <summary>
    /// Private (LAN) address the dedicated host writes as the second pair of
    /// its own handshake and roster records. Left unset, the machine's private
    /// address is detected and <c>192.168.1.15</c> answers when none can be
    /// read, so a recorded host's public-first, private-second pair is
    /// reproduced without a setting.
    /// </summary>
    public string? GameplayServerPrivateAddress { get; set; }

    /// <summary>Name of the lobby the gameplay server publishes its match in.</summary>
    public string GameplayLobbyName { get; set; } = "Free Battle";

    /// <summary>
    /// Name of the room the gameplay server publishes. It is the name a client
    /// sees in its room list and the one the host's own settings row states.
    /// <para>
    /// <b>Unset is <c>server-&lt;port&gt;</c>, not a recording's name.</b> The port is
    /// unique and stable for a deployment, so a host that is told nothing still
    /// publishes a room no other host can collide with, and the name says which
    /// room it is — the same shape the character name takes
    /// (<see cref="GameplayServerCharacterName"/>, against
    /// <see cref="GameplayServerRoomName"/>). A blank name reads as none for the
    /// same reason: a room is never published under a name no client can show.
    /// </para>
    /// </summary>
    public string? GameplayServerGameName { get; set; }

    /// <summary>
    /// The rounds the gameplay server's room plays, in order, written as the
    /// rotation the client's own room screen writes: one <c>RULE@Map</c> pair per
    /// round, separated by commas.
    /// <para>
    /// The rule is named the way the replay console and
    /// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> name rules (<c>TDM</c>,
    /// <c>TSNE</c>, <c>CAP</c>, <c>SCAP</c>, <c>BASE</c>, <c>BOMB</c>, <c>RACE</c>…) and the map
    /// the way a player knows it (<c>Blood Bath</c>) or the stage the disc ships it
    /// as (<c>n007a</c>). Both tables are ported in <see cref="MatchRuleUtils"/> and
    /// <see cref="MatchMapUtils"/>.
    /// </para>
    /// <para>
    /// <b>It is a rotation and not one rule on one map.</b> The room plays one
    /// entry per round, entry zero first, and starts again at the top once it runs
    /// out (<c>MatchSettings.Round</c>), which is what the recorded rooms do: the
    /// briefing this host follows files nine rounds across five rules and seven
    /// maps. The row the lobby hands a browsing client is this same list, so the
    /// rotation a player sees and the rounds this host opens are one setting.
    /// A name that is no rule or no map stops the process at startup, because the
    /// round it would open is one the client is not in.
    /// </para>
    /// </summary>
    public string GameplayServerRotation { get; set; } = DefaultRotation;

    /// <summary>
    /// The room's seventeen per-rule words — times, round counts and tickets — in
    /// the order the lobby's game-details reply states them: SNE time and rounds,
    /// CAP, RES, TDM with its ticket count, DM with its ticket count, BASE, BOMB
    /// and TSNE (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2), comma separated.
    /// <para>
    /// <b>It is where a round's length and its kill limit come from.</b> A round's
    /// rule names its own word in this bank, so a rotation that mixes rules plays
    /// each one for its own time instead of for one host-wide number — which is
    /// what the recorded rooms do, and what the client reads the round's length out
    /// of. The same bank is written to the host's settings row, so a client
    /// browsing the room and the host playing it are told one round length.
    /// </para>
    /// </summary>
    public string GameplayServerRuleTimers { get; set; } = DefaultRuleTimers;

    /// <summary>
    /// Slots the gameplay server's room admits, the host's own seat among them. It is
    /// the figure the room row and the host's settings row both state, and the one a
    /// browsing client draws as the room's capacity.
    /// </summary>
    /// <remarks>
    /// <b>The host holds a seat the row does not count.</b> This figure is what the
    /// room list states and what a browsing client draws, and it is the players' —
    /// the host's seat is added on the gameplay channel's side, as one more than the
    /// row states (`GameplayServer.Match.MatchService.RoomCapacity`). A room of
    /// sixteen therefore seats fifteen players and the host, which is the working
    /// room's shape: `.pcapngs/actions/actions.pcapng`'s roster lists a nameless host
    /// entry and `BOT-01` to `BOT-15`, and its kill feeds name the human in the
    /// sixteenth player slot — fifteen bots, one player, one host. A room advertised
    /// at sixteen that sized the fill from the row alone seated fourteen
    /// (2026-11-11 13:52), the one bot short.
    /// </remarks>
    public int GameplayServerMaxPlayers { get; set; } = 16;

    /// <summary>
    /// Level-limit base the host's settings row states: the level the room's limit
    /// is centred on, which the recorded room stated as 22.
    /// </summary>
    public int GameplayServerLevelLimitBase { get; set; } = 22;

    /// <summary>
    /// Briefing time of the gameplay server's room, in minutes: how long the room
    /// waits between two rounds.
    /// <para>
    /// The client pushes its own when a player hosts a room
    /// (<c>characters_host_settings.briefing_time</c>, the lobby's
    /// <c>0x4312</c> push), and a dedicated host has no such push: what its room
    /// briefs for is this setting. Its form on the gameplay channel is not
    /// settled, so no record carries it yet; it is resolved and reported, not
    /// written.
    /// </para>
    /// </summary>
    public int GameplayServerBriefingMinutes { get; set; } = 1;

    /// <summary>Display name of the account the gameplay server logs in with.</summary>
    public string GameplayServerAccountName { get; set; } = "server";

    /// <summary>
    /// Password of the gameplay server account. When unset, a random one is
    /// generated per process, so the account cannot be logged into by anyone
    /// who knows the default.
    /// </summary>
    public string? GameplayServerAccountPassword { get; set; }

    /// <summary>
    /// Name of the character the gameplay server plays as. It is
    /// <c>server-&lt;port&gt;</c> when it is not configured, which keeps every
    /// dedicated host on a character of its own without an index being handed
    /// out: the port is already unique and stable, and the registered
    /// peer-to-peer endpoint is filed per character. It is the same shape and the
    /// same value as <see cref="GameplayServerRoomName"/>'s fallback, so a host
    /// told nothing plays as the character its room is named after.
    /// </summary>
    public string GameplayServerCharacterName
    {
        get => gameplayServerCharacterName
            ?? DedicatedHostNameUtils.HostCharacterName(GameplayServerPort);
        set => gameplayServerCharacterName = value;
    }

    /// <summary>
    /// Clan name the gameplay server announces in its peer-to-peer
    /// player-profile record. A recorded record carries an empty clan name when
    /// the player has no clan, which is the case for a dedicated host.
    /// </summary>
    public string? GameplayServerClanName { get; set; }

    /// <summary>
    /// Address published to clients in place of each lobby's stored address.
    /// Only set when the advertised address is a routable address rather than a
    /// wildcard or loopback address.
    /// </summary>
    public string? OverrideIpAddress =>
        string.IsNullOrWhiteSpace(AdvertisedAddress) ||
        AdvertisedAddress is "0.0.0.0" or "127.0.0.1" or "localhost"
            ? null
            : AdvertisedAddress;

    /// <summary>
    /// Address this instance announces to clients and writes to the lobby rows:
    /// the advertised address when one is configured, and the wildcard address
    /// otherwise, which only a client on the same machine can reach.
    /// </summary>
    public string AnnouncedIpAddress => OverrideIpAddress ?? "0.0.0.0";

    /// <summary>
    /// Name of the room the gameplay server publishes. It is the configured game
    /// name, and the host prefix and this instance's own port when none is
    /// configured: the port is unique and stable, so two dedicated hosts never
    /// publish a room under one name. A blank configured name reads as none, so a
    /// room is never published under a name no client can show.
    /// </summary>
    public string GameplayServerRoomName =>
        string.IsNullOrWhiteSpace(GameplayServerGameName)
            ? $"server-{GameplayServerPort}"
            : GameplayServerGameName;

    /// <summary>Interval in seconds between two heartbeats of an owned row.</summary>
    public int LobbyHeartbeatIntervalSeconds => LobbiesRefreshIntervalMinutes * 60;

    /// <summary>
    /// Threshold in seconds after which a gameplay lobby is considered stale:
    /// twice <see cref="LobbyHeartbeatIntervalSeconds"/>, which is two hours at the
    /// default beat. One window answers two questions, and they are the same
    /// question asked twice: a lobby whose <c>updated_at</c> is older than it is
    /// not listed — so a client never sees a lobby whose server stopped, however
    /// long ago that was — and it is what the daily cleanup deletes.
    /// </summary>
    public int LobbyStaleSeconds => LobbyHeartbeatIntervalSeconds * 2;

    /// <summary>
    /// Threshold in seconds after which a room is considered stale: a minute. A
    /// populated room is kept alive by its host's ping report, which the client
    /// sends every half minute and which stamps <c>games.updated_at</c> through
    /// <c>UpdatePingsAsync</c>; a room hosted by a dedicated server by its own
    /// <see cref="MatchHeartbeatIntervalSeconds"/> beat. A minute of silence is
    /// therefore a host that has missed both of its beats, whatever kind it was,
    /// and the room is dropped from every list the moment it is that old — a
    /// client is never shown a room whose host is gone.
    /// <para>
    /// It is deliberately two client beats and not more. The room list is what a
    /// client joins from, so a room that is still listed is a room that can be
    /// joined, and a window any wider than the beat that fills it lists hosts that
    /// are no longer answering. The deletes are a different question with a
    /// different answer: they are a daily batch, so a stale row lingers in the
    /// table until midnight and is only ever invisible, never joined.
    /// </para>
    /// </summary>
    public int GameStaleSeconds { get; set; } = 60;

    /// <summary>
    /// Interval in seconds between two beats of a dedicated host's own match row,
    /// which is half of <see cref="GameStaleSeconds"/> so the beat lands once
    /// inside the window the room is listed within. A player-hosted room needs no
    /// such beat: its host's ping report is the beat.
    /// </summary>
    public int MatchHeartbeatIntervalSeconds => GameStaleSeconds / 2;
}
