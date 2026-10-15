namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing countdown: the operations record a peer is sent while its room
/// is still briefing, counting the briefing's remaining seconds down from the
/// room's own briefing time.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the seven-byte <c>0x0b</c> record, not the operations clock.</b> A
/// briefing writes <c>0b 3c 00 …</c> — the opening <c>0x3c</c> being the room's
/// briefing time in seconds, sixty, and fifty-five (<c>0b 37</c>) in
/// <c>mgo2-started-briefing-then-solo-game</c>. The operations clock
/// (<c>0x01</c>, 125 ms units) is the round's own and not a briefing countdown
/// (<see cref="MatchRecordBodiesUtils"/>), which is where an earlier reading of
/// this stream went wrong.
/// </para>
/// <para>
/// <b>The bytes.</b> The opcode, the remaining seconds, a zero, the room's
/// reporting players as a little-endian bit set, and two recorded zeros. Seconds
/// and set move independently: measured over
/// <c>mgo2-game-joined-when-red-team-then-briefing</c>, the seconds tick while the
/// set grows as each player reports — <c>40 00</c>, <c>c0 00</c>, <c>c2 00</c>,
/// <c>ca 00</c>, <c>ea 01</c> — so a report moves the set and not the count. The
/// set is **at least two bytes** (the <c>ea 01</c> above is <c>0x01ea</c>, bits
/// past the first byte), which is why a room of up to twenty-four slots fits it.
/// </para>
/// <para>
/// <b>It is held at the whole briefing time until the room has a second player,
/// and it ends on a zero when the room is ready.</b> A room of one has no briefing
/// to run out, so the count does not move
/// (<see cref="BriefingCountdownService.IsCounting"/>); once a second player has
/// reported it counts, and the instant the room is fully ready the host writes
/// <c>0b 00 00 &lt;full set&gt; …</c> — the set complete and the count at zero.
/// That zero is the round's opening and not the count running out:
/// <c>joined-when-red-team</c> opened with twenty-three still on the ticking
/// records beside it. **[V]** the opcode, the length, the opening sixty/fifty-five,
/// the freeze, the growing set and the closing zero; **[U]** the zero between the
/// count and the set.
/// </para>
/// </remarks>
public static class BriefingCountdownBodiesUtils
{
    /// <summary>
    /// Opcode of the briefing countdown record, in its first body byte.
    /// </summary>
    public const byte CountdownOpcode = 0x0b;

    /// <summary>Length of the countdown body: the opcode and six bytes.</summary>
    public const int RecordLength = 7;

    /// <summary>
    /// Spacing between countdown records, the operations tick the rest of the
    /// stream is written on.
    /// </summary>
    public const int PublishPeriodMilliseconds = 2500;

    /// <summary>Seconds in one minute, the factor between a briefing's minutes and its count.</summary>
    public const int SecondsPerMinute = 60;

    /// <summary>The largest value the count byte can hold.</summary>
    public const int MaxCountSeconds = 0xff;

    /// <summary>
    /// The countdown's value at a point in a briefing: the seconds of briefing
    /// left, rounded up so the last second is still told, held to the byte the
    /// record has room for.
    /// </summary>
    /// <param name="remaining">Time left in the briefing.</param>
    /// <returns>A value in <c>0</c>…<see cref="MaxCountSeconds"/>.</returns>
    public static int CountdownSeconds(TimeSpan remaining) =>
        Math.Clamp((int)Math.Ceiling(remaining.TotalSeconds), 0, MaxCountSeconds);

    /// <summary>
    /// The countdown record's body: the <c>0x0b</c> opcode, the seconds left, a
    /// zero, the room's reporting players and three zeros.
    /// </summary>
    /// <param name="seconds">Seconds of briefing the room has left.</param>
    /// <param name="reportingSet">
    /// The room's reporting players as a bit set, one bit per player
    /// (<see cref="Rooms.PlayerReadyService.PresenceMask"/>).
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] CountdownBody(int seconds, byte reportingSet) =>
    [
        CountdownOpcode,
        (byte)Math.Clamp(seconds, 0, MaxCountSeconds),
        0x00,
        reportingSet,
        0x00,
        0x00,
        0x00,
    ];
}
