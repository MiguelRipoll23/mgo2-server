using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The run a briefing writes in answer to a peer's entry: the watcher that waits
/// for the briefing's set, the peer's entry and the entry's own dwell, and then
/// writes the briefing's run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <c>MatchStartRunService.Entries.cs</c> so neither file passes the
/// 400-line limit.</b> The entry note is there; the run it draws is here.
/// </para>
/// <para>
/// <b>The dwell is what the pair waits on as well.</b> The room's first
/// <c>85</c>/<c>86</c> copy is written inside this run in both reference briefings,
/// so the publication reads the same due instant
/// (<see cref="MatchStartRunService.IsEntryAnswerDue"/>,
/// <c>docs/analysis/join-stall-after-mode2.md</c> §55).
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// How long a briefing's run waits for its peer's lobby-slot echo before it is
    /// written anyway.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The echo is the client's own request to enter, and it is the instant
    /// every reference host answers — but it is not a record this host can
    /// require.</b> A run gated on it alone is a run that never goes out for a
    /// client whose echo this host does not read as one, and such a client is left
    /// on the black screen it showed while loading the map: no room state, no
    /// briefing fade, nothing until the briefing's deadline opens a round under it
    /// (`docs/analysis/join-stall-after-mode2.md` §18, and the live session of
    /// 2026-10-29 whose round map loaded with the client still on that screen).
    /// </para>
    /// <para>
    /// <b>It is a ceiling and not a new schedule.</b> Both reference clients echo
    /// 0.39 s and 0.52 s after their report and the run follows the echo, so this
    /// grace is never reached on the capture's own shape; it only bounds how long
    /// a peer can be left without the room's state. Three seconds is the dwell
    /// (<see cref="EntryRunDwellMilliseconds"/>) plus a second of slack, and the
    /// run it releases opens on the recorded empty heads because no entry answer
    /// was held.
    /// </para>
    /// </remarks>
    public const int EntryEchoFallbackMilliseconds = 3000;

    /// <summary>
    /// Peers already told that no entry echo was heard, so the warning is written
    /// once per briefing rather than on every poll.
    /// </summary>
    private readonly HashSet<PeerSession> entrylessWarned = [];

    /// <summary>
    /// Writes the briefing's own run to a peer once the briefing has heard a
    /// report and the peer has entered the room, and returns when the round it
    /// precedes opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The run is the answer to the peer's entry, and the report only names
    /// the set.</b> A client reports while it is still loading its map and echoes
    /// the lobby-slot pair once it has been placed in the room; every capture
    /// writes the run behind the echo and no capture writes one behind the
    /// report (<see cref="MatchStartRunService.OnRoomEntry"/>,
    /// <c>docs/analysis/join-stall-after-mode2.md</c> §18).
    /// </para>
    /// <para>
    /// <b>The briefing's run is not its round's.</b> A client still being briefed
    /// is served the run the first report named, under that report's own set, and
    /// the round it is waiting for opens later and under the other set
    /// (<see cref="MatchStartRunService.OnJoinReport"/>, <c>docs/protocol/BRIEFING_FLOW.md</c>
    /// §1–§2). The run's clock stays whole: nothing of the round has been played
    /// while the client is still reading the briefing, and the clock the countdown
    /// shows it is the briefing's own seven-byte record rather than this one.
    /// </para>
    /// <para>
    /// <b>And it waits out the entry's dwell before it goes.</b> Both reference
    /// briefings answer the echo two seconds later and nothing else in between,
    /// and the clients use that gap to start their own operations stream
    /// (<see cref="EntryRunDwellMilliseconds"/>). A run written in the entry's own
    /// dwell overtakes that transition, which is <c>myserver23.pcapng</c>'s join.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the run is written through.</param>
    private async Task WriteBriefingRunAsync(PeerContext context)
    {
        var session = context.Session;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            // No run is written before a briefing has heard a report: the run
            // echoes that report's byte, so a briefing that has heard from nobody
            // has nothing to write one from.
            if (BriefingGeneration is not { } generation)
            {
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            // The run is the answer to the client's entry and not to its report,
            // and the two are not the same instant: the report arrives while the
            // client is still loading its map, and every capture writes the run
            // behind the entry it answered (OnRoomEntry). A run written on the
            // report is written for a client that is not in the room yet, and the
            // client that is handed one does not report again, does not ready and
            // never reaches its briefing screen (myserver19.pcapng).
            //
            // The wait is bounded: a peer whose echo this host never hears is a
            // peer left without the room's state for as long as the briefing runs,
            // which is the black screen a client shows while the map it loaded has
            // no briefing over it. The grace releases the run so the state the
            // briefing is made of reaches it anyway (EntryEchoFallbackMilliseconds).
            if (!HasEntered(session))
            {
                if (EntryEchoGraceRemaining() is { } grace)
                {
                    await Task.Delay(grace);
                    if (roundState.IsOpen)
                    {
                        // The briefing ended inside the grace, so the round's own
                        // run is this peer's next record and this briefing's is
                        // stale.
                        return;
                    }
                }

                if (!HasEntered(session))
                {
                    NoteEntryMissing(context);
                }
            }

            if (ReadBriefingRound() is not { } round)
            {
                // The room is not published yet, so there is no round for the
                // briefing to precede and no time to hold its clock at.
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                continue;
            }

            if (EntryRunDwellRemaining(session) is { } owed)
            {
                await Task.Delay(owed);
                if (roundState.IsOpen)
                {
                    // The briefing ended inside the dwell, so the round's own run
                    // is this peer's next record and this briefing's is stale.
                    return;
                }
            }

            await WriteRunCoreAsync(context, generation, round, round.TimeLimitUnits, briefing: true);
            return;
        }
    }

    /// <summary>
    /// Notes, once, that a peer's briefing is being written without the entry
    /// echo that would have named it.
    /// </summary>
    /// <param name="context">Context the run is being written through.</param>
    private void NoteEntryMissing(PeerContext context)
    {
        lock (gate)
        {
            if (!entrylessWarned.Add(context.Session))
            {
                return;
            }
        }

        logger.LogWarning(
            "UDP {LocalPort}: no lobby-slot echo was heard from {RemoteAddress} within {Grace} ms of its "
            + "report; the briefing's run is written anyway, opening on the recorded empty heads, so the "
            + "peer is not left without the room's state",
            context.LocalPort,
            context.Remote,
            EntryEchoFallbackMilliseconds);
    }

    /// <summary>
    /// How much of the grace a briefing gives its peer to echo the lobby-slot pair
    /// is left, or <c>null</c> once it is spent.
    /// </summary>
    /// <remarks>
    /// It is counted from the briefing's report and not from the peer's session:
    /// the report is what names the set the run is written under, and a briefing
    /// that has not heard one has no run to release
    /// (<see cref="EntryEchoFallbackMilliseconds"/>).
    /// </remarks>
    /// <returns>The wait still owed, or <c>null</c> when it is already spent.</returns>
    private TimeSpan? EntryEchoGraceRemaining()
    {
        DateTimeOffset reportedAt;
        lock (gate)
        {
            if (briefingReportInstant is not { } instant)
            {
                return null;
            }

            reportedAt = instant;
        }

        var left = reportedAt.AddMilliseconds(EntryEchoFallbackMilliseconds) - DateTimeOffset.UtcNow;
        return left > TimeSpan.Zero ? left : null;
    }
}
