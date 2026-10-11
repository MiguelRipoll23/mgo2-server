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
/// ready state looks like on the wire. One report is therefore kept per peer, so a
/// repeat costs nothing and cannot queue a round per datagram: the byte it carried,
/// because that is what the host echoes back, and the identifier it arrived on,
/// because that names the generation the round has to be opened under.
/// </para>
/// <para>
/// The byte the report carries is **[U]**. It is echoed verbatim in the host's
/// reply and nothing measured here varies with it, so it is carried rather than
/// interpreted; see <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2. The identifier
/// is the round's own payload record and it *does* vary: it is the generation the
/// client's counter is on, which the room's first round takes from it rather than
/// assuming one (see <see cref="MatchRecordGenerationUtils.OfReadinessReport"/>).
/// </para>
/// </remarks>
public sealed class PlayerReadyService
{
    private readonly Lock gate = new();
    private readonly Dictionary<PeerSession, byte> reports = [];
    private readonly Dictionary<PeerSession, byte> lastReported = [];
    private readonly Dictionary<PeerSession, ushort> reportIdentifiers = [];

    /// <summary>
    /// Notes that a peer has reported itself ready, and reports whether this is
    /// the first report it has sent.
    /// </summary>
    /// <param name="session">Peer that reported.</param>
    /// <param name="reported">Byte the report carried.</param>
    /// <param name="identifier">Identifier the report arrived on.</param>
    /// <returns><c>true</c> when the peer had not reported before.</returns>
    public bool Report(PeerSession session, byte reported, ushort identifier)
    {
        lock (gate)
        {
            // The last byte and identifier survive the round they arrived for,
            // because the run that answers the report echoes the byte and is
            // written after the room has taken the pending report.
            lastReported[session] = reported;
            reportIdentifiers[session] = identifier;
            return reports.TryAdd(session, reported);
        }
    }

    /// <summary>
    /// Whether a peer has a readiness report the room has not taken yet. It is a
    /// read and not a take: the room asks it of every peer before it opens a
    /// round, so asking must not spend the report of a peer that is still the
    /// only one ready.
    /// </summary>
    /// <param name="session">Peer to ask about.</param>
    /// <returns><c>true</c> when a report is waiting.</returns>
    public bool HasReport(PeerSession session)
    {
        lock (gate)
        {
            return reports.ContainsKey(session);
        }
    }

    /// <summary>
    /// The last byte a peer reported, which is what its round's payload record
    /// echoes back. It stays after the report is taken, because the run is written
    /// after the room has consumed it.
    /// </summary>
    /// <param name="session">Peer to ask about.</param>
    /// <returns>The byte, or zero for a peer that has not reported.</returns>
    public byte ReportedByte(PeerSession session)
    {
        lock (gate)
        {
            return lastReported.GetValueOrDefault(session);
        }
    }

    /// <summary>
    /// The identifier of the last record a peer reported itself ready under,
    /// which is the generation the round it is owed plays under. It stays after
    /// the report is taken, because the room opens the round after consuming it.
    /// </summary>
    /// <param name="session">Peer to ask about.</param>
    /// <returns>The identifier, or <c>null</c> for a peer that has not reported.</returns>
    public ushort? ReportedIdentifier(PeerSession session)
    {
        lock (gate)
        {
            return reportIdentifiers.TryGetValue(session, out var identifier) ? identifier : null;
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
            lastReported.Remove(session);
            reportIdentifiers.Remove(session);
        }
    }
}