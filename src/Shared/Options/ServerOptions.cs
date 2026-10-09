namespace Mgo2Server.Shared.Options;

/// <summary>
/// Options that describe how this server runs. Every one of them is read from
/// a single upper-case environment variable.
/// </summary>
public sealed class ServerOptions
{
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
    /// Whether the dedicated host keeps writing to a peer after it has answered
    /// that peer's profile: the timed burst that follows the roster exchange and
    /// the recorded host stream under it. On by default, because the recorded
    /// host writes both.
    /// <para>
    /// Turning it off leaves the join untouched - the handshake, the roster run
    /// and its repeat still go out - and sends nothing after the closing roster
    /// record. That is what tells a join that stalls on the traffic after the
    /// roster apart from one that stalls on the roster itself, which no single
    /// log separates because both end with the same roster lines.
    /// </para>
    /// </summary>
    public bool GameplayServerPostJoinTraffic { get; set; } = true;

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

    /// <summary>Name of the lobby the gameplay server publishes its match in.</summary>
    public string GameplayLobbyName { get; set; } = "Free Battle";

    /// <summary>
    /// Optional name of the room the gameplay server publishes. When unset,
    /// the room name is derived from the gameplay server port.
    /// </summary>
    public string? GameplayServerGameName { get; set; }

    /// <summary>Display name of the account the gameplay server logs in with.</summary>
    public string GameplayServerAccountName { get; set; } = "server";

    /// <summary>
    /// Password of the gameplay server account. When unset, a random one is
    /// generated per process, so the account cannot be logged into by anyone
    /// who knows the default.
    /// </summary>
    public string? GameplayServerAccountPassword { get; set; }

    /// <summary>Name of the character the gameplay server plays as.</summary>
    public string GameplayServerCharacterName { get; set; } = "server";

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
