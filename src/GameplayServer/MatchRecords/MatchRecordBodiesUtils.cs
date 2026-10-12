using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The bodies the recorded host writes in its match records, as measured from
/// the solo window of <c>docs/mgo2-game.pcapng</c>. Every one of them was
/// checked byte for byte against the capture.
/// </summary>
/// <remarks>
/// <para>
/// Where the replay console's parser names the fields, the body is built from
/// them rather than copied: the round clock is its opcode and its value, the
/// vitals record is health and stamina, the rules-roster record is the list-end
/// byte. Where the parser runs out — the operations state block's vector, the
/// match-start run's entries — the bytes are the recorded ones and the doc
/// comment says which part is mapped. The mapping is
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>.
/// </para>
/// </remarks>
public static class MatchRecordBodiesUtils
{
    /// <summary>Health, and stamina, the recorded vitals record carries: 250, both full.</summary>
    public const byte VitalsFull = 0xfa;

    /// <summary>
    /// The byte that ends a rules-roster list, the replay parser's
    /// <c>LIST_END</c>, which the empty list is alone.
    /// </summary>
    public const byte RulesListEnd = 0xfe;

    /// <summary>
    /// Opcode the operations stream uses for its round clock, in the payload's
    /// first byte.
    /// </summary>
    public const byte OperationsClockOpcode = 0x01;

    /// <summary>
    /// Opcode the operations stream uses for its state block, in the payload's
    /// first byte.
    /// </summary>
    public const byte OperationsStateOpcode = 0x00;

    /// <summary>
    /// The clock the recorded operations record carries, in the stream's own
    /// 125 ms units: 3 360, so 420 s. Every one of the round's clock records
    /// carries this same value.
    /// </summary>
    public const int RoundClockUnits = 3360;

    /// <summary>
    /// Opcode of the two-byte operations record the match-start run carries. It
    /// is the one opcode the replay parser's table does not name: it reads
    /// <c>0x00</c> state, <c>0x01</c> clock, <c>0x02</c> state write, <c>0x04</c>
    /// objective feed, <c>0x05</c> slot, <c>0x09</c> kill feed, <c>0x0e</c>
    /// round-over, <c>0x12</c> race goal and <c>0x13</c> rating, and has no entry
    /// for this one, so the byte stays the recorded one.
    /// </summary>
    private const byte OperationsShortOpcode = 0x0f;

    /// <summary>
    /// Payload type of the team array, the first record of the pair the
    /// match-start run carries at index bytes 7 and 8.
    /// </summary>
    public const byte TeamArrayType = 0x85;

    /// <summary>Payload type of the occupancy array, the second record of the pair.</summary>
    public const byte OccupancyArrayType = 0x86;

    /// <summary>Body length of either record of the pair: the type, a sequence byte, 24 slot bytes.</summary>
    public const int TeamArrayLength = 26;

    /// <summary>
    /// Slots the array carries, counting slot 0 — the byte the replay parser
    /// never reads.
    /// </summary>
    public const int TeamArraySlots = 24;

    /// <summary>
    /// Occupancy value that marks a slot as taken, in the occupancy array. It is
    /// the only value the parser accepts there, and the team array's byte at the
    /// same offset is then the slot's team, 0 or 1.
    /// </summary>
    public const byte OccupancyArrayOccupied = 0x07;

    /// <summary>Occupancy value of a free slot, which all three captures write.</summary>
    public const byte OccupancyArrayVacant = 0x7f;

    /// <summary>Team array's byte for a slot nobody holds, as the captures write it.</summary>
    public const byte TeamArrayVacant = 0xff;

    /// <summary>Slot 0's byte in the team array — the sentinel the parser never reads.</summary>
    public const byte TeamArraySlotZero = 0xfe;

    /// <summary>
    /// The sequence byte both records of a pair carry; the parser requires the
    /// two to agree and their index bytes to be consecutive. It is per-array
    /// rather than constant: the capture writes 0xf3 here and 0xf4 for its later
    /// arrays, while `mgo2-game2` uses 0x00 and 0x01 and `mgo2-game3` 0x6b.
    /// </summary>
    public const byte MatchStartTeamArraySequence = 0xf3;

    /// <summary>Control byte of an early sparse-phase record.</summary>
    private static ReadOnlySpan<byte> ControlBodyEarly => [0x02];

    /// <summary>Control byte of an alternating sparse-phase record.</summary>
    private static ReadOnlySpan<byte> ControlBodyAlternate => [0x03];

