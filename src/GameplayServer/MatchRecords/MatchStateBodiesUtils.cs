namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The state blob and the operations state block — the two bodies that carry a
/// round's state rather than its roster — as measured from the solo window of
/// <c>mgo2-game1.pcapng</c>.
/// </summary>
/// <remarks>
/// Split out of <see cref="MatchRecordBodiesUtils"/> so neither file exceeds the
/// repository's size limit. Both bodies are built here and consumed there; the
/// field names and offsets are the replay parser's, mapped in
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>.
/// </remarks>
public static class MatchStateBodiesUtils
{
    /// <summary>Length of a state blob: every recorded one is this long.</summary>
    public const int StateBlobLength = 22;

    /// <summary>Offset of the marker byte inside a state blob.</summary>
    public const int StateBlobMarkerOffset = 18;

    /// <summary>Marker byte every recorded state blob carries.</summary>
    public const byte StateBlobMarker = 0x40;

    /// <summary>Offset of a state blob's only varying field, a big-endian 16-bit value.</summary>
    public const int StateBlobValueOffset = 20;

    /// <summary>Value the sparse phase's blobs carry.</summary>
    public const ushort SparseStateBlobValue = 100;

    /// <summary>Length of the state vector the state block's opcode introduces.</summary>
    public const int StateVectorLength = 89;

    /// <summary>Length of an operations state block: the opcode and the vector.</summary>
    public const int StateBlockLength = 1 + StateVectorLength;

    /// <summary>
    /// Result the round ended on: 2 for raw team 0, 3 for raw team 2, 1 for a
    /// draw and 6 for no result.
    /// </summary>
    public const int StateRoundResultIndex = 2;

    /// <summary>Result of the last round, in the same coding as <see cref="StateRoundResultIndex"/>.</summary>
    public const int StateLastRoundResultIndex = 3;

    /// <summary>First of the two objective-holder bytes the parser reads.</summary>
    public const int StateObjectiveHolderFirstIndex = 40;

    /// <summary>Second of the two objective-holder bytes the parser reads.</summary>
    public const int StateObjectiveHolderSecondIndex = 41;

    /// <summary>First of the two capture-clock bytes, which only the Capture mode reads.</summary>
    public const int StateCaptureClockFirstIndex = 42;

    /// <summary>Second of the two capture-clock bytes.</summary>
    public const int StateCaptureClockSecondIndex = 43;

    /// <summary>Low byte of the round clock the state vector carries, in 125 ms units.</summary>
    public const int StateClockLowIndex = 87;

    /// <summary>High byte of the round clock the state vector carries.</summary>
    public const int StateClockHighIndex = 88;

    /// <summary>
    /// Value an objective-holder byte carries while nobody holds the objective:
    /// the parser's <c>HOLDER_NONE</c>.
    /// </summary>
    public const byte StateObjectiveHolderNone = 0xff;

    /// <summary>
    /// Value a state field carries before the host writes it, which the parser
    /// reads as nothing written.
    /// </summary>
    public const byte StateUnwritten = 0xff;

    /// <summary>
    /// Builds a state blob from its two fields: eighteen zero bytes, the marker,
    /// a zero, and the value big-endian. Only the value moves across the round's
    /// 141 blobs, which is why the record is built rather than copied twice.
    /// </summary>
    /// <remarks>
    /// The capture's measurement, not a mapping: the replay parser does not
    /// dispatch the blob's identifier.
    /// </remarks>
    /// <param name="value">Value the blob's trailing field carries.</param>
    /// <returns>The record body.</returns>
    public static byte[] StateBlobBody(ushort value)
    {
        var body = new byte[StateBlobLength];
        body[StateBlobMarkerOffset] = StateBlobMarker;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(
            body.AsSpan(StateBlobValueOffset, sizeof(ushort)),
            value);
        return body;
    }

    /// <summary>The sparse phase's 100-valued state blob.</summary>
    /// <returns>The record body.</returns>
    public static byte[] SparseStateBlob() => StateBlobBody(SparseStateBlobValue);

    /// <summary>The steady phase's zero-valued state blob.</summary>
    /// <returns>The record body.</returns>
    public static byte[] SteadyZeroStateBlob() => StateBlobBody(0);

    /// <summary>
    /// The state vector as the capture records it, with the fields the replay
    /// parser names left to be written by name: those offsets are zero here and
    /// <see cref="MatchStartStateBlock"/> fills them in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parser reads five things out of these eighty-nine bytes: the round
    /// result at <c>state[2]</c>, the last round's result at <c>state[3]</c>, the
    /// two objective holders at <c>state[40]</c> and <c>state[41]</c>, and — in
    /// Capture only — the two capture clocks at <c>state[42]</c> and
    /// <c>state[43]</c>. It also writes a Team Sneaking countdown and lobby slot
    /// into <c>state[87]</c> and <c>state[88]</c> through an opcode-<c>0x02</c>
    /// write, so those two bytes carry the same offsets as the clock below and a
    /// later reader should know both readings.
    /// </para>
    /// <para>
    /// The rest is the recorded vector and no source names it: the two
    /// twenty-four values at <c>state[38]</c>–<c>state[39]</c> and
    /// <c>state[44]</c>–<c>state[48]</c>, the four <c>0xfe</c> at
    /// <c>state[53]</c>–<c>state[56]</c>, the four <c>0x3c</c> at
    /// <c>state[57]</c>–<c>state[60]</c> and the <c>0xff</c> at
    /// <c>state[86]</c>. They stay as recorded rather than invented.
    /// </para>
    /// </remarks>
    private static ReadOnlySpan<byte> MatchStartStateVectorRecorded =>
    [
        // state[0..35], of which state[2] (the result) and state[3] (the last round) are named.
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        // state[36..47]: the pair at 38/39 and the 24 run from 44, around the named holders at 40/41.
        0x00, 0x00, 0x18, 0x18, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x18, 0x18,
        // state[48..59]: the last of that run, then the 0xfe and 0x3c runs.
        0x18, 0x00, 0x00, 0x00, 0x00, 0xfe, 0xfe, 0xfe, 0xfe, 0x3c, 0x3c, 0x3c,
        0x3c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        // state[84..88]: the 0xff at 86 is recorded and unnamed; 87/88 are the clock.
        0x00, 0x00, 0xff, 0x00, 0x00,
    ];

    /// <summary>
    /// The operations state block the match-start run carries: the state opcode
    /// <c>0x00</c>, then the state vector with the fields the replay parser names
    /// written into it — the round result, the two objective holders, the two
    /// capture clocks and the round clock.
    /// </summary>
    /// <param name="stateOpcode">Opcode the operations stream uses for its state block.</param>
    /// <param name="roundClockUnits">Round clock the vector carries, in 125 ms units.</param>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartStateBlock(byte stateOpcode, int roundClockUnits)
    {
        var vector = MatchStartStateVectorRecorded.ToArray();
        vector[StateRoundResultIndex] = StateUnwritten;
        vector[StateObjectiveHolderFirstIndex] = StateObjectiveHolderNone;
        vector[StateObjectiveHolderSecondIndex] = StateObjectiveHolderNone;
        vector[StateCaptureClockFirstIndex] = StateUnwritten;
        vector[StateCaptureClockSecondIndex] = StateUnwritten;
        vector[StateClockLowIndex] = (byte)(roundClockUnits & 0xff);
        vector[StateClockHighIndex] = (byte)((roundClockUnits >> 8) & 0xff);
        return [stateOpcode, .. vector];
    }
}