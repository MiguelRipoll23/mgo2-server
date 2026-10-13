using Mgo2Server.Shared.Domain.Games;

namespace Mgo2Server.Shared.Options;

/// <summary>
/// Options that describe how this server runs. Every one of them is read from
/// a single upper-case environment variable.
/// </summary>
public sealed class ServerOptions
{
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
    /// Optional name of the room the gameplay server publishes. When unset,
    /// the room name is derived from the gameplay server port.
    /// </summary>
    public string? GameplayServerGameName { get; set; }

    /// <summary>
    /// Rule the gameplay server's room runs, named the way the replay console and
    /// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> name rules: <c>TDM</c>,
    /// <c>TSNE</c>, <c>CAP</c>, <c>SCAP</c>. The name is what a person writes; the
    /// id a rotation carries is the mapping's own
    /// (<c>MatchRuleUtils.Parse</c>, from <c>rpdt.js</c>'s <c>MODE_NAMES</c>).
    /// <para>
    /// The dedicated host <i>is</i> its room's host, so its rule is its own
    /// setting and not a row's to be read back: nothing else can state one, and
    /// the room row is written from this. The name is matched without case, and a
    /// name that is no rule stops the process at startup — a host that ran a rule
    /// no client was told about would open a round the client is not in.
    /// </para>
    /// </summary>
    public string GameplayServerRule { get; set; } = "TDM";

    /// <summary>
    /// Map the gameplay server's room runs, by the name a player knows it by
    /// (<c>Blood Bath</c>) or by the stage the disc ships it as (<c>n007a</c>).
    /// Both are the replay console's table, ported in <c>MatchMapUtils</c>.
    /// <para>
    /// Like the rule, this is the room's own setting rather than a draw or a row:
    /// a host whose map was drawn at random would publish a room whose round no
    /// operator chose. The default is the map of the recorded briefing this host
    /// follows, and a name that is no map stops the process at startup.
    /// </para>
    /// </summary>
    public string GameplayServerMap { get; set; } = "Blood Bath";

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

    /// <summary>
    /// Minutes a round of the gameplay server's room lasts. The round's clock is
    /// this times 480, the relation measured over three captures
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2).
    /// <para>
    /// A player-hosted room's time per rule is in the bank the client pushes
    /// (<c>characters_host_settings.rule_timers</c>), and a dedicated host, again,
    /// has never pushed one: the round's length is this setting, for every rule.
    /// The default is seven minutes, which is the clock of the recorded room this
    /// host replays. Minutes of zero or less stop the process at startup, because
    /// zero is a round that ends the moment it opens.
    /// </para>
    /// </summary>
    public int GameplayServerRoundMinutes { get; set; } = 7;

    /// <summary>
    /// Kill limit the gameplay server's room's round ends on, or <c>null</c> for a
    /// round that ends on its clock alone.
    /// <para>
    /// The reply calls this word a rule's <c>tickets</c>
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2, §3) — the recorded rooms
    /// play 50- and 55-ticket team deathmatches — and nothing on the gameplay
    /// channel counts kills for a host, so a limit here is only ever reached by
    /// the score this host keeps itself. Its default is unset, which is what a
    /// room with no stored bank plays: a round that runs its full time. A limit of
    /// zero or less stops the process at startup, because such a round ends before
    /// it opens.
    /// </para>
    /// </summary>
    public int? GameplayServerKillLimit { get; set; }

    /// <summary>Display name of the account the gameplay server logs in with.</summary>
    public string GameplayServerAccountName { get; set; } = "server";

    /// <summary>
    /// Password of the gameplay server account. When unset, a random one is
    /// generated per process, so the account cannot be logged into by anyone
    /// who knows the default.
    /// </summary>
    public string? GameplayServerAccountPassword { get; set; }

    /// <summary>
    /// Name of the character the gameplay server plays as. It is the host prefix
    /// and this host's own port when it is not configured, which keeps every
    /// dedicated host on a character of its own without an index being handed
    /// out: the port is already unique and stable, and the registered
    /// peer-to-peer endpoint is filed per character.
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
