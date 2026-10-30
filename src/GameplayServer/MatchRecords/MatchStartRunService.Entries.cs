using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The entry half of the round opener: the note that a peer has asked to be
/// placed in the room, which is the event a briefing's run is written in answer
/// to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The room's state answers the client's entry and not its report.</b> A
/// joining client's map-loaded report arrives while it is still loading its map:
/// 0.4 s before its entry in <c>mgo2-started-briefing-then-solo-game</c> and
/// 0.5 s in <c>mgo2-game-started-on-briefing</c>, the two captures of a working
/// host that hold a single join. Every run those captures write follows an entry
/// — 0.3 s, 0.6 s, 0.7 s, 0.9 s, 1.2 s, 1.4 s, 1.9 s, 2.3 s and 4.7 s behind
/// one, and in <c>mgo2-game-joined-when-red-team-then-briefing</c> two of them
/// follow an entry 39 s and 75 s after that room's last report, which no report
/// can explain (<c>docs/analysis/join-stall-after-mode2.md</c> §18).
/// </para>
/// <para>
/// <b>What a run written for a report instead produces.</b> The room's state is
/// handed to a client that has not entered yet, so the client that reaches its
/// briefing screen never hears the briefing's state at all: it reports again, it
/// readies nothing and it never shows the screen — `myserver19`'s join, and the
/// same shape in the live sessions before it (`myserver16`, `myserver17`).
/// </para>
/// <para>
/// The note is taken in the pass that answers the echo
/// (<see cref="Commands.Peer.Room.PlayerProfileHandler"/>), and what that pass
/// writes is the briefing's run and nothing else: the lobby-slot handshake's own
/// confirmation is written when the room's round opens
/// (<see cref="ConfirmEntryAsync"/>), which is where the captures put it.
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Peers that have been placed in the room: each one echoed the lobby-slot
    /// pair (<c>82 NN</c>/<c>84 NN</c>), which is the client's own request to
    /// enter.
    /// </summary>
    private readonly HashSet<PeerSession> entered = [];

    /// <summary>
    /// The sequence each peer's entry named, which is the one its lobby-slot
    /// confirmation answers.
    /// </summary>
    /// <remarks>
    /// It is kept rather than answered in the pass that heard it: no capture
    /// confirms the entry's own echo (see <see cref="ConfirmEntryAsync"/>).
    /// </remarks>
    private readonly Dictionary<PeerSession, byte> entrySequences = [];

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
    /// When each peer's entry was heard, which is the instant the run that
    /// answers it is spaced from.
    /// </summary>
    /// <remarks>
    /// It is kept rather than answered in the pass that heard the echo, because
    /// the answer is not written in that pass either: the captures put it
    /// <see cref="EntryRunDwellMilliseconds"/> behind the entry.
    /// </remarks>
    private readonly Dictionary<PeerSession, DateTimeOffset> entryInstants = [];

    /// <summary>
    /// When the room's briefing first heard a report, which is the other instant
    /// a briefing's run is spaced from.
    /// </summary>
    /// <remarks>
    /// <b>A client enters and reports in either order.</b> Both references report
    /// first and enter half a second later, which is the order the dwell was
    /// measured in; <c>myserver24.pcapng</c>'s client entered at t+56.047 and
    /// reported at t+59.024, three seconds later. A dwell counted from the entry
    /// alone is spent before that report names the briefing's set, and the run
    /// overtakes the transition it was spaced from — 0.29 s behind the report,
    /// against 1.90 s and 2.31 s after the entry in the references.
    /// </remarks>
    private DateTimeOffset? briefingReportInstant;

    /// <summary>
    /// How long a briefing's run is spaced from the entry it answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The captures answer the echo behind a dwell rather than inside its own
    /// pass.</b> Both reference briefings receive the lobby-slot echo and write
    /// their run two seconds later: `mgo2-started-briefing-then-solo-game` takes
    /// the pair at t+55.312 and writes the run at t+57.626 (**2.31 s**), and
    /// `mgo2-game-started-on-briefing` takes it at t+56.360 and writes at t+58.255
    /// (**1.90 s**). Neither writes anything else between them.
    /// </para>
    /// <para>
    /// <b>And the client acts in that gap.</b> The reference client starts its own
    /// operations stream there — five seven-byte records and the twenty-two-byte
    /// one, at the stream's own first six ordinals
    /// (`docs/protocol/STATE_VECTOR.md` §1) — at 1.99 s after its entry in the solo
    /// capture, before the run arrives. A host that writes the run in the entry's
    /// own dwell therefore overtakes a client that is still entering its briefing,
    /// which is what `myserver23.pcapng` measures: the echo at t+56.789 was
    /// answered at t+57.007 (**0.22 s**), the client acknowledged the run and every
    /// record behind it for the next 3.5 minutes, and it never wrote its own
    /// operations at all.
    /// </para>
    /// </remarks>
    public const int EntryRunDwellMilliseconds = 2000;

    /// <summary>
    /// Notes that a peer has asked to be placed in the room, which is what a
    /// briefing's run is written in answer to.
    /// </summary>
    /// <param name="context">Context of the entry the peer asked for.</param>
    public void OnRoomEntry(PeerContext context)
    {
        bool first;
        lock (gate)
        {
            first = entered.Add(context.Session);
            if (first)
            {
                entrySequences[context.Session] =
                    Rooms.TeamArrayFramesUtils.SequenceOf(context.Message.Body);
                entryInstants[context.Session] = DateTimeOffset.UtcNow;
            }
        }

        if (!first)
        {
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} asked to be placed in the room; the run it is owed "
            + "follows its entry and not its report",
            context.LocalPort,
            context.Remote);
    }

    /// <summary>
    /// Answers the peer's entry with the lobby-slot confirmation and the pair
    /// under the next sequence, which is what a briefing ends on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not in the pass that heard the echo.</b> Every capture writes the
    /// confirmation far behind the entry that asked for it and never beside it:
    /// `mgo2-started-briefing-then-solo-game` echoes <c>82 7e</c> at t+55.312 and
    /// is confirmed <c>81 7e</c> at t+167.992, four seconds before its round;
    /// `mgo2-game-started-on-briefing` echoes at t+56.360 and is confirmed at
    /// t+193.621; `mgo2-game-joined-when-red-team-then-briefing` echoes at t+57.983
    /// and is confirmed at t+72.560. `mgo2-game1` and `mgo2-started-on-solo-game`
    /// are confirmed nowhere in their captures at all.
    /// </para>
    /// <para>
    /// <b>A confirmation in the entry pass stops the client where it stands.</b>
    /// `myserver20.pcapng` is that: the echo at t+61.265 was answered 37 ms later
    /// with the pair, <c>81 f3</c>, the group and the pair at <c>f4</c> (twice, once
    /// per echo record), the briefing's run followed 164 ms after it, and the
    /// client acknowledged every record and then sent nothing but its own heartbeat
    /// for two minutes — never the five <c>1d</c> rows that open every reference
    /// client's own operations stream, never a ready, and never an echo of the
    /// <c>f4</c> it had just been handed. **How far behind the entry those rows
    /// come is the client's own and not a stage host or client can time:**
    /// 1.69 s, 1.71 s, 1.99 s, 6.26 s, 37.5 s, 224.7 s and 424.5 s across the seven
    /// captures that write them, so a client missing them at t+2 s has not been
    /// shown to be stalled by that alone
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §32.1).
    /// </para>
    /// </remarks>
    /// <param name="context">Context of the peer whose entry is answered.</param>
    private async Task ConfirmEntryAsync(PeerContext context)
    {
        byte sequence;
        lock (gate)
        {
            if (!entrySequences.TryGetValue(context.Session, out sequence))
            {
                return;
            }
        }

        await teamArrays.ConfirmAsync(context, sequence);
    }

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
    /// its report, raising its modal ten seconds later. Written as the run's opening pair, the answer arrives where
    /// every capture puts it — 2.3 s behind the entry — and names the records it
    /// answers.
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
    /// Whether the run that answers a peer's entry is due, which is when the
    /// briefing's room publication writes its first membership pair.
    /// </summary>
    /// <remarks>
    /// <b>A pair written before this names a room the peer is not in yet.</b>
    /// Both reference briefings write their first <c>85</c>/<c>86</c> copy inside
    /// the run — 1.90 s after the echo in <c>started-on-briefing</c>, 2.31 s in
    /// <c>game2</c> — and neither writes one in the twenty seconds ahead of it, so
    /// the pair's first copy is owed exactly when the run is
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §54).
    /// </remarks>
    /// <param name="session">Session the peer is on.</param>
    /// <returns>
    /// <c>true</c> once the briefing has heard a report and the dwell behind the
    /// peer's entry — or behind the report, when no entry was heard — is spent.
    /// </returns>
    public bool IsEntryAnswerDue(PeerSession session)
    {
        return BriefingGeneration is not null
            && EntryRunDwellRemaining(session) is null;
    }

    /// <summary>
    /// How much of the entry's dwell is left, counted from the later of the peer's
    /// entry and the briefing's first report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The report is an anchor on its own, which is what a peer that has not
    /// entered yet waits on.</b> The dwell runs from the later of the peer's entry
    /// and the briefing's first report: a client that enters before it reports is
    /// still loading its map when it enters, so the report is the instant its
    /// transition ends. A peer that has not entered at all is timed by the report
    /// alone. Reading a missing entry as a spent dwell was the defect the live join
    /// of 2026-10-30 exposed: the run went out 0.06 s after the report and 0.81 s
    /// <em>before</em> the client's entry, and a run ahead of the entry is the black
    /// screen §18 records (<c>docs/analysis/join-stall-after-mode2.md</c> §67).
    /// </para>
    /// <para>
    /// <b>No report yet is not a dwell of zero either.</b> There is then no instant
    /// to count from, and the run waits for one: <see cref="IsEntryAnswerDue"/>
    /// already requires a briefing generation, and this is that condition one layer
    /// down.
    /// </para>
    /// </remarks>
    /// <param name="session">Session the peer is reached on.</param>
    /// <returns>The wait still owed, or <c>null</c> when it is already spent.</returns>
    private TimeSpan? EntryRunDwellRemaining(PeerSession session)
    {
        DateTimeOffset anchor;
        lock (gate)
        {
            if (briefingReportInstant is not { } reportedAt)
            {
                return null;
            }

            anchor = entryInstants.TryGetValue(session, out var heardAt) && heardAt > reportedAt
                ? heardAt
                : reportedAt;
        }

        var left = anchor.AddMilliseconds(EntryRunDwellMilliseconds) - DateTimeOffset.UtcNow;
        return left > TimeSpan.Zero ? left : null;
    }

    /// <summary>
    /// Forgets a peer's entry: a session that is gone has no sequence left to
    /// confirm, and the next peer to enter starts its own.
    /// </summary>
    /// <param name="session">Session that ended.</param>
    private void ForgetEntry(PeerSession session)
    {
        lock (gate)
        {
            entrySequences.Remove(session);
            entryAnswers.Remove(session);
            entryInstants.Remove(session);
        }
    }
}
