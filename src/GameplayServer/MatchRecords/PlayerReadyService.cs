using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Holds the readiness reports peers have sent until the writer of their match
/// records is ready to open the round on them.
/// </summary>
/// <remarks>
/// <para>
/// A client says it has loaded and is ready, and the host opens the round. The
/// report has to survive the gap between the dispatch loop that receives it and
/// the per-peer writer that is still replaying the sparse phase. Either order is
/// ordinary — game1's report lands 1.66 s before the round is otherwise due — and
/// both are the same event seen from either end, so neither side waits on the
/// other.
/// </para>
/// <para>
/// A client re-reports until the round opens: the live run reported twenty-three
/// times over two minutes and was answered to none of them, which is what a stuck
/// ready state looks like on the wire. One report is therefore kept per peer — the
/// byte it carried, since that is what the host echoes back — so a repeat costs
/// nothing and cannot queue a round per datagram.
/// </para>
/// <para>
/// The byte the report carries is **[U]**. It is echoed verbatim in the host's
/// reply and nothing measured here varies with it, so it is carried rather than
/// interpreted; see <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2.
/// </para>
/// </remarks>
public sealed class PlayerReadyService
{
    private readonly Lock gate = new();
    private readonly Dictionary<PeerSession, byte> reports = [];

    /// <summary>
    /// Notes that a peer has reported itself ready, and reports whether this is
    /// the first report it has sent.
    /// </summary>
    /// <param name="session">Peer that reported.</param>
    /// <param name="reported">Byte the report carried.</param>
    /// <returns><c>true</c> when the peer had not reported before.</returns>
    public bool Report(PeerSession session, byte reported)
    {
        lock (gate)
        {
            return reports.TryAdd(session, reported);
        }
    }

    /// <summary>
    /// Takes a peer's readiness report, if it has sent one.
    /// </summary>
    /// <remarks>
    /// Taken rather than read, so a client that reports again while its round is
    /// going out queues exactly one more. The capture agrees: game1's client
    /// reported twice, 2.29 s apart, and the recorded host opened one round.
    /// </remarks>
    /// <param name="session">Peer whose report to take.</param>
    /// <param name="reported">The byte the report carried.</param>
    /// <returns><c>true</c> when a report was waiting.</returns>
    public bool TryTake(PeerSession session, out byte reported)
    {
        lock (gate)
        {
            return reports.Remove(session, out reported);
        }
    }

    /// <summary>
    /// Drops a peer's report, for a peer whose records have stopped. Without it a
    /// session reaped with a report unspent would keep the peer here after the
    /// server has let the session go.
    /// </summary>
    /// <param name="session">Peer to forget.</param>
    public void Forget(PeerSession session)
    {
        lock (gate)
        {
            reports.Remove(session);
        }
    }
}