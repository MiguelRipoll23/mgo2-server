namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing's player-state record: the nine-byte body a host writes beside
/// each of its ready-state publications, one per reporting player.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured, 3 November 2026, on a working server's own session.</b> The
/// capture <c>.pcapngs/briefing/working server/working-server-log.pcapng</c>
/// holds eleven copies, one for each player as that player's <c>e0</c>
/// publication goes out, at t+508.7 to t+513.5, and every one of them is this
/// body with its own slot:
/// </para>
/// <code>
/// 00 &lt;slot&gt; 02 60 09 06 00 c4 09
/// </code>
/// <para>
/// <b>The slot is the same byte the publication echoes</b> — the reporting
/// player's own lobby slot, which its join report carried — and the record
/// travels in the same datagram as that publication at the same fourth byte:
/// <c>0x1b06</c> ordinal 9 beside <c>0x1ade e0 0f 00 00 00</c> ordinal 9
/// (t+512.10). The client's own ready is <c>0x9b06 02 &lt;slot&gt;</c>, which
/// the host answers <c>0xdb06 06</c>; neither direction is written here.
/// </para>
/// <para>
/// <b>The word at offset 3 is the round's whole clock.</b> It is
/// <c>60 09</c> = 2 400 in that room, and the same capture's operations clock
/// opens the round that follows the briefing at exactly 2 400 (t+572.4), so the
/// value is the room's round time in the stream's 125 ms units
/// (<see cref="MatchRecordBodiesUtils.ClockUnitMilliseconds"/>) and not a
/// constant. It is therefore the caller's, read from the round the briefing
/// precedes.
/// </para>
/// <para>
/// <b>Two words are the recording's and are not explained.</b> <c>06 00</c> and
/// <c>c4 09</c> (2 500) are the same in all eleven copies and move with nothing
/// that measurement could see, so they are written as measured **[U]** rather
/// than derived. A capture that moves either is what would name them.
/// </para>
/// <para>
/// This repository carried no record of the family before this measurement: it
/// is the third record a briefing writes, with the ready publication and the
/// countdown, and no host here wrote it (<c>docs/analysis/briefing-screen-3-november.md</c>
/// §2).
/// </para>
/// </remarks>
public static class BriefingReadyStateBodiesUtils
{
    /// <summary>Length of the body on the wire: nine bytes in every measured copy.</summary>
    public const int RecordLength = 9;

    /// <summary>The body's first byte, zero in every measured copy.</summary>
    public const byte Version = 0x00;

    /// <summary>
    /// Offset of the player's own lobby slot, which is the byte its join report
    /// carried and the byte the publication beside it echoes.
    /// </summary>
    public const int SlotIndex = 1;

    /// <summary>
    /// The state byte every measured copy carries. What it states is **[U]**: it
    /// is the same for a player that has reported and one that has readied, so it
    /// is not the briefing's ready state.
    /// </summary>
    public const byte State = 0x02;

    /// <summary>Offset of the round clock, a little-endian 16-bit count of 125 ms units.</summary>
    public const int RoundUnitsIndex = 3;

    /// <summary>
    /// The word at offset 5, identical in all eleven measured copies. **[U]** what
    /// it states.
    /// </summary>
    public const ushort RecordedWord = 0x0006;

    /// <summary>
    /// The word at offset 7, identical in all eleven measured copies — 2 500.
    /// **[U]** what it states.
    /// </summary>
    public const ushort RecordedTailWord = 0x09c4;

    /// <summary>
    /// The record one reporting player's publication carries: its slot, this
    /// host's round clock and the recording's two unexplained words.
    /// </summary>
    /// <param name="slot">The player's own lobby slot, as its report carried it.</param>
    /// <param name="roundUnits">The round clock, in 125 ms units.</param>
    /// <returns>The record body.</returns>
    public static byte[] Body(byte slot, int roundUnits) =>
    [
        Version,
        slot,
        State,
        (byte)(roundUnits & 0xff),
        (byte)((roundUnits >> 8) & 0xff),
        (byte)(RecordedWord & 0xff),
        (byte)((RecordedWord >> 8) & 0xff),
        (byte)(RecordedTailWord & 0xff),
        (byte)((RecordedTailWord >> 8) & 0xff),
    ];
}
