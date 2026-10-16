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
/// <para>
/// <b>The client's own table of record shapes is not the parse of these bodies,</b>
/// and no body here is rewritten from it. The 26 descriptors at
/// <c>*(0x011A646C)</c> are a table of <i>states</i> — 24 slot states of 1248
/// bytes, and two others — and the presence map it reads needs at least eight
/// bytes before the first field: measured over three captures, none of the
/// operations identifier's 2 214 records fits it, neither the two- and
/// three-byte clock and kill feed nor the ninety-byte state record
/// (<c>docs/analysis/udp-record-dispatch.md</c> §3.6,
/// <c>tools/mgo2_state_wire_check.py</c>).
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
    /// Opcode of the operations stream's clock, in the payload's first byte.
    /// </summary>
    /// <remarks>
    /// <b>The clock is the round's time limit.</b> Measured in three rooms: a
    /// round opens with this record set to the rule's own time, and the value
    /// counts down in <see cref="ClockUnitMilliseconds"/> steps until the round
    /// is over. 3 840 units (8:00) in a room whose TDM time is 8, 2 400 (5:00) in
    /// one whose time is 5. A briefing's countdown is a record of its own,
    /// <c>0x0b</c> (<see cref="BriefingCountdownBodiesUtils"/>), and not this one.
    /// </remarks>
    public const byte OperationsClockOpcode = 0x01;

    /// <summary>
    /// Opcode of the operations stream's state block, in the payload's first
    /// byte: eighty-nine state bytes behind it.
    /// </summary>
    /// <remarks>
    /// Two of its bytes are the round's and the match's outcome, and both are
    /// measured in a live capture (<c>docs/protocol/BRIEFING_FLOW.md</c> §4):
    /// <c>state[2]</c> is the round's winner — 2 for raw team 0, 3 for raw team 2,
    /// 1 for a draw, 255 while the round is undecided — and <c>state[3]</c> the
    /// match's, in the same coding. A host writes the block whole and writes
    /// later changes as slices of it, under opcode <c>0x02</c>: an offset, a
    /// count, then the values, so <c>02 02 01 02</c> is "state[2] = 2".
    /// </remarks>
    public const byte OperationsStateOpcode = 0x00;

    /// <summary>
    /// Opcode of the operations stream's state write, in the payload's first
    /// byte: <c>[opcode, startOffset, byteCount, values…]</c> writes the values
    /// into the state block at the offset for the count. A round's result arrives
    /// this way — <c>02 02 01 02</c> is "state[2] = 2" — and the state block
    /// itself was written whole before it.
    /// </summary>
    public const byte OperationsStateWriteOpcode = 0x02;

    /// <summary>
    /// Result code of a round raw team <c>0</c> took. The coding is the replay
    /// decoder's, measured over 28 round ends in 16 replays, and it uses the
    /// game's own team ids: <c>2</c> team 0, <c>3</c> team 2, <c>1</c> drawn and
    /// <c>6</c> no result.
    /// </summary>
    public const byte StateResultTeamZero = 2;

    /// <summary>Result code of a round raw team <c>1</c> took; see <see cref="StateResultTeamZero"/>.</summary>
    public const byte StateResultTeamOne = 3;

    /// <summary>Result code of a round nobody won.</summary>
    public const byte StateResultDrawn = 1;

    /// <summary>
    /// Offset of the round's outcome within the operations state block.
    /// </summary>
    public const int StateRoundResultIndex = 2;

    /// <summary>
    /// Offset of the match's outcome within the operations state block, written
    /// in the same coding as <see cref="StateRoundResultIndex"/>.
    /// </summary>
    /// <remarks>
    /// <b>Measured as a field of its own, and written with the round's.</b> A
    /// working server ends the round that decides a match with one state write
    /// covering both — <c>02 02 02 02 02</c> is "state[2] = 2, state[3] = 2" — and
    /// no capture ever writes one without the other. So a match result is
    /// something this host can write rather than invent; what it reports over the
    /// lobby's TCP channel once the match is decided is the open question, not
    /// this byte (<c>docs/protocol/STATE_VECTOR.md</c> §3).
    /// </remarks>
    public const int StateMatchResultIndex = 3;

    /// <summary>
    /// The result code a raw team's win is written as: team <c>0</c> is the
    /// decoder's code <c>2</c> and team <c>1</c> its code <c>3</c>, since the
    /// coding names the game's own team ids and the room's rows hold sides.
    /// </summary>
    /// <param name="team">Raw team value the room's rows hold, 0 or 1.</param>
    /// <returns>The result code.</returns>
    public static byte ResultCode(short team) =>
        team == 0 ? StateResultTeamZero : StateResultTeamOne;

    /// <summary>
    /// A state write: the opcode, the offset it starts at, the count of values
    /// behind it and the values themselves.
    /// </summary>
    /// <param name="offset">Offset within the state vector the write starts at.</param>
    /// <param name="values">Values to write, in order.</param>
    /// <returns>The record body.</returns>
    public static byte[] StateWrite(int offset, params byte[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return [OperationsStateWriteOpcode, (byte)offset, (byte)values.Length, .. values];
    }

    /// <summary>
    /// The unit the operations clock counts in, in milliseconds: the stream's own
    /// tick, measured as exactly 125 ms on a live capture over the 300 s between
    /// two clock writes.
    /// </summary>
    public const int ClockUnitMilliseconds = 125;

    /// <summary>
    /// Units of the operations clock in one minute: 480, and so the factor
    /// between a room's rule time in minutes and the clock its round opens on.
    /// </summary>
    public const int ClockUnitsPerMinute = 60_000 / ClockUnitMilliseconds;

    /// <summary>
    /// Opcode of the two-byte operations record the match-start run carries. It
    /// is the one opcode the replay parser's table does not name: it reads
    /// <c>0x00</c> state, <c>0x01</c> clock, <c>0x02</c> state write, <c>0x04</c>
    /// objective feed, <c>0x05</c> slot, <c>0x09</c> kill feed, <c>0x0e</c>
    /// round-over, <c>0x12</c> race goal and <c>0x13</c> rating, and has no entry
    /// for this one, so the byte stays the recorded one.
    /// </summary>
    private const byte OperationsShortOpcode = 0x0f;

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
    /// The two-byte operations body the match-start run carries. The replay
    /// parser's opcode table does not name <see cref="OperationsShortOpcode"/>,
    /// so the bytes are the recorded ones.
    /// </summary>
    /// <returns>The record body.</returns>
    public static byte[] OperationsShort() => [OperationsShortOpcode, 0x00];

    /// <summary>
    /// The operations stream's clock body — the opcode and a little-endian count
    /// of 125 ms units — for however far the round's clock has run.
    /// </summary>
    /// <remarks>
    /// <b>The count is the room's own and moves.</b> The record opened a recorded
    /// room's round at 3 360 units, that room's 7-minute TDM time, while three
    /// rooms since carry three opening values and count down from them as the
    /// round plays (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2 and
    /// <c>BRIEFING_FLOW.md</c> §3). It is therefore the caller's: the round's own
    /// rule time × <see cref="ClockUnitsPerMinute"/> when the round opens, and
    /// <see cref="MatchRoundStateService.RemainingUnits"/> on every record
    /// behind it.
    /// </remarks>
    /// <param name="units">Count of <see cref="ClockUnitMilliseconds"/> units.</param>
    /// <returns>The body.</returns>
    public static byte[] RoundTimeLimitUnits(int units) =>
    [
        OperationsClockOpcode,
        (byte)(units & 0xff),
        (byte)((units >> 8) & 0xff),
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
    /// Leading byte of the room-count record. Every captured one is zero, in both
    /// of the two-byte records and in the four-byte ones the same identifier
    /// carries later in a round; what it states is **[U]**.
    /// </summary>
    public const byte RoomCountLeadingByte = 0x00;

    /// <summary>
    /// The room-count record's body: the leading zero and the room's population.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The second byte is how many players the room holds, and the lobby's own
    /// details are what say so.</b> <c>mgo2-game-started-on-briefing</c> joins
    /// <c>Best TDM [EU]</c> — its <c>0x4313</c> reply reads one player of a limit
    /// of seventeen — and its host writes <c>00 01</c> when that peer is
    /// answered and <c>00 02</c> once a second player is in the room
    /// (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §1 for the reply's fields).
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c> writes <c>00 05</c> into
    /// a room of five joiners. So the byte is the room's count and not its limit,
    /// and not the host's own seat, which no captured room counts.
    /// </para>
    /// <para>
    /// It is one byte on the wire, so a room of more than 255 is a count this
    /// record cannot state; the rooms the captures hold are one to five.
    /// </para>
    /// </remarks>
    /// <param name="count">Players the room holds.</param>
    /// <returns>The record body.</returns>
    public static byte[] RoomCount(byte count) => [RoomCountLeadingByte, count];

    /// <summary>
    /// Length of the room-state record that rides beside the close pair: a
    /// twenty-two-byte roster-shaped record, the room's own stage.
    /// </summary>
    public const int RoomStateRecordLength = 22;

    /// <summary>
    /// Length of either room-state entry record: the fifty-four-byte pair the
    /// room's own stage carries behind the record above.
    /// </summary>
    public const int RoomStateEntryLength = 54;

    /// <summary>
    /// The room-state record's bytes behind its roster header. What it states is
    /// **[U]**: `mgo2-game1` and `mgo2-started-briefing-then-solo-game` — two
    /// sessions that share no origin and no client — write these twenty bytes
    /// byte for byte, and `mgo2-game2` writes another twenty, so the body is a
    /// fact about the room's own stage rather than about a session.
    /// </summary>
    private static ReadOnlySpan<byte> RoomStateRecordRemainder =>
    [
        0x00, 0x03, 0x00, 0x00, 0x00, 0x40, 0x7c, 0xfc,
        0x6c, 0x07, 0xac, 0xf4, 0x7c, 0xfc, 0x6c, 0x07,
        0xac, 0xf4, 0xfd, 0x00,
    ];

    /// <summary>The first room-state entry's bytes behind its roster header.</summary>
    private static ReadOnlySpan<byte> RoomStateEntryFirstRemainder =>
    [
        0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x26, 0x01,
        0x00, 0xec, 0x2c, 0x80, 0x38, 0x01, 0x80, 0xb5,
        0x01, 0x00, 0x00, 0x04, 0xc2, 0xb6, 0x1f, 0xf4,
        0x01, 0x58, 0x2d, 0x7d, 0xa0, 0x92, 0x7f, 0xf4,
        0x01, 0x30, 0x87, 0x7d, 0x23, 0x92, 0x1f, 0x77,
        0x01, 0x30, 0xb1, 0x7d, 0x9a, 0x93, 0xbf, 0x0a,
        0x02, 0xf8, 0x3c, 0x7d,
    ];

    /// <summary>The second room-state entry's remainders; its first bytes differ from the first's.</summary>
    private static ReadOnlySpan<byte> RoomStateEntrySecondRemainder =>
    [
        0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x26, 0x01,
        0x00, 0x98, 0x3a, 0x00, 0x00, 0x00, 0x80, 0xcd,
        0x7d, 0x00, 0x00, 0x04, 0x3d, 0xe1, 0x1f, 0xfa,
        0x00, 0x68, 0xbf, 0x00, 0x0b, 0xdf, 0x1f, 0x00,
        0x00, 0xc0, 0x10, 0x7f, 0x7f, 0x0a, 0x00, 0x83,
        0xff, 0x9b, 0x40, 0x7f, 0x54, 0xbe, 0x7f, 0xe2,
        0xff, 0x17, 0x80, 0x7f,
    ];

    /// <summary>
    /// The room's own stage as the room-state record states it: the record the
    /// captures write beside the close pair, in the round's opening run and again
    /// through a briefing.
    /// </summary>
    /// <remarks>
    /// <b>Its body is the recorded one, and it is the room's stage rather than a
    /// session's.</b> The two records below and this one are the same in
    /// `mgo2-game1` and `mgo2-started-briefing-then-solo-game` and differ in
    /// `mgo2-game2`, so they are written from the recording the way the close pair
    /// beside them already is (<see cref="MatchStartCloseFirst"/>): a room on
    /// another stage is served the recorded stage's bytes, which is visible on the
    /// wire and recorded in `docs/analysis/briefing-room-state.md`.
    /// </remarks>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateRecord() =>
        RosterShapedRecord(RoomStateRecordLength, RoomStateRecordRemainder);

    /// <summary>The first room-state entry record.</summary>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateEntryFirst() =>
        RosterShapedRecord(RoomStateEntryLength, RoomStateEntryFirstRemainder);

    /// <summary>The second room-state entry record.</summary>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateEntrySecond() =>
        RosterShapedRecord(RoomStateEntryLength, RoomStateEntrySecondRemainder);

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
        RosterShapedRecord(CloseRecordLength, remainder);

    /// <summary>
    /// Builds a roster-shaped body from its version, its own length less the
    /// fixed prefix, and the recorded bytes behind them.
    /// </summary>
    /// <param name="length">Length of the body on the wire.</param>
    /// <param name="remainder">The recorded bytes after the two mapped ones.</param>
    /// <returns>The record body.</returns>
    private static byte[] RosterShapedRecord(int length, ReadOnlySpan<byte> remainder) =>
    [
        PlayerProfileRecordUtility.RecordVersion,
        (byte)(length - PlayerProfileRecordUtility.FixedPrefixLength),
        .. remainder,
    ];

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
