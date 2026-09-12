namespace Mgo2Server.Shared.Options;

/// <summary>
/// Options that describe how this server runs. Every one of them is read from
/// a single upper-case environment variable.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// Address the server binds to and that the name server resolves local
    /// domains to.
    /// </summary>
    public string ListeningIpAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Interval in minutes between two lobby heartbeats, and between two
    /// refreshes of the in-memory lobby cache. Every other timing of the lobby
    /// lifecycle is derived from it.
    /// </summary>
    public int LobbiesRefreshIntervalMinutes { get; set; } = 5;

    /// <summary>Port of the dedicated UDP gameplay host, when this instance runs one.</summary>
    public int GameplayServerPort { get; set; } = 5730;

    /// <summary>
    /// Address the dedicated host announces to peers. Defaults to the loopback
    /// address, which is what a peer that is not given a public host is told to
    /// connect to.
    /// </summary>
    public string? PublicHostAddress { get; set; }

    /// <summary>Name of the lobby the gameplay server publishes its match in.</summary>
    public string GameplayLobbyName { get; set; } = "Free Battle";

    /// <summary>
    /// Address published to clients in place of each lobby's stored address.
    /// Only set when the listening address is a routable address rather than a
    /// wildcard or loopback address.
    /// </summary>
    public string? OverrideIpAddress =>
        ListeningIpAddress is "0.0.0.0" or "127.0.0.1" or "localhost" ? null : ListeningIpAddress;

    /// <summary>Interval in seconds between two heartbeats of an owned row.</summary>
    public int LobbyHeartbeatIntervalSeconds => LobbiesRefreshIntervalMinutes * 60;

    /// <summary>
    /// Interval in seconds between two refreshes of the served lobby list: a
    /// tenth of the heartbeat interval, bounded so a deployment that shortened
    /// the heartbeat still refreshes promptly and never floods the database.
    /// Gameplay lobbies register themselves within seconds of a deployment
    /// starting, so a refresh as slow as the heartbeat would leave the gate
    /// publishing an incomplete list for minutes after every restart.
    /// </summary>
    public int LobbyCacheRefreshIntervalSeconds =>
        Math.Clamp(LobbyHeartbeatIntervalSeconds / 10, 15, 60);

    /// <summary>
    /// Threshold in seconds after which a row is considered stale: twice the
    /// heartbeat interval. A gameplay lobby or a dedicated-host match that was
    /// not written to within this window is no longer served, and is cleaned up.
    /// </summary>
    public int LobbyStaleSeconds => LobbyHeartbeatIntervalSeconds * 2;
}
