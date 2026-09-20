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
    /// Interval in minutes between two heartbeats of a lobby row, and how long a
    /// lobby list that was read is served before it is read again. Every other
    /// timing of the lobby lifecycle is derived from it. It is the slowest beat
    /// this server runs, because a lobby container is a process that answers for
    /// itself; a room is kept alive by its host's ping report and a player by a
    /// much shorter one, so neither borrows this interval.
    /// </summary>
    public int LobbiesRefreshIntervalMinutes { get; set; } = 60;

    /// <summary>Port of the dedicated UDP gameplay host, when this instance runs one.</summary>
    public int GameplayServerPort { get; set; } = 5730;

    /// <summary>
    /// Address the dedicated host announces to peers. Defaults to the advertised
    /// address of this instance, and to loopback when no advertised address is
    /// configured.
    /// </summary>
    public string? PublicHostAddress { get; set; }

    /// <summary>Name of the lobby the gameplay server publishes its match in.</summary>
    public string GameplayLobbyName { get; set; } = "Free Battle";

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
    /// Threshold in seconds after which a room is considered stale: an hour. A
    /// populated room is kept alive by its host's ping report, which the client
    /// sends every half minute and which stamps <c>games.updated_at</c> through
    /// <c>UpdatePingsAsync</c>; a room hosted by a dedicated server by its own
    /// <see cref="MatchHeartbeatIntervalSeconds"/> beat. An hour of silence is
    /// therefore a host that is gone, whatever kind it was, and the room is both
    /// dropped from every list and deleted once a day.
    /// </summary>
    public int GameStaleSeconds { get; set; } = 3600;

    /// <summary>
    /// Interval in seconds between two beats of a dedicated host's own match row,
    /// which is half of <see cref="GameStaleSeconds"/> so the beat lands once
    /// inside the window the room is listed within. A player-hosted room needs no
    /// such beat: its host's ping report is the beat.
    /// </summary>
    public int MatchHeartbeatIntervalSeconds => GameStaleSeconds / 2;
}
