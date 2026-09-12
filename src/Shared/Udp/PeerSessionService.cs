using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// Holds the negotiated peer-to-peer sessions, keyed by remote endpoint. The
/// sessions are reaped once they have gone quiet, which releases the peers that
/// never completed a handshake.
/// </summary>
/// <param name="idleTimeout">How long a session may stay quiet before it is dropped.</param>
public sealed class PeerSessionService(TimeSpan idleTimeout)
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

    /// <summary>Returns the session of a remote endpoint, when it has one.</summary>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    public PeerSession? Get(string remoteAddress)
    {
        lock (gate)
        {
            return sessions.GetValueOrDefault(remoteAddress);
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
        lock (gate)
        {
            foreach (var (remoteAddress, session) in sessions.ToList())
            {
                if (now - session.LastSeenAt > idleTimeout.TotalMilliseconds)
                {
                    sessions.Remove(remoteAddress);
                }
            }
        }
    }
}
