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
/// <b>What is measured and what is chosen.</b> That it opens on 128, that it
/// reaches zero, that the span in the one capture that holds a full one is 58.5 s
/// in a room whose briefing is a minute, and that no round-length value appears
/// in it — all measured. The mapping of the briefing's own minutes onto the 128
/// units is this host's derivation from those, and it is the one step here that
/// is not a measurement; the capture is consistent with it and does not fix it.
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
    /// Spacing between countdown records, which is the beat's own frame period
    /// group rather than a slow poll.
    /// </summary>
    /// <remarks>
    /// The capture sends the briefing clock on the same cadence as the round's —
    /// the operations insertion at <c>MatchRecordFramesUtils</c>'s
    /// <c>OperationsPeriodFrames</c> — so the countdown is written per insertion
    /// and not on a timer of its own.
    /// </remarks>
    public const int PublishPeriodMilliseconds = 2000;

    /// <summary>
    /// The countdown's own value at a point in a briefing: the whole 128 units at
    /// its start, zero at its end, and the briefing's remaining share of its own
    /// time between.
    /// </summary>
    /// <param name="elapsed">How long the briefing has run.</param>
    /// <param name="length">How long the briefing runs for.</param>
    /// <returns>A value in <c>0</c>…<see cref="BriefingClockUnits"/>.</returns>
    public static int CountdownUnits(TimeSpan elapsed, TimeSpan length)
    {
        if (length <= TimeSpan.Zero)
        {
            return BriefingClockUnits;
        }

        var remaining = length - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        // Rounded rather than truncated, so a briefing half over reads 64 units
        // and not 63, and the value reaches zero on the last step rather than one
        // step after the briefing ends.
        var units = (int)Math.Round(
            BriefingClockUnits * (double)remaining.Ticks / length.Ticks,
            MidpointRounding.AwayFromZero);
        return Math.Clamp(units, 0, BriefingClockUnits);
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
