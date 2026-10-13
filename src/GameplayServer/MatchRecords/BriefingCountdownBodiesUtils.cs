namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing countdown: what a peer is sent on the operations clock while
/// its room is still briefing.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the clock record, not a record of its own.</b> The room's briefing
/// time was the deadline its round opens on and the duration it waits for a
/// report that never comes, and it reached no client at all. It does reach one
/// through the operations clock: in
/// <c>mgo2-game-joined-when-red-team-then-briefing</c> the clock reads
/// <c>0x0080</c> — 128 units — at the start of the briefing and counts to zero
/// over 58.5 s while the client sits in it, 14 s before that room's round opens
/// at t+72.560. That is the countdown, and it is the same opcode, the same
/// little-endian 125 ms unit and the same stream as the round clock that follows
/// it — a briefing is a round's clock opened on 128 units rather than on the
/// round's own length.
/// </para>
/// <para>
/// <b>It is built from the room's own briefing time.</b> A client waits in a
/// briefing either for every player to be ready or for its own timer, so the
/// countdown is how a client learns which of the two it is about to be. The
/// value is the briefing's remaining share of its own time in the stream's own
/// 125 ms units, read per record, so a briefing that opens late or is cut short
/// by its deadline counts down against the time it actually has.
/// </para>
/// <para>
/// <b>The clock is a fixed sixteen seconds, and the values are the steps it
/// reaches them in.</b> Measured: it opens on 128 and reads <c>128 → 96 → 64 → 32 →
/// 0</c> over <b>16.2 s</b> (<c>mgo2-game-joined-when-red-team-then-briefing</c>,
/// t+11.671 to t+27.915, read with <c>tools/udp_briefing_flow.py</c>), and the round
/// that briefing precedes opens at t+72.560 — so the clock reaches zero about 45 s
/// before the round does and **stays at zero** while the room waits, and it is
/// **not** the briefing's length. The five readings are exactly the quarter steps of
/// sixteen seconds: four seconds apart, <c>128 − 32</c> each. An earlier reading
/// here said it counted to zero over 58.5 s and spread 128 units across the room's
/// briefing time; the same tool's output contradicts that, and this host no longer
/// writes it.
/// </para>
/// <para>
/// <b>What is derived and what is measured.</b> **[V]** the opening value, the four
/// steps, the 16 s span and the zeros that follow. **[D]** that the step is the
/// clock's own grid rather than the client's display: the four values are the ones
/// the wire carries, and a host that wrote a value per 125 ms tick would write the
/// same clock in a hundred and twenty-eight records instead of five.
/// </para>
/// <para>
/// The other candidate on the wire is **not** the briefing countdown.
/// <c>docs/protocol/STATE_VECTOR.md</c> §4 records the measurement that cleared
/// it: the six-byte <c>0x0b</c> record counts down from sixty and was read as a
/// briefing timer, but it continues past a round's opening run and restarts
/// afterwards, so it is a recurring per-entity timer of its own and this host
/// writes none of it.
/// </para>
/// </remarks>
public static class BriefingCountdownBodiesUtils
{
    /// <summary>
    /// The units a briefing's clock opens on: 128, which is sixteen seconds of
    /// the stream's own tick.
    /// </summary>
    public const int BriefingClockUnits = 128;

    /// <summary>
    /// Spacing between countdown records. The capture writes the briefing clock
    /// 2.4–4.6 s apart (its readings at t+11.671, 14.179, 18.808, 21.324, 23.790
    /// and 27.915), which is the operations tick the round's own clock is written
    /// on rather than a period the client is sent on.
    /// </summary>
    public const int PublishPeriodMilliseconds = 2500;

    /// <summary>
    /// The stream's own tick in a clock value: 125 ms per unit.
    /// </summary>
    public const int UnitMilliseconds = 125;

    /// <summary>
    /// Units one step of the countdown takes off: 32, which is four seconds of the
    /// stream's own tick. The capture's five readings are the four steps of its
    /// sixteen-second clock.
    /// </summary>
    public const int CountdownStepUnits = BriefingClockUnits / 4;

    /// <summary>
    /// How long the countdown takes off the whole clock: sixteen seconds of the
    /// stream's own tick, which is the span the capture's readings cover.
    /// </summary>
    public static readonly TimeSpan CountdownLength =
        TimeSpan.FromMilliseconds((long)BriefingClockUnits * UnitMilliseconds);

    /// <summary>
    /// The countdown's own value at a point in a briefing: the whole 128 units in
    /// its first four seconds, one step less every four seconds after that, and
    /// zero from sixteen seconds in — where it stays for the rest of the briefing,
    /// because the capture's clock reaches zero ~45 s before its round opens.
    /// </summary>
    /// <param name="elapsed">How long the briefing has run.</param>
    /// <returns>A value in <c>0</c>…<see cref="BriefingClockUnits"/>.</returns>
    public static int CountdownUnits(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return BriefingClockUnits;
        }

        var stepLength = CountdownLength / (BriefingClockUnits / CountdownStepUnits);
        var steps = (int)(elapsed.Ticks / stepLength.Ticks);
        return Math.Clamp(BriefingClockUnits - (steps * CountdownStepUnits), 0, BriefingClockUnits);
    }

    /// <summary>
    /// The countdown record's body: the operations clock opcode and the value,
    /// little-endian. The same builder the round clock uses, because it is the
    /// same record — a briefing is a clock opened on 128 units.
    /// </summary>
    /// <param name="units">Count of 125 ms units the countdown reads.</param>
    /// <returns>The record body.</returns>
    public static byte[] CountdownBody(int units) =>
        MatchRecordBodiesUtils.RoundTimeLimitUnits(units);
}
