using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Holds the readiness reports peers have sent: the identifier set their round
/// plays under and the byte its run echoes back.
/// </summary>
/// <remarks>
/// <para>
/// A client says it has loaded and is ready. The report has to survive the gap
/// between the dispatch loop that receives it and the per-peer writer that is
/// still replaying the sparse phase — game1's report lands 1.66 s before the
/// round is otherwise due — so it is latched here rather than consumed by the
/// dispatcher. The round itself opens on the room's own clock and not on this:
/// the report names the round's set and the byte its run echoes, and a peer that
/// never sends one is still opened a round.
/// </para>
/// <para>
/// A client re-reports until it sees its round: the live run reported
/// twenty-three times over two minutes. One peer is therefore kept here, so a
/// repeat costs nothing beyond overwriting the two facts it carries: the byte it
/// reported, because that is what the host echoes back, and the identifier it
/// arrived on, because that names the generation the round plays under. The last
/// report wins, as it did when the room consumed the report this used to latch.
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
    private readonly HashSet<PeerSession> reportedPeers = [];
    private readonly Dictionary<PeerSession, byte> reportedBytes = [];
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
            reportedBytes[session] = reported;
            reportIdentifiers[session] = identifier;
            return reportedPeers.Add(session);
        }
    }

    /// <summary>
    /// The byte a peer's round echoes back, or <paramref name="fallback"/> when it
    /// has not reported at all. The fallback is the recorded host's own byte —
    /// game1's first round answers <c>0xe0 0x14</c> — so a round opened on the
    /// room's clock without a report still carries a measured body rather than a
    /// zero the capture never shows.
    /// </summary>
    /// <param name="session">Peer whose round is being written.</param>
    /// <param name="fallback">Byte to echo for a peer that has not reported.</param>
    /// <returns>The byte the round's payload record carries.</returns>
    public byte EchoedByte(PeerSession session, byte fallback)
    {
        lock (gate)
        {
            return reportedBytes.GetValueOrDefault(session, fallback);
        }
    }

    /// <summary>
    /// The identifier of the last record a peer reported itself ready under,
    /// which is the generation the room's first round plays under.
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
    /// Drops a peer's report, for a peer whose records have stopped. Without it a
    /// session reaped with a report unspent would keep the peer here after the
    /// server has let the session go.
    /// </summary>
    /// <param name="session">Peer to forget.</param>
    public void Forget(PeerSession session)
    {
        lock (gate)
        {
            reportedPeers.Remove(session);
            reportedBytes.Remove(session);
            reportIdentifiers.Remove(session);
        }
    }
}
