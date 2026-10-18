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
/// reference writes, and <see cref="TimeRemaining"/> is the time the room is being
/// given before its round opens on its own, which is what the round's clock hold
/// reads and what a room of one does not get.
/// </remarks>
public sealed partial class BriefingCountdownService
{
    /// <summary>
    /// Arms the countdown for a briefing whose first join report has arrived.
    /// </summary>
    /// <remarks>
    /// <b>It is the count a client is shown and not the room's deadline.</b> The
    /// report that arms this is the client saying it is on the briefing screen
    /// (<see cref="MatchStartRunService.OnJoinReport"/>), and what it is owed is the
    /// count that screen carries, held at the room's whole briefing time until a
    /// second player joins (<see cref="StartCounting"/>). The deadline that ends the
    /// briefing belongs to the room and is armed where the room gets its second
    /// player, by the same call that starts this count moving. A briefing with no
    /// time recorded — the room is not published yet — arms nothing and publishes
    /// nothing, which is the same answer <see cref="MatchStartRunService"/> gives the
    /// round.
    /// </remarks>
    /// <param name="begun">When the briefing's count began.</param>
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
            "A briefing's {BriefingTime}-minute count is now armed; its peers are sent the countdown, "
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
    /// second player has reported, at which point the room's own deadline starts
    /// from the same moment (<see cref="MatchStartRunService"/>)
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
    /// How long the briefing the room is running has left, and whether the room is
    /// being timed at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It ends where the count does, and a frozen count never ends.</b> The
    /// briefing's time is the room's own — the timer its clients run on their
    /// briefing screens — so it starts where the count starts moving, at the report
    /// that gives the room a second player (<see cref="StartCounting"/>), and a room
    /// of one is not timed at all: its count is held at the whole briefing time and
    /// its briefing never runs out, which is why its round opens on its player's own
    /// ready and on nothing else (`docs/analysis/join-stall-after-mode2.md` §17).
    /// Answering with the held count here instead was the defect: a room of one would
    /// be given a briefing end it does not have, and — through the round's clock hold
    /// below — a round would open that nobody asked for, which is the frozen seven
    /// minutes <c>myserver12.pcapng</c> shows a client from t+62.371 to t+123.973 and
    /// the round <c>myserver17.pcapng</c> opens at t+113.712, 60.3 s after its only
    /// client's report.
    /// </para>
    /// <para>
    /// <b>What reads it is the round's clock hold.</b> A round opened while a
    /// briefing still has time to run holds its clock at the round's whole length
    /// for what is left of it, and a round opened once the briefing's time is out
    /// counts from its own open — as does a round in a room that was never timed.
    /// </para>
    /// </remarks>
    /// <param name="now">The moment to read the clock at.</param>
    /// <param name="remaining">Time left in the briefing, when one is running.</param>
    /// <returns><c>true</c> while the room is being timed and has time left.</returns>
    public bool TimeRemaining(DateTimeOffset now, out TimeSpan remaining)
    {
        lock (gate)
        {
            if (countingFrom is not { } counting || length <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                return false;
            }

            remaining = (counting + length) - now;
            return remaining > TimeSpan.Zero;
        }
    }
}
