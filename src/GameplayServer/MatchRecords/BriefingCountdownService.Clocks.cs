using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing's clock: when it began, how long it runs for, how much of it is
/// left, and whether its count is moving yet.
/// </summary>
/// <remarks>
/// The service itself — what the clock is sent as, and the tick that writes it — is
/// in <c>BriefingCountdownService.cs</c>; this file is the time those writes are
/// about, so a reader asking "how long is left" has one place to look. Two readings
/// live here on purpose: <see cref="Remaining"/> is the count a peer is sent, which
/// a room of one has held at the whole briefing time because that is what the
/// reference writes, and <see cref="TimeRemaining"/> is the briefing's own end,
/// which is what the round's clock hold reads.
/// </remarks>
public sealed partial class BriefingCountdownService
{
    /// <summary>
    /// Arms the countdown for a briefing that has just begun.
    /// </summary>
    /// <remarks>
    /// Called where a briefing's first report reaches its room, so the countdown is
    /// armed by the same event the deadline is (<see cref="MatchStartRunService"/>).
    /// A briefing with no time recorded — the room is not published yet — arms
    /// nothing and publishes nothing, which is the same answer
    /// <see cref="MatchStartRunService"/> gives the round.
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
    /// How much of the current briefing is left as a count, and whether a briefing
    /// is running at all.
    /// </summary>
    /// <remarks>
    /// <b>This is the count the peers are sent, and it is held for a room of one.</b>
    /// A room that has not heard a second player is held at the room's whole
    /// briefing time, because that is what the reference writes there
    /// (<see cref="IsCounting"/>). A caller asking how long the briefing itself has
    /// left wants <see cref="TimeRemaining"/> instead: a room of one is still
    /// briefed, and its briefing still ends.
    /// </remarks>
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
    /// How long the briefing itself has left, whether or not its count is moving,
    /// and whether a briefing is running at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the briefing's own end, and the count is not.</b> A room of one has
    /// its count held at the whole briefing time — the reference writes the count
    /// that way, and <see cref="Remaining"/> is that reading — but the briefing
    /// itself still ends: the deadline that opens the room's round is armed on this
    /// same time (<see cref="MatchStartRunService"/>).
    /// </para>
    /// <para>
    /// <b>What reads it is the round's clock hold.</b> A round opened while a
    /// briefing still has time to run holds its clock at the round's whole length
    /// for what is left of it, and a round opened once the briefing's time is out
    /// counts from its own open. The held count is the wrong reading for that: a
    /// solo room would hold a round's clock for a nominal minute it had already
    /// spent, which is the frozen seven minutes <c>myserver12.pcapng</c> shows a
    /// client from t+62.371 to t+123.973.
    /// </para>
    /// </remarks>
    /// <param name="now">The moment to read the clock at.</param>
    /// <param name="remaining">Time left in the briefing, when one is running.</param>
    /// <returns><c>true</c> while a briefing is current and has time left.</returns>
    public bool TimeRemaining(DateTimeOffset now, out TimeSpan remaining)
    {
        lock (gate)
        {
            if (startedAt is not { } started || length <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                return false;
            }

            remaining = (started + length) - now;
            return remaining > TimeSpan.Zero;
        }
    }
}
