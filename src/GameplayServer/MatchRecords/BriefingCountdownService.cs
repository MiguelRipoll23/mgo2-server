using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Tells a peer how much of its briefing is left, on the operations clock,
/// while the room is still briefing.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this closes.</b> The room's briefing time was the deadline its round
/// opens on and the duration it waits for a report that never comes, and it was
/// sent to no client: <c>docs/GAMEPLAY_SERVER.md</c> §5 item 3 listed the
/// countdown as the one thing on the wire that carries it and marked it unread.
/// It is read now — the operations clock, opened on <b>128 units</b> and counting
/// down to zero across the briefing, measured at 58.5 s in a room whose briefing
/// is a minute (<see cref="BriefingCountdownBodiesUtils"/>). A client in a
/// briefing waits either for every player to be ready or for its own timer, so
/// the countdown is how it learns which of the two it is about to be, and a host
/// that never sends it leaves every client timing its own silence.
/// </para>
/// <para>
/// <b>It stops when the round opens, and that is the whole of its lifetime.</b>
/// The capture places the briefing clock only before a round's opening run, and
/// the round's own clock — the same record opened on the round's length —
/// replaces it. A peer that arrives mid-round is served the round, not a
/// briefing, and is sent no countdown at all.
/// </para>
/// <para>
/// The clock is the room's own briefing time, read per record rather than
/// captured once, so a briefing that opens late or runs to its deadline counts
/// down against the time it actually has.
/// </para>
/// </remarks>
/// <param name="roundState">The room's round, whose opening is the countdown's end.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingCountdownService(
    MatchRoundStateService roundState,
    ILogger<BriefingCountdownService> logger)
{
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
            length = TimeSpan.FromMinutes(minutes);
        }

        logger.LogDebug(
            "A briefing runs for {BriefingTime} minute(s) from now; its peers are sent the countdown",
            minutes);
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
            if (startedAt is not { } began || length <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                return false;
            }

            remaining = (began + length) - now;
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
        var ordinal = (byte)0;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            if (!Remaining(DateTimeOffset.UtcNow, out var remaining))
            {
                return;
            }

            var units = BriefingCountdownBodiesUtils.CountdownUnits(length - remaining, length);
            try
            {
                await context.Send(
                    CountdownIdentifier,
                    BriefingCountdownBodiesUtils.CountdownBody(units),
                    ordinal++);
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

            await Task.Delay(PublishPeriodMilliseconds);
        }

        logger.LogDebug(
            "UDP {LocalPort}: the briefing countdown to {RemoteAddress} ended",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>How long the current briefing runs for, or zero when none is armed.</summary>
    public TimeSpan Length
    {
        get
        {
            lock (gate)
            {
                return length;
            }
        }
    }

    /// <summary>The stream this service writes under, named for the log and the docs.</summary>
    public static ushort Identifier => CountdownIdentifier;
}
