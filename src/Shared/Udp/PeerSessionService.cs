using System.Net;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// Holds the negotiated peer-to-peer sessions, keyed by remote endpoint. The
/// sessions are reaped once they have gone quiet, which releases the peers that
/// never completed a handshake.
/// <para>
/// A reaped session is logged when a logger is supplied, because the reaper is
/// the only place a peer is let go: there is no close to watch, so a session
/// that times out is otherwise a peer that leaves the log without a line.
/// </para>
/// </summary>
/// <param name="idleTimeout">How long a session may stay quiet before it is dropped.</param>
/// <param name="logger">Logger the reaper writes to, or null to stay silent.</param>
/// <param name="logPrefix">Prefix of the server, so a line names its port.</param>
public sealed class PeerSessionService(
    TimeSpan idleTimeout,
    ILogger? logger = null,
    string logPrefix = "udp")
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PeerSession> sessions = [];
    private Timer? reaper;

    /// <summary>Starts the reaper that drops idle sessions.</summary>
    public void Start() =>
        reaper ??= new Timer(_ => Reap(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

    /// <summary>Stops the reaper.</summary>
    public void Stop()
    {
        reaper?.Dispose();
        reaper = null;
    }

    /// <summary>Number of sessions currently held, reaped ones included until the reaper runs.</summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return sessions.Count;
            }
        }
    }

    /// <summary>Returns the session of a remote endpoint, when it has one.</summary>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    public PeerSession? Get(string remoteAddress)
    {
        lock (gate)
        {
            return sessions.GetValueOrDefault(remoteAddress);
        }
    }

    /// <summary>
    /// Returns the session that dials back to the given endpoint, when one does.
    /// </summary>
    /// <remarks>
    /// A session is filed under the endpoint its handshake arrived from, but the
    /// handshake handler then replaces that with the pair the peer advertised,
    /// because that is the only endpoint the peer's socket is reading. The two
    /// are the same for a peer out on the internet, where both name the peer's
    /// NATed address, and different wherever something rewrites the source in
    /// between: a load balancer, or a router that hairpins a reply addressed to
    /// its own public address back into the LAN it shares with this host. A
    /// datagram from such a peer arrives from the advertised endpoint and would
    /// otherwise miss the session entirely.
    /// </remarks>
    /// <param name="endpoint">Endpoint a datagram arrived from.</param>
    public PeerSession? FindByDialBack(IPEndPoint endpoint)
    {
        lock (gate)
        {
            return sessions.Values.FirstOrDefault(session => session.DialBack.Equals(endpoint));
        }
    }

    /// <summary>Stores a session, replacing any earlier one for the endpoint.</summary>
    /// <param name="session">Session to store.</param>
    public void Put(PeerSession session)
    {
        lock (gate)
        {
            sessions[session.RemoteAddress] = session;
        }
    }

    /// <summary>Drops the session of a remote endpoint.</summary>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    public void Remove(string remoteAddress)
    {
        lock (gate)
        {
            sessions.Remove(remoteAddress);
        }
    }

    /// <summary>Returns a snapshot of every live session.</summary>
    public List<PeerSession> Snapshot()
    {
        lock (gate)
        {
            return [.. sessions.Values];
        }
    }

    private void Reap()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        List<string> reaped = [];
        lock (gate)
        {
            foreach (var (remoteAddress, session) in sessions.ToList())
            {
                if (now - session.LastSeenAt > idleTimeout.TotalMilliseconds)
                {
                    sessions.Remove(remoteAddress);
                    reaped.Add($"{remoteAddress} (peer {session.PeerIdentifier})");
                }
            }
        }

        foreach (var remoteAddress in reaped)
        {
            logger?.LogInformation(
                "[{LogPrefix}] Peer disconnected: {RemoteAddress} went quiet for more than {IdleSeconds} seconds",
                logPrefix,
                remoteAddress,
                idleTimeout.TotalSeconds);
        }
    }
}
