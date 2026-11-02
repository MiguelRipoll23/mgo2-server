using Mgo2Server.Shared.Types;
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
/// bytes, and two others — and the state map it reads needs at least eight
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
    public const byte OperationsClockOpcode = (byte)OperationsOpcode.RoundClock;

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
    public const byte OperationsStateOpcode = (byte)OperationsOpcode.StateBlock;

    /// <summary>
    /// Opcode of the operations stream's state write, in the payload's first
    /// byte: <c>[opcode, startOffset, byteCount, values…]</c> writes the values
    /// into the state block at the offset for the count. A round's result arrives
    /// this way — <c>02 02 01 02</c> is "state[2] = 2" — and the state block
    /// itself was written whole before it.
    /// </summary>
    public const byte OperationsStateWriteOpcode = (byte)OperationsOpcode.StateWrite;

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
    private const byte OperationsShortOpcode = (byte)OperationsOpcode.UnnamedShort;

    /// <summary>Control byte of an early sparse-phase record.</summary>
    private static ReadOnlySpan<byte> ControlBodyEarly => [0x02];

    /// <summary>Control byte of an alternating sparse-phase record.</summary>
    private static ReadOnlySpan<byte> ControlBodyAlternate => [0x03];

    /// <summary>Control byte a steady-phase record carries.</summary>
    private static ReadOnlySpan<byte> ControlBodySteady => [0x04];

    /// <summary>One-byte control body seen once in the steady phase.</summary>
    private static ReadOnlySpan<byte> ControlBodyRare => [0x25];

    /// <summary>
    /// The zero byte every record of this family carries between its length and
    /// its kind, and the two header bytes that follow the kind.
    /// </summary>
    private const byte HeaderZero = 0x00;

    /// <summary>The header's slot byte: nought, these records name no player.</summary>
    private const byte NoPlayerSlot = 0x00;

    /// <summary>The header's branch byte: nought hands the payload to the kind's node.</summary>
    private const byte PayloadBranch = 0x00;

    /// <summary>The header's index of a pair's first record, which its payload tag repeats.</summary>
    internal const byte FirstRecordIndex = 0x00;

    /// <summary>Index of a pair's second record: one, and its payload tag with it.</summary>
    internal const byte SecondRecordIndex = 0x01;

    /// <summary>
    /// Kind of the close pair: a region with a count of no corners, which is the host
    /// closing one rather than describing it.
    /// </summary>
    private const byte CloseKind = (byte)RoomRecordKind.Box;

    /// <summary>Length of either record of the match-start run's close pair.</summary>
    public const int CloseRecordLength = 20;

    /// <summary>
    /// The first close record's twelve bytes behind its tag: the box the pair
    /// closes, whose fields are **[U]**.
    /// </summary>
    /// <remarks>
    /// Kind <c>0x02</c> is the box shape and its corner count is nought, so what
    /// these bytes state is the box itself rather than a corner of it
    /// (<c>docs/analysis/roster-record-kinds.md</c> §6). They are the recording's.
    /// </remarks>
    private static ReadOnlySpan<byte> CloseFirstPayload =>
        [0x02, 0xd2, 0x5a, 0x00, 0x00, 0x00, 0xe0, 0x8f, 0x7e, 0x00, 0x00, 0x00];

    /// <summary>The second close record's twelve bytes behind its tag.</summary>
    private static ReadOnlySpan<byte> CloseSecondPayload =>
        [0x02, 0x34, 0xa4, 0x1f, 0x00, 0x00, 0x18, 0x6e, 0x01, 0x00, 0x00, 0x00];

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

    /// <summary>The first record of the match-start run's close pair.</summary>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartCloseFirst() =>
        RosterShapedRecord(
            CloseRecordLength,
            CloseKind,
            FirstRecordIndex,
            FirstRecordIndex,
            CloseFirstPayload);

    /// <summary>The second record of the match-start run's close pair.</summary>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartCloseSecond() =>
        RosterShapedRecord(
            CloseRecordLength,
            CloseKind,
            SecondRecordIndex,
            SecondRecordIndex,
            CloseSecondPayload);

    /// <summary>
    /// Builds one record of the family from its header fields and the payload
    /// behind them: the version, the record's own length less the fixed prefix,
    /// the zero, the kind, the index, the slot, the branch byte, the payload's tag
    /// and then the payload.
    /// </summary>
    /// <param name="length">Length of the body on the wire.</param>
    /// <param name="kind">Kind that indexes the client's stage object.</param>
    /// <param name="index">The record's index within its pair.</param>
    /// <param name="tag">The payload's own first byte, which the index repeats.</param>
    /// <param name="payload">The bytes behind the tag.</param>
    /// <returns>The record body.</returns>
    internal static byte[] RosterShapedRecord(
        int length,
        byte kind,
        byte index,
        byte tag,
        ReadOnlySpan<byte> payload) =>
    [
        PlayerProfileRecordUtility.RecordVersion,
        (byte)(length - PlayerProfileRecordUtility.FixedPrefixLength),
        HeaderZero,
        kind,
        index,
        NoPlayerSlot,
        PayloadBranch,
        tag,
        .. payload,
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
