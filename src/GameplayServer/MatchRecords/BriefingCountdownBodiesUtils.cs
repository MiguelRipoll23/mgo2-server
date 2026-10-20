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
/// <b>The bytes.</b> The opcode, the remaining seconds as two little-endian bytes,
/// the room's readied players as a two-byte little-endian bit set, and two
/// recorded zeros. Seconds and set move independently: measured over
/// <c>mgo2-game-joined-when-red-team-then-briefing</c>, the seconds tick while the
/// set grows as each player readies — <c>40 00</c>, <c>c0 00</c>, <c>c2 00</c>,
/// <c>ca 00</c>, <c>ea 01</c> — so a ready moves the set and not the count. The
/// set is two bytes (<c>ea 01</c> is <c>0x01ea</c>, bits past the first byte, and
/// the second briefing's <c>02 7c</c> is <c>0x7c02</c>), so the record names slots
/// up to 15.
/// </para>
/// <para>
/// <b>The set is the ready set and not the reports.</b> Its bits are the slots the
/// ready records name (<see cref="BriefingReadyBodiesUtils"/>), which is what makes
/// the countdown the room's publication of who is waiting to play: the room above
/// holds <c>2a</c> — slots 1, 3 and 5 — as its reported players and
/// <c>0x01ea</c> — slots 1, 3, 5, 6, 7 and 8 — as its ready ones, the three extra
/// bits being the host's own bots, which are ready in the record that adds them.
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
    /// Spacing between countdown records: the briefing's one tick, which the rules
    /// pair and the ready set are written on in the same pass.
    /// </summary>
    /// <remarks>
    /// <b>It is the room's tick and not this record's.</b> The reference writes
    /// the countdown in a datagram of its own a millisecond beside the rules pair at
    /// 28 of <c>started-on-briefing</c>'s 31 countdown instants, so the two cannot
    /// be given different periods without drifting off each other:
    /// <see cref="MatchRoundStateService.BriefingTickMilliseconds"/> carries the
    /// measurement and the reason it is 2.35 s rather than the 2.5 s this held.
    /// </remarks>
    public const int PublishPeriodMilliseconds = MatchRoundStateService.BriefingTickMilliseconds;

    /// <summary>Seconds in one minute, the factor between a briefing's minutes and its count.</summary>
    public const int SecondsPerMinute = 60;

    /// <summary>The largest value the count byte can hold.</summary>
    public const int MaxCountSeconds = 0xff;

    /// <summary>Offset of the remaining seconds, little-endian over two bytes.</summary>
    public const int SecondsIndex = 1;

    /// <summary>Offset of the room's ready set, little-endian over two bytes.</summary>
    public const int ReadySetIndex = 3;

    /// <summary>
    /// Whether a body is a briefing countdown record: the opcode at the length
    /// every capture sends.
    /// </summary>
    /// <param name="body">Body of an inbound operations record.</param>
    /// <returns><c>true</c> when the body is a room's briefing countdown.</returns>
    public static bool IsCountdown(ReadOnlySpan<byte> body) =>
        body.Length == RecordLength && body[0] == CountdownOpcode;

    /// <summary>The seconds of briefing the record says the room has left.</summary>
    /// <param name="body">Body of a briefing countdown record.</param>
    /// <returns>The remaining seconds, as the record carries them.</returns>
    public static int SecondsOf(ReadOnlySpan<byte> body) =>
        body[SecondsIndex] | (body[SecondsIndex + 1] << 8);

    /// <summary>
    /// The room's readied players as the record carries them: one bit per player
    /// that has readied, at the slot its own ready record named.
    /// </summary>
    /// <param name="body">Body of a briefing countdown record.</param>
    /// <returns>The ready set, little-endian over two bytes.</returns>
    public static ushort ReadySetOf(ReadOnlySpan<byte> body) =>
        (ushort)((body[ReadySetIndex] | (body[ReadySetIndex + 1] << 8)) & 0xffff);

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
    /// The countdown record's body: the <c>0x0b</c> opcode, the seconds left, the
    /// room's readied players and two zeros.
    /// </summary>
    /// <remarks>
    /// The closing zero is this same body with no seconds on it and the room's whole
    /// ready set, which is the record a client's briefing ends on.
    /// </remarks>
    /// <param name="seconds">Seconds of briefing the room has left.</param>
    /// <param name="readySet">
    /// The room's readied players as a bit set, one bit per player at the slot its
    /// ready record named (<see cref="Rooms.BriefingStateService.ReadyMask"/>).
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] CountdownBody(int seconds, ushort readySet) =>
    [
        CountdownOpcode,
        (byte)Math.Clamp(seconds, 0, MaxCountSeconds),
        0x00,
        (byte)(readySet & 0xff),
        (byte)(readySet >> 8),
        0x00,
        0x00,
    ];
}