    /// <summary>Control byte a steady-phase record carries.</summary>
    private static ReadOnlySpan<byte> ControlBodySteady => [0x04];

    /// <summary>One-byte control body seen once in the steady phase.</summary>
    private static ReadOnlySpan<byte> ControlBodyRare => [0x25];

    /// <summary>Length of either record of the match-start run's close pair.</summary>
    public const int CloseRecordLength = 20;

    /// <summary>
    /// The eighteen bytes behind the pair's roster header: what the two records
    /// differ by and what nothing names.
    /// </summary>
    private static ReadOnlySpan<byte> CloseRecordFirstRemainder =>
    [
        0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x02, 0x42,
        0xf5, 0x9f, 0xda, 0xff, 0xc3, 0xec, 0x7d, 0x00, 0x00, 0x00,
    ];

    /// <summary>The second record's remainder, which differs from the first's.</summary>
    private static ReadOnlySpan<byte> CloseRecordSecondRemainder =>
    [
        0x00, 0x02, 0x01, 0x00, 0x00, 0x00, 0x02, 0xf4,
        0x01, 0xc0, 0xea, 0xff, 0x83, 0xaf, 0x02, 0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// Opcode a client's readiness report carries, in the record's first byte.
    /// The host's reply to the same record begins
    /// <see cref="MatchStartPayloadOpcode"/> in that byte, which is what makes the
    /// two a report and its answer rather than two readings of one record.
    /// </summary>
    /// <remarks>
    /// The report is the client saying it has loaded and is ready to play. That
    /// the client means *ready* specifically is **[I]**: the timing and the
    /// pairing are measured, the intent behind them is not. It is recognised and
    /// logged, and nothing is written back: the round start that answered it is
    /// never sent (see <see cref="MatchRecordService"/>).
    /// </remarks>
    public const byte ReadinessReportOpcode = 0xe1;

    /// <summary>Opcode the host's match-start payload carries in its first byte.</summary>
    public const byte MatchStartPayloadOpcode = 0xe0;

    /// <summary>
    /// Byte a round's run echoes when the peer it is written to has not reported
    /// itself ready: the recorded host's own, <c>0x14</c>, from game1's first
    /// round's <c>0xe0 0x14</c>. Nothing echoes it now, because nothing writes a
    /// round start; it is kept as the measured value a future run would echo.
    /// </summary>
    public const byte DefaultReadinessReportValue = 0x14;

    /// <summary>
    /// Length of the readiness report: the opcode and the one byte the host
    /// echoes back. Every report in the captures is exactly this long.
    /// </summary>
    public const int ReadinessReportLength = 2;

    /// <summary>
    /// Offset of the byte the report carries and the reply echoes.
    /// </summary>
    public const int ReadinessReportValueIndex = 1;

    /// <summary>
    /// The two-byte operations body the match-start run carries. The replay
    /// parser's opcode table does not name <see cref="OperationsShortOpcode"/>,
    /// so the bytes are the recorded ones.
    /// </summary>
    /// <returns>The record body.</returns>
    public static byte[] OperationsShort() => [OperationsShortOpcode, 0x00];

    /// <summary>The round clock's body, built from the two fields it carries.</summary>
    /// <returns>The body: the clock opcode, then its units little-endian.</returns>
    public static byte[] RoundClock() =>
    [
        OperationsClockOpcode,
        (byte)(RoundClockUnits & 0xff),
        (byte)((RoundClockUnits >> 8) & 0xff),
    ];

    /// <summary>The vitals record's body: health and stamina, both full.</summary>
    /// <returns>The record body.</returns>
    public static byte[] Vitals() => [VitalsFull, VitalsFull];

    /// <summary>
    /// The empty rules-roster list: the recorded host's beat frame carries this
    /// body, so nothing in it names a live player key.
    /// </summary>
    /// <returns>The record body.</returns>
    public static byte[] RulesRoster() => [RulesListEnd];

    /// <summary>
    /// The team array of the match-start run: its type, the pair's sequence
    /// byte, slot 0's sentinel, and a team byte for each of the other
    /// twenty-three slots. The capture's array has no slot taken, so each of
    /// them is <see cref="TeamArrayVacant"/>. The parser takes a pair only when
    /// the occupancy array beside it marks the same slot with
    /// <see cref="OccupancyArrayOccupied"/> and the team byte is 0 or 1; all
    /// three captures satisfy it, eighty-one pairs between them.
    /// </summary>
    /// <returns>The record body.</returns>
    public static byte[] TeamArray() => TeamArrayOf(TeamArrayType, TeamArraySlotZero);

    /// <summary>
    /// The occupancy array of the match-start run: the same shape, with
    /// <see cref="OccupancyArrayVacant"/> in every slot but slot 0.
    /// </summary>
    /// <returns>The record body.</returns>
    public static byte[] OccupancyArray() => TeamArrayOf(OccupancyArrayType, OccupancyArrayVacant);

    /// <summary>
    /// Builds one record of the pair from its fields: the type, the sequence
    /// byte, slot 0's byte, and the twenty-three slots behind it.
    /// </summary>
    /// <param name="type">One of <see cref="TeamArrayType"/> or <see cref="OccupancyArrayType"/>.</param>
    /// <param name="slotZero">Slot 0's byte, which the parser never reads.</param>
    /// <returns>The record body.</returns>
    private static byte[] TeamArrayOf(byte type, byte slotZero)
    {
        var body = new byte[TeamArrayLength];
        body[0] = type;
        body[1] = MatchStartTeamArraySequence;
        body[2] = slotZero;
        body.AsSpan(3).Fill(type == OccupancyArrayType ? OccupancyArrayVacant : TeamArrayVacant);
        return body;
    }

    /// <summary>The first record of the match-start run's close pair.</summary>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartCloseFirst() => CloseRecord(CloseRecordFirstRemainder);

    /// <summary>The second record of the match-start run's close pair.</summary>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartCloseSecond() => CloseRecord(CloseRecordSecondRemainder);

    /// <summary>
    /// Builds one record of the close pair from its two mapped bytes and the 18
    /// recorded ones behind them. The pair opens the way every roster record
    /// does — the record version, then the record's own length less seven — so
    /// those two are written rather than copied; the parser reads a roster record
    /// only from 58 bytes up, so it decodes nothing further here.
    /// </summary>
    /// <param name="remainder">The recorded bytes after the two mapped ones.</param>
    /// <returns>The record body.</returns>
    private static byte[] CloseRecord(ReadOnlySpan<byte> remainder) =>
    [
        PlayerProfileRecordUtility.RecordVersion,
        (byte)(CloseRecordLength - PlayerProfileRecordUtility.FixedPrefixLength),
        .. remainder,
    ];

    /// <summary>
    /// The host's payload record, the answer to a client's readiness report: the
    /// payload opcode and the byte the client reported, then the trailing zeros
    /// the recorded bodies carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The echo is the whole of the answer this server can read. Across the
    /// captures every report/answer pair agrees on that one byte — <c>e1 14</c>
    /// is answered <c>e0 14 …</c> and <c>e1 01</c> is answered <c>e0 01 …</c> —
    /// and answering with anything but the client's own byte would be a value
    /// this server has no source for. What the byte means is **[U]**: game1
    /// sends 0x14 and game2 0x01 on the same record, so it is neither a
    /// constant nor a count this server can derive.
    /// </para>
    /// <para>
    /// <b>The trailing length is not settled.</b> game1 answers in six bytes
    /// (<c>e0 14 00 00 00 00</c>) and game2 in five (<c>e0 01 00 00 00</c>), on
    /// the same record and the same report shape; game1 later grows the same
    /// record to fourteen bytes mid-round. Nothing measured here separates the
    /// two — both captures hold four roster entries and neither occupies a slot
    /// in the array that opens the run — so this writes game1's six-byte form,
    /// the capture the rest of the replay is measured from, rather than
    /// inventing a rule. A round opened against a game2-shaped host would carry
    /// one zero more than that host writes. Recorded as **[U]** in
    /// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2.
    /// </para>
    /// </remarks>
    /// <param name="reported">Byte the client's readiness report carried.</param>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartPayload(byte reported) =>
        [MatchStartPayloadOpcode, reported, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    /// Whether a body is a client's readiness report: the report opcode followed
    /// by the byte the host echoes back.
    /// </summary>
    /// <param name="body">Body of an inbound payload record.</param>
    /// <returns><c>true</c> when the body says the client is ready to play.</returns>
    public static bool IsReadinessReport(ReadOnlySpan<byte> body) =>
        body.Length == ReadinessReportLength && body[0] == ReadinessReportOpcode;

    /// <summary>Body of the early sparse-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] EarlyControl() => [.. ControlBodyEarly];

    /// <summary>Body of the alternating sparse-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] AlternateControl() => [.. ControlBodyAlternate];

    /// <summary>Body of the steady-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] SteadyControl() => [.. ControlBodySteady];

    /// <summary>Body of the control record seen once in the steady phase.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] RareControl() => [.. ControlBodyRare];
}
