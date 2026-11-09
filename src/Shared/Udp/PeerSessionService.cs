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
/// <param name="onReaped">
/// Invoked once per session the reaper drops, after it has been logged and is
/// no longer held here. Anything keyed on a session - a room roster, a stream -
/// has no other way to hear that the peer is gone, because there is no close to
/// watch; without it that state keeps a peer the reaper has already let go.
/// </param>
public sealed class PeerSessionService(
    TimeSpan idleTimeout,
    ILogger? logger = null,
    string logPrefix = "udp",
    Action<PeerSession>? onReaped = null)
{
    /// <summary>
    /// How long a session may stay quiet before it is dropped, when the caller
    /// has no reason to choose another window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a named value rather than a literal at the construction site
    /// because a second rule has to agree with it: the recorded host's stream
    /// stops for a peer once that peer has been quiet for this long, and it must
    /// stop no later than the reaper lets the peer go, or it keeps writing to a
    /// session that no longer exists.
    /// </para>
    /// <para>
    /// <b>The window is measured, not chosen.</b> A client that is still there
    /// answers every five seconds by itself, and the longest silence in any of the
    /// reference captures is <b>22.44 s</b> — <c>mgo2-started-on-solo-game</c>, a
    /// client that answers its entry echo at t+60.41 and then reads its map until
    /// t+82.85, when it lands a backlog of 298 acknowledgements at once. Every
    /// other reference peer stays inside 8.05 s. Half a minute is therefore the
    /// first whole number above the slowest measured load with room to spare, and
    /// the window was sixty seconds only because nothing had measured it.
    /// </para>
    /// <para>
    /// A peer that is let go early is not lost: a handshake on the endpoint of a
    /// session that is still held — or a fresh one after the reaper took it —
    /// starts that peer over, and it joins the room again
    /// (<see cref="Mgo2Server.GameplayServer.PeerSessionResetService"/>).
    /// </para>
    /// </remarks>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How often the reaper looks for sessions past <see cref="DefaultIdleTimeout"/>.
    /// </summary>
    /// <remarks>
    /// The window a peer is actually given is the timeout rounded up to the next
    /// tick, so a tick nearly half the window is what made the sixty-second
    /// timeout a wait of up to seventy-five. Five seconds is inside the peer's
    /// own cadence: a live client is seen this often, and a peer that has left is
    /// let go within five seconds of the window rather than up to fifteen.
    /// </remarks>
    public static readonly TimeSpan ReapInterval = TimeSpan.FromSeconds(5);

    private readonly Lock gate = new();
    private readonly Dictionary<string, PeerSession> sessions = [];

    /// <summary>
    /// Sessions the reaper has let go, kept with the key each one had so a peer
    /// that comes back without a handshake is still recognised.
    /// </summary>
    /// <remarks>
    /// <b>A dropped client does not always handshake again.</b> It carries on
    /// writing the frames it was writing, keyed with the session it still believes
    /// it holds, and it may arrive from a different endpoint than the one it left
    /// on - a re-bound socket, or a router that has stopped hairpinning its own
    /// address back into the LAN. Its key is the only thing that names it then, and
    /// the key lived on the session the reaper removed. Measured on a local run:
    /// the peer is reaped at 13:45:39 and answers at 13:45:54 from a second
    /// endpoint, eight times over the next five minutes, each frame undecodable
    /// because nothing held the key.
    /// </remarks>
    private readonly List<PeerSession> retired = [];

    /// <summary>
    /// How many reaped sessions keep their key, newest last. The window is the
    /// count rather than a clock: the keys only have to outlive the handful of
    /// peers a room holds, and a session taken back up leaves the list.
    /// </summary>
    public const int RetiredLimit = 8;

    private Timer? reaper;

    /// <summary>Starts the reaper that drops idle sessions.</summary>
    public void Start() =>
        reaper ??= new Timer(_ => Reap(), null, ReapInterval, ReapInterval);

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

    /// <summary>
    /// Returns the sessions the reaper has let go and whose keys are still kept.
    /// </summary>
    /// <returns>A snapshot, newest last, safe to walk outside the lock.</returns>
    public List<PeerSession> SnapshotRetired()
    {
        lock (gate)
        {
            return [.. retired];
        }
    }

    /// <summary>
    /// Binds a session to the endpoint its peer now answers from, taking it back
    /// up when the reaper had let it go.
    /// </summary>
    /// <remarks>
    /// The session is filed under the endpoint it was opened on and found by the
    /// endpoint it now dials back to, which is how its frames are recognised
    /// (<see cref="FindByDialBack"/>). A live session that has moved is rebound the
    /// same way, because the peer keeps its key: what changed is only where it is
    /// reachable. What the room held for a reaped peer is not restored with it —
    /// the reaper's callback has already dropped that, and the join the peer is in
    /// the middle of re-states it.
    /// </remarks>
    /// <param name="session">Session to take back up.</param>
    /// <param name="endpoint">Endpoint its peer now answers from.</param>
    public void Revive(PeerSession session, IPEndPoint endpoint)
    {
        lock (gate)
        {
            retired.Remove(session);
            session.DialBack = endpoint;
            session.Retired = false;
            session.LastSeenAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            sessions[session.RemoteAddress] = session;
        }
    }

    private void Reap()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        List<PeerSession> reaped = [];
        lock (gate)
        {
            foreach (var (remoteAddress, session) in sessions.ToList())
            {
                if (now - session.LastSeenAt > idleTimeout.TotalMilliseconds)
                {
                    sessions.Remove(remoteAddress);
                    reaped.Add(session);

                    // The key outlives the session, so the peer that comes back
                    // without a handshake can still be read (RetiredLimit).
                    retired.Add(session);
                    if (retired.Count > RetiredLimit)
                    {
                        retired.RemoveAt(0);
                    }
                }
            }
        }

        foreach (var session in reaped)
        {
            logger?.LogInformation(
                "[{LogPrefix}] Peer disconnected: {RemoteAddress} (peer {PeerIdentifier}) went quiet for more than {IdleSeconds} seconds",
                logPrefix,
                session.RemoteAddress,
                session.PeerIdentifier,
                idleTimeout.TotalSeconds);

            // The callback runs outside the lock and after the line, so it sees
            // a session this service no longer holds and cannot deadlock it by
            // taking a lock of its own back.
            onReaped?.Invoke(session);
        }
    }
}
