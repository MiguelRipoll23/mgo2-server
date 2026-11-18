using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The answer half of the round opener: the records a peer's entry echo draws,
/// held from the pass that heard it until the run is written.
/// </summary>
/// <remarks>
/// Split from <see cref="MatchStartRunService"/> because the run's own writer and
/// this together outgrow the size a source file is allowed. The measurement that
/// decides which records these are is on <see cref="WithEntryAnswers"/>.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// The record-level answers each peer's entry echo draws, which are the
    /// records the briefing's run opens on for that peer.
    /// </summary>
    /// <remarks>
    /// The echo is answered by the room's state and not beside it, so its
    /// answers are held from the pass that heard it until the run is written
    /// (<see cref="NoteEntryAnswers"/>).
    /// </remarks>
    private readonly Dictionary<PeerSession, List<UdpMessage>> entryAnswers = [];

    /// <summary>
    /// Holds the answers a peer's entry echo draws, so its briefing's run opens
    /// on them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The run's two opening records are these answers, and the captures
    /// write them as the echo's own records answered.</b> A reference briefing's
    /// run opens on two empty <c>0x5001</c> records whose fourth bytes are the
    /// ones the client's echo carried: <c>started-briefing-then-solo</c> echoes
    /// <c>82 7e</c>/<c>84 7e</c> at classes <c>0x0c</c>/<c>0x0d</c> at t+55.312
    /// and its run opens <c>0x5001</c> classes <c>0x0c</c>/<c>0x0d</c> at
    /// t+57.626. So the pair is not a recorded head but the answer to that
    /// peer's entry, matched by identifier and fourth byte
    /// (<see cref="PeerAcknowledgementUtils"/>).
    /// </para>
    /// <para>
    /// <b>And it is why the pass that hears the echo writes nothing.</b>
    /// Answering it beside the echo stops the client where it stands
    /// (<c>myserver20.pcapng</c>), and answering it nowhere leaves a record whose
    /// window never drains: <c>myserver22.pcapng</c>'s client re-sent the pair
    /// three seconds after the first copy and tore its session down 32 s after
    /// its report, raising its modal ten seconds later. Written as the run's
    /// opening pair, the answer arrives where every capture puts it — 2.3 s
    /// behind the entry — and names the records it answers.
    /// </para>
    /// </remarks>
    /// <param name="session">Session the echo arrived on.</param>
    /// <param name="answers">The records its answer draws, in echo order.</param>
    public void NoteEntryAnswers(PeerSession session, IReadOnlyList<UdpMessage> answers)
    {
        lock (gate)
        {
            entryAnswers[session] = [.. answers];
        }
    }

    /// <summary>
    /// The answers a peer's entry echo draws, in echo order; empty when the peer
    /// has not entered.
    /// </summary>
    /// <param name="session">Session the peer is on.</param>
    /// <returns>The records the run opens on for that peer.</returns>
    public IReadOnlyList<UdpMessage> EntryAnswersOf(PeerSession session)
    {
        lock (gate)
        {
            return entryAnswers.TryGetValue(session, out var answers) ? answers : [];
        }
    }

    /// <summary>
    /// A run with the answers the peer's entry echo drew placed in front of it,
    /// which is where every capture puts them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The empty <c>0x5001</c> pair a run opens on in the captures is these
    /// answers, not a position of the run's own.</b> Every empty <c>0x5001</c> the
    /// reference hosts write answers one of the peer's <c>0x1001</c> records and
    /// carries that record's fourth byte: `briefing-then-solo-game` writes the pair
    /// at t+9.780 behind the peer's <c>0x1001</c> ordinals <c>03</c>/<c>04</c>, at
    /// t+147.650 behind <c>22</c>/<c>23</c>, and one 0.9 s after each of the peer's
    /// polls at <c>05</c>…<c>18</c>; `fix-spawn-point-midtown` and
    /// `rounded-ended-by-reaching-tickets-limit` the same behind <c>08</c>/<c>09</c>
    /// and <c>35</c>/<c>36</c>. So the pair spends nothing on the room's exchange
    /// counter and exists only when there is something to answer.
    /// </para>
    /// <para>
    /// <b>An opening of invented heads leaves the echo's own records unanswered.</b>
    /// The pass that hears the echo withholds its answers so the run can carry
    /// them (<see cref="NoteEntryAnswers"/>); a run that opens on heads of its own
    /// instead never writes them, the client re-sends the pair on its own timer
    /// and its window never drains, and it tears the session down once its report
    /// is thirty seconds old. That is the shape `myserver22.pcapng` records, and
    /// the live run of 2026-11-09 reproduced it: `0x9001 ordinal 9` and
    /// `0x1001 ordinal 10` re-sent six times from the report on, the client's own
    /// ticks ending twenty seconds into the round, and `2001:FFFFFF80` behind it.
    /// </para>
    /// <para>
    /// <b>With nothing heard the run opens on nothing.</b> The pair is written
    /// only where the peer sent the records it answers; a run with no held answer
    /// is the run as built, starting on the state block or on the round's marker.
    /// A pinned <c>0x5001</c> there would be a numbered record carrying a byte the
    /// exchange had already spent, which the peer refuses without advancing — the
    /// two records this host used to write at <c>0x00</c> on every run.
    /// </para>
    /// </remarks>
    /// <param name="session">Session the run is written to.</param>
    /// <param name="run">The run as built.</param>
    /// <returns>The run, opening on the held answers when the pair was heard.</returns>
    public List<MatchRecordFramesUtils.RecordMessage> WithEntryAnswers(
        PeerSession session,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> run)
    {
        // The echo is a pair: `82 NN` on the team array and `84 NN` on the
        // occupancy one, each its own record. One heard on its own is a client
        // part-way through the exchange, and its answer waits for the other.
        const int echoRecords = 2;

        List<UdpMessage>? held;
        lock (gate)
        {
            entryAnswers.Remove(session, out held);
        }

        if (held is not { Count: echoRecords })
        {
            return [.. run];
        }

        return
        [
            .. held.Select(message =>
                new MatchRecordFramesUtils.RecordMessage(message.Type, message.Body, message.Flags)),
            .. run,
        ];
    }
}
