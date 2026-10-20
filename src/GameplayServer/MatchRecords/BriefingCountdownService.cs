using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Tells a peer how much of its briefing is left, on the operations stream,
/// while the room is still briefing.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this closes.</b> A client in a briefing waits either for every player
/// to be ready or for its own timer, so the countdown is how it learns which of the
/// two it is about to be, and a host that never sends it leaves every client timing
/// its own silence. What it is: the seven-byte <c>0x0b</c> operations record,
/// carrying the seconds of the room's briefing that are left and the room's
/// readied players as a bit set (<see cref="BriefingCountdownBodiesUtils"/>), which
/// is how a client learns which of the room's players are waiting to play.
/// </para>
/// <para>
/// <b>Its window is the briefing's own run, and not the join.</b> Both reference
/// hosts write their first countdown <em>after</em> the run the peer's first
/// report asks for and none before it: <c>mgo2-game-started-on-briefing</c> runs
/// at t+58.255 and counts from t+93.127 to its round, and
/// <c>mgo2-started-briefing-then-solo-game</c> runs at t+57.626 and writes its
/// countdown only at t+167.992, two seconds before its round is asked for. A peer
/// that has not reported has no run and is owed no countdown either, which is what
/// the wait for the run below is (<c>docs/analysis/join-stall-after-mode2.md</c>
/// §15).
/// </para>
/// <para>
/// <b>It ends on the round's open and on the briefing's own time running out.</b>
/// Either end is the same record: the count at zero carrying the room's whole
/// reporting set, written once. The captures place that zero 3 to 6 s before the
/// round it belongs to — <c>started-briefing-then-solo</c> writes
/// <c>0b 37 00 08 …</c> and <c>0b 00 00 08 …</c> at t+167.992 and again at
/// t+170.366, and its round follows at t+174.371 — because a briefing that runs
/// out is over whether or not the round that asks for it has opened yet. A peer
/// that arrives mid-round is served the round, not a briefing, and is sent no
/// countdown at all.
/// </para>
/// <para>
/// <b>And it is frozen while the room has one player.</b> A room of one has no
/// briefing to run out — the second player is what the wait is for — so the
/// count is held at the room's whole briefing time until a second player reports
/// and only then moves (<see cref="StartCounting"/>, <see cref="IsCounting"/>).
/// </para>
/// </remarks>
/// <param name="roundState">The room's round, whose opening is the countdown's end.</param>
/// <param name="sequences">
/// The stream counters the countdown's ordinal comes from: the briefing writes on
/// the operations stream, the same one the round's own clock uses.
/// </param>
/// <param name="roster">The room's roster, whose ordinals the room-state records take.</param>
/// <param name="ready">
/// The room's readied players, whose bit set the countdown carries on every tick
/// (<see cref="Rooms.BriefingStateService.ReadyMask"/>).
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed partial class BriefingCountdownService(
    MatchRoundStateService roundState,
    MatchRecordSequenceService sequences,
    Rooms.RoomRosterService roster,
    Rooms.BriefingStateService briefingStates,
    ILogger<BriefingCountdownService> logger)
{
    /// <summary>
    /// The room's own state, written beside the countdown on every tick of a room
    /// whose round has played: the stage records the reference host writes through
    /// a briefing that follows a round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and carried by this tick because this is what a briefing
    /// sends.</b> The reference hosts write the 22-byte room-state record and the
    /// two 54-byte entries through a briefing in groups 2.2 to 2.6 s apart —
    /// `mgo2-game1` t+466.0, 470.5, 472.6, `mgo2-game2` t+62.2, 64.4, 224.6 and
    /// `mgo2-started-briefing-then-solo-game` t+57.6, 60.0 — which is the cadence
    /// of this tick (<see cref="BriefingCountdownBodiesUtils.PublishPeriodMilliseconds"/>).
    /// The records themselves are the ones the run writes
    /// (<see cref="MatchRecordBodiesUtils.RoomStateRecord"/> and the two entries
    /// behind it), so a peer is told the room's stage both when a round opens and
    /// for as long as a briefing runs.
    /// </para>
    /// <para>
    /// <b>The group belongs to the room's state and not to the phase, so a room
    /// with no round behind it writes none of it.</b> Every briefing named above
    /// follows a round; a fresh room's first briefing is the case the captures
    /// separate. The reference <c>mgo2-game-started-on-briefing</c> joins an
    /// unplayed room and writes neither the 22-byte record nor the entries in its
    /// 48 s briefing, while this host wrote them from the join onward — telling
    /// the client a round exists while it is still being briefed
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §10). The condition is the
    /// room's own round count, which is zero only before the first round opens.
    /// </para>
    /// </remarks>
    private async Task PublishRoomStateAsync(PeerContext context)
    {
        if (roundState.RoundNumber == 0)
        {
            return;
        }

        var ordinals = roster.TakeRunOrdinals(3);
        var state = new List<Rooms.RosterRecord>
        {
            new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.RoomStateRecord(), ordinals[0]),
            new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.RoomStateEntryFirst(), ordinals[1]),
            new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.RoomStateEntrySecond(), ordinals[2]),
        };

        await Rooms.RosterRunSendUtils.SendAsync(context, state, compressed: true);
    }

    /// <summary>
    /// Spacing between countdown records, measured on the same cadence as the
    /// round's own operations insertion.
    /// </summary>
    public const int PublishPeriodMilliseconds = BriefingCountdownBodiesUtils.PublishPeriodMilliseconds;

    /// <summary>
    /// Operations identifier the countdown travels under before any round is
    /// open: the first generation's, which is the set a briefing is under before
    /// a report chooses one.
    /// </summary>
    private static readonly ushort CountdownIdentifier = MatchRecordGenerationUtils.First.Operations;

    private readonly Lock gate = new();
    private DateTimeOffset? startedAt;
    private DateTimeOffset? countingFrom;
    private TimeSpan length;

    /// <summary>
    /// Sends the countdown to one peer from that peer's own run for as long as its
    /// room's briefing runs, and returns when the briefing's time is out, the round
    /// opens, or the peer goes.
    /// </summary>
    /// <remarks>
    /// The record is written per peer rather than per room because the channel is
    /// per peer: each session has its own counter and its own ordinal, and a
    /// record written once for the room would be numbered as though one peer had
    /// received it on the others' streams too.
    /// <para>
    /// <b>It does not fault.</b> It runs beside the round's waits rather than
    /// inside them, so a wait that throws would leave this task unawaited and its
    /// exception unobserved — and a send that fails here says the peer is gone,
    /// which is what ending the countdown already means. A failed send therefore
    /// ends the countdown quietly.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var session = context.Session;

        // A briefing's clock is heard from the peer's own run, which is what its
        // first report asks for: the run is written two to three seconds after
        // that report and the briefing's records follow it, so a peer that has not
        // reported — and so has no run — is owed no countdown. The wait is bounded
        // by the peer (WaitForRunWrittenAsync) rather than by a timer, because a
        // report may arrive at any point in the room's briefing.
        await roundState.WaitForRunWrittenAsync(session);

        var ticked = false;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            if (!Remaining(DateTimeOffset.UtcNow, out var remaining))
            {
                // The briefing's own time is out, so the briefing is over whether
                // or not the round has opened: it closes on the zero below, which
                // is the record a client waiting in its briefing acts on.
                break;
            }

            var seconds = BriefingCountdownBodiesUtils.CountdownSeconds(remaining);
            try
            {
                await context.Send(
                    CountdownIdentifier,
                    BriefingCountdownBodiesUtils.CountdownBody(seconds, briefingStates.ReadyMask),
                    sequences.Take(CountdownIdentifier));

                // The room's own stage rides with the countdown, which is the one
                // thing this host writes through a briefing.
                await PublishRoomStateAsync(context);
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "UDP {LocalPort}: the briefing countdown to {RemoteAddress} stopped on a failed send",
                    context.LocalPort,
                    session.RemoteAddress);
                return;
            }

            ticked = true;

            // The tick ends the moment the round opens: this task is awaited
            // before the beat, and a period slept out after the run was written
            // would hold the beat's first frame behind it.
            await roundState.WaitBriefingTickAsync(session, PublishPeriodMilliseconds);
        }

        // A briefing's last word to a peer is a zero, and it carries the room's
        // whole ready set. The reference writes it at the instant its round opens
        // — `mgo2-game-joined-when-red-team-then-briefing` t+113.758,
        // `0b 00 00 ea 01 00 00` — beside the round's own run, and the ticking
        // record never reaches the zero on its own in a room that fills (it opened
        // there with twenty seconds still on the count). A briefing that runs its
        // own time out instead closes on the same zero: `started-briefing-then-solo`
        // writes the pair at t+167.992 and t+170.366 and is asked for its round at
        // t+173.438. Either end writes it for a peer the briefing ran for; a peer
        // that arrives into a round already playing is served the round and no
        // countdown at all, which is what `ticked` keeps true here.
        if (ticked && MatchRecordPeerUtils.IsAlive(session))
        {
            await SendBriefingEndAsync(context, session);
        }

        logger.LogDebug(
            "UDP {LocalPort}: the briefing countdown to {RemoteAddress} ended",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>
    /// Writes a peer the briefing's closing record: the count at zero, carrying
    /// the room's whole reporting set.
    /// </summary>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="session">Session the record is written to.</param>
    private async Task SendBriefingEndAsync(PeerContext context, PeerSession session)
    {
        try
        {
            await context.Send(
                CountdownIdentifier,
                BriefingCountdownBodiesUtils.CountdownBody(
                    seconds: 0,
                    readySet: briefingStates.ReadyMask),
                sequences.Take(CountdownIdentifier));
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the briefing countdown's closing zero to {RemoteAddress} was not sent",
                context.LocalPort,
                session.RemoteAddress);
        }
    }

    /// <summary>The stream this service writes under, named for the log and the docs.</summary>
    public static ushort Identifier => CountdownIdentifier;

    /// <summary>
    /// The countdown record a room's answer carries, or <c>null</c> when the room
    /// is not briefing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A briefing's answer to <c>83 NN</c> is the pair, this record and the
    /// player's settings header, and no entry</b> — `mgo2-game-started-on-briefing`
    /// t+93.127 and `mgo2-game2` t+78.987 both write
    /// <c>85 NN / 86 NN / 0b 3c 00 00 00 00 00 / 0b SS 02 …</c> under the request's
    /// own sequence, and the countdown there is the same body the tick writes, at
    /// the count the room holds at that instant. The record says which briefing the
    /// client is waiting in, which the pair alone does not.
    /// </para>
    /// <para>
    /// <b>It is the tick's own count and the tick's own stream.</b> The answer
    /// takes its fourth byte from the countdown stream, as the tick does, so the
    /// record the answer carries is one the client could have received a tick later
    /// — the same numbering, the same set — rather than a second series beside the
    /// countdown. A room whose briefing has not been armed (no join report yet) has
    /// no count to answer with and returns <c>null</c>: the reference writes none
    /// before its first run either.
    /// </para>
    /// </remarks>
    /// <returns>The record, or <c>null</c> when no briefing is current.</returns>
    public Rooms.RosterRecord? AnswerRecord()
    {
        if (!Remaining(DateTimeOffset.UtcNow, out var remaining))
        {
            return null;
        }

        return new Rooms.RosterRecord(
            CountdownIdentifier,
            BriefingCountdownBodiesUtils.CountdownBody(
                BriefingCountdownBodiesUtils.CountdownSeconds(remaining),
                briefingStates.ReadyMask),
            sequences.Take(CountdownIdentifier));
    }
}
