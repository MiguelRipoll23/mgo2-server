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
/// reporting players as a bit set (<see cref="BriefingCountdownBodiesUtils"/>).
/// </para>
/// <para>
/// <b>It stops when the round opens, and that is the whole of its lifetime.</b>
/// The capture places the briefing countdown only before a round's opening run.
/// A peer that arrives mid-round is served the round, not a briefing, and is sent
/// no countdown at all.
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
/// The room's reporting players, whose bit set the countdown carries on every
/// tick (<see cref="Rooms.PlayerReadyService.PresenceMask"/>).
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingCountdownService(
    MatchRoundStateService roundState,
    MatchRecordSequenceService sequences,
    Rooms.RoomRosterService roster,
    Rooms.PlayerReadyService ready,
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
    /// Arms the countdown for a briefing that has just begun.
    /// </summary>
    /// <remarks>
    /// Called where every briefing begins, so the countdown is armed by the same
    /// event the deadline is. A briefing with no time recorded — the room is not
    /// published yet — arms nothing and publishes nothing, which is the same
    /// answer <see cref="MatchStartRunService"/> gives the round.
    /// </remarks>
    /// <param name="begun">When the briefing began.</param>
    /// <param name="minutes">How long the briefing runs for, in minutes.</param>
    public void Begin(DateTimeOffset begun, int minutes)
    {
        if (minutes <= 0)
        {
            return;
        }

        lock (gate)
        {
            startedAt = begun;
            countingFrom = null;
            length = TimeSpan.FromMinutes(minutes);
        }

        logger.LogDebug(
            "A briefing runs for {BriefingTime} minute(s) from now; its peers are sent the countdown, "
            + "held at the whole briefing time until a second player reports",
            minutes);
    }

    /// <summary>
    /// Starts the countdown moving, because the room now has a second player.
    /// </summary>
    /// <remarks>
    /// <b>A room of one has no briefing to count down.</b> The reference holds
    /// the count at the room's whole briefing time for as long as a single player
    /// is in the room — <c>mgo2-game-started-on-briefing</c> writes
    /// <c>0b 3c …</c> unchanged for tens of seconds — and moves it only once a
    /// second player has reported
    /// (<c>mgo2-game-joined-when-red-team-then-briefing</c> holds <c>0b 3c</c> at
    /// the briefing's start and then counts it down). The count is the briefing's
    /// own time and not the room's age, so it runs from the moment the second
    /// report arrives and not from the briefing's start
    /// (<see cref="BriefingCountdownBodiesUtils"/>).
    /// </remarks>
    /// <param name="now">The moment the second player reported.</param>
    public void StartCounting(DateTimeOffset now)
    {
        lock (gate)
        {
            if (startedAt is null || length <= TimeSpan.Zero || countingFrom is not null)
            {
                return;
            }

            countingFrom = now;
        }

        logger.LogDebug(
            "A second player has reported, so the room's briefing countdown starts moving from now");
    }

    /// <summary>
    /// Whether the countdown is moving at all, or is still held at the whole
    /// briefing time for a room of one.
    /// </summary>
    public bool IsCounting
    {
        get
        {
            lock (gate)
            {
                return countingFrom is not null;
            }
        }
    }

    /// <summary>
    /// How much of the current briefing is left, and whether a briefing is
    /// running at all.
    /// </summary>
    /// <param name="now">The moment to read the clock at.</param>
    /// <param name="remaining">Time left in the briefing, when one is running.</param>
    /// <returns><c>true</c> while a briefing is current and has time left.</returns>
    public bool Remaining(DateTimeOffset now, out TimeSpan remaining)
    {
        lock (gate)
        {
            if (startedAt is null || length <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                return false;
            }

            // A briefing that has not heard a second player has no end to count
            // to, so it is held at the room's whole briefing time: the count the
            // peer is sent does not move until the room has someone to count for
            // (StartCounting).
            if (countingFrom is not { } counting)
            {
                remaining = length;
                return true;
            }

            remaining = (counting + length) - now;
            return remaining > TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Sends the countdown to one peer for as long as its room's briefing runs,
    /// and returns when the round opens or the peer goes.
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
        var ticked = false;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            // The briefing's own records end where the peer's run goes out: the
            // beat takes the tick over from there (HasRunWritten), and the
            // reference writes nothing of the briefing's between its run at
            // t+57.626 and its round at t+174.371 (docs/analysis/join-stall-after-mode2.md
            // §10). The countdown stays armed and silent instead of ending,
            // because the zero it closes a briefing on is the room's round opening
            // and not the run — the reference writes that pair of seven-byte
            // records at t+167.992, six seconds before the round it belongs to.
            if (roundState.HasRunWritten(session))
            {
                await roundState.WaitBriefingTickAsync(session, PublishPeriodMilliseconds);
                continue;
            }

            if (!Remaining(DateTimeOffset.UtcNow, out var remaining))
            {
                return;
            }

            var seconds = BriefingCountdownBodiesUtils.CountdownSeconds(remaining);
            try
            {
                await context.Send(
                    CountdownIdentifier,
                    BriefingCountdownBodiesUtils.CountdownBody(seconds, ready.PresenceMask),
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
        // there with twenty seconds still on the count). The zero is therefore the
        // briefing being over, and it is written for a peer that the briefing ran
        // for; a peer that arrives into a round already playing is served the round
        // and no countdown at all, which is what `ticked` keeps true here.
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
                    ready.PresenceMask),
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
}
