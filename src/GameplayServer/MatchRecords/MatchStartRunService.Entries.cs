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
    /// <para>
    /// <b>It is the reference's own range and not the client's stream that
    /// settles the value.</b> The two briefings write their run 2.31 s and 1.90 s
    /// after the entry, and the client's five <c>1d</c> rows land 1.69 s, 1.71 s
    /// and 1.99 s after it, so the run must be clear of that window rather than
    /// level with it: the live join of 2026-10-30 wrote its run 1.99 s after the
    /// entry and the client never wrote the rows
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §67). Half a second of
    /// margin puts this host past the latest of them without leaving the
    /// reference's range.
    /// </para>
    /// </remarks>
    public const int EntryRunDwellMilliseconds = 2500;

    /// <summary>
    /// Notes that a peer has asked to be placed in the room, which is what a
    /// briefing's run is written in answer to.
    /// </summary>
    /// <param name="context">Context of the entry the peer asked for.</param>
    /// <returns>
    /// <c>true</c> when this is the peer's own entry and <c>false</c> when it has
    /// entered before, which is what tells a re-sent echo from the first one
    /// (<see cref="Commands.Peer.Room.PlayerProfileHandler"/>).
    /// </returns>
    public bool OnRoomEntry(PeerContext context)
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
            return false;
        }

        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} asked to be placed in the room; the run it is owed "
            + "follows its entry and not its report, and {Answers}",
            context.LocalPort,
            context.Remote,
            MatchRecordPeerUtils.AnswersSoFar(context.Session));

        return true;
    }

    /// <summary>
    /// Answers the peer's entry with the lobby-slot confirmation, which is what a
    /// briefing ends on, and returns the sequence the pair it is to echo next is
    /// published under.
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
    /// <para>
    /// <b>The sequence is the peer's current one, not the one its first entry
    /// named.</b> A client answers each pair the room publishes, so its sequence has
    /// moved through the briefing; confirming the sequence of the first entry at the
    /// next round's open is a confirmation the client has already passed, and it
    /// waits for the pair at the sequence it is actually on until its session goes
    /// quiet. <see cref="Rooms.RoomRosterService.PairSequenceOf"/> is the one the
    /// client's own echoes update (<see cref="Commands.Peer.Room.PlayerProfileHandler"/>).
    /// </para>
    /// </para>
    /// <para>
    /// <b>The pair the client answers next is the caller's, not this pass's.</b>
    /// A round's open publishes it behind the two state transfers that hold and
    /// release the client, which is where the reference puts it, so the sequence is
    /// returned rather than written here (<see cref="Rooms.TeamArrayService.ConfirmAsync"/>,
    /// <see cref="Rooms.TeamArrayService.PublishNextPairAsync"/>). What is noted here
    /// is the sequence itself, because the round's run names it.
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
    /// <returns>
    /// The sequence the pair the client answers next carries, or <c>null</c> when
    /// this peer has no entry to confirm.
    /// </returns>
    private async Task<byte?> ConfirmEntryAsync(PeerContext context)
    {
        lock (gate)
        {
            if (!entrySequences.ContainsKey(context.Session))
            {
                return null;
            }
        }

        // **The open writes the confirmation alone.** The reference's first record of
        // a boundary is `81 NN` with no pair in front of it (`81 00` at t+15.933 and
        // `81 01` at t+97.610 of `rounded-ended-by-reaching-tickets-limit`), and the
        // pair the client echoes is the one behind the hold and the release, at
        // `NN + 1`. Republishing the answered pair first hands the client a sequence
        // it has already echoed: it echoes that one again and never asks to be
        // placed, which is the round that starts with no body in the map.
        var sequence = roster.PairSequenceOf(context.Remote.ToString());
        var next = await teamArrays.ConfirmAsync(context, sequence, writeAnsweredPair: false);

        // The confirmation republishes the pair at the sequence after the one the
        // client answered, and the round's run names that same pair. Noting it here
        // is what keeps the run from falling back to the sequence the client has
        // just answered while its echo of the next one is still in flight.
        roster.NotePairSequence(context.Remote.ToString(), next);
        return next;
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
