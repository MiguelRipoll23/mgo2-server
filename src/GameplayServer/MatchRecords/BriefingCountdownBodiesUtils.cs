namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing countdown: the operations record a peer is sent while its room
/// is still briefing, counting the briefing's remaining seconds down from the
/// room's own briefing time.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the seven-byte <c>0x0b</c> record, not the operations clock.</b> A
/// briefing in <c>mgo2-game-started-on-briefing</c> is written as
/// <c>0b 3c 00 00 00 00 00</c> and counted down — <c>0b 3b 00 02 …</c>,
/// <c>0b 3a …</c> — the opening <c>0x3c</c> being the room's briefing time of
/// sixty seconds. It is the record <c>docs/protocol/STATE_VECTOR.md</c> §4 read
/// as a recurring per-entity timer; the operations clock (<c>01</c>, 128 units)
/// this host wrote instead is a round clock near its end and not a briefing
/// countdown.
/// </para>
/// <para>
/// <b>The bytes.</b> The opcode is followed by the remaining seconds, a zero,
/// the room's reporting players as the same bit set the presence answer carries
/// (<see cref="Rooms.PlayerReadyService.PresenceMask"/>), and three recorded
/// zeros. The set grows as the room reports — <c>00</c>, then <c>02</c>,
/// <c>06</c> — so the countdown is rewritten and not a constant.
/// **[V]** the opcode, the length, the opening sixty and the growth; **[U]** the
/// zero between the count and the set.
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
