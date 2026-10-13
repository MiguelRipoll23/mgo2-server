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

    /// <summary>
    /// The twenty-four a capture carries in the seven bytes at
    /// <see cref="StateSecondCountdownIndex"/> and
    /// <c>state[45..48]</c>. Two of the seven are constants of the layout; this
    /// one is at an offset a capture moves, so it is written here.
    /// </summary>
    public const byte StateTwentyFour = 0x18;

    /// <summary>
    /// The 0xfe the four bytes at <c>state[53..56]</c> carry, and which the
    /// destabilizer coding also reads as "lost". Nothing on this channel moves
    /// them, so nothing on this channel names them either.
    /// </summary>
    public const byte StateSentinel = 0xfe;

    /// <summary>
    /// The sixty the countdown run at <c>state[57..60]</c> is seeded with.
    /// </summary>
    /// <remarks>
    /// <b>The seed, not the value.</b> A working server writes 0x3c here in five
    /// of six captures and 0x28 in the sixth, and in the one that runs a thirty-
    /// minute round it writes it across <c>state[57..84]</c> rather than four
    /// bytes — so the length is a property of the room and the seed is not.
    /// Whether sixty is seconds, a count or a limit is <b>[U]</b>.
    /// </remarks>
    public const byte StateCountdownSeed = 0x3c;

    /// <summary>First offset of the countdown run this host seeds with <see cref="StateCountdownSeed"/>.</summary>
    public const int StateCountdownSeedIndex = 57;

    /// <summary>Last offset of the four-byte countdown run, matching a capture of a seven-minute room.</summary>
    public const int StateCountdownSeedLastIndex = 60;

    /// <summary>
    /// The twenty-four at <c>state[44]</c>, the offset just before the
    /// twenty-four run. A capture moves it to 1 and seeds the countdown run with
    /// forty in the same room, so the two are read together and neither is
    /// written as a constant of the layout.
    /// </summary>
    public const int StateSecondCountdownIndex = 44;

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
    /// <para>
    /// The capture's measurement, not a mapping: the replay parser does not
    /// dispatch the blob's identifier.
    /// </para>
    /// <para>
    /// <b>The identifier carries more than this one shape, and the class byte
    /// moves with the value.</b> Measured over the six captures, <c>0x0002</c>
    /// appears under <b>nineteen</b> fourth-byte values — every value from
    /// <c>0x01</c> to <c>0x12</c>, and <c>0x15</c> — and the trailing value is
    /// not one number per phase: game1
    /// carries <c>0x0032</c>, <c>0x0045</c>, <c>0x005d</c>, <c>0x0062</c> and
    /// <c>0x0063</c> as well as the sparse 100. So "only the value moves across
    /// the round's blobs", as an earlier reading here had it, is true of *this*
    /// shape and not of the identifier. What the other shapes are is **[U]**;
    /// this host writes the two it can name (the sparse 100 under <c>0x01</c> and
    /// the steady zero under <c>0x15</c>) and no value it cannot.
    /// </para>
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
    /// The runs of <see cref="StateVectorLength"/> bytes that carry one value in
    /// every capture of a working server and in every round of each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Forty-nine of the eighty-nine bytes are constants of the layout, not
    /// facts about a match.</b> Measured over the six captures of a working
    /// dedicated server — <c>mgo2-game1/2/3</c>,
    /// <c>mgo2-game-started-on-briefing</c>,
    /// <c>mgo2-game-started-team-selection-selected-blue-then-game</c> and
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c> — these runs hold one
    /// value in all six, in each capture's opening vector and through every round
    /// of it. They are written as runs rather than kept as one recorded blob,
    /// because a byte that has never moved in six matches is not the recording of
    /// one of them. The measurement is <c>tools/udp_state_vector.py</c> and the
    /// reading is <c>docs/protocol/STATE_VECTOR.md</c>.
    /// </para>
    /// <para>
    /// What the runs do <b>not</b> claim is what any of them means. The sevens
    /// twenty-fours and the four sixties are the values every capture carries,
    /// not a length or a duration read out of them; nothing on this channel names
    /// them, and this host has no better source for them than the captures do.
    /// </para>
    /// </remarks>
    private static (int Offset, int Length, byte Value)[] ConstantRuns =>
    [
        // state[5..10], behind the three result bytes and before the pair the
        // survival replays move at state[11..13].
        (5, 6, 0x00),
        // state[14..37], the long zero run up to the first twenty-four.
        (14, 24, 0x00),
        // state[38..39], twenty-four.
        (38, 2, StateTwentyFour),
        // state[45..48], the other five twenty-fours.
        (45, 4, StateTwentyFour),
        // state[49..52], zero again, between the twenty-fours and the sentinels.
        (49, 4, 0x00),
        // state[53..56], the sentinel run — the same byte the destabilizer coding
        // reads as "lost", and never moved by any capture of a working server.
        (53, 4, StateSentinel),
        // state[61..85], the twenty-five zero bytes behind the sixty seed.
        (61, 25, 0x00),
        // state[86], the last unnamed byte before the clock.
        (86, 1, StateUnwritten),
    ];

    /// <summary>
    /// The operations state block the match-start run carries: the state opcode,
    /// then the state vector with the fields this host knows written into it — the
    /// two results, the two objective holders, the two capture clocks, the
    /// countdown seed and the round clock.
    /// </summary>
    /// <remarks>
    /// The round's result is opened as the sentinel, which is what a working
    /// server writes at a round's open, and the match's is left at the zero a
    /// fresh vector already holds — also what a working server writes. The two
    /// are written together when a round decides the match, by
    /// <see cref="MatchRecordBodiesUtils.StateMatchResultIndex"/>
    /// (<c>docs/protocol/STATE_VECTOR.md</c> §3), and a match that is still
    /// undecided is the zero.
    /// </remarks>
    /// <param name="stateOpcode">Opcode the operations stream uses for its state block.</param>
    /// <param name="roundClockUnits">Round clock the vector carries, in 125 ms units.</param>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartStateBlock(byte stateOpcode, int roundClockUnits)
    {
        var vector = new byte[StateVectorLength];
        foreach (var (offset, length, value) in ConstantRuns)
        {
            Array.Fill(vector, value, offset, length);
        }

        vector[StateRoundResultIndex] = StateUnwritten;
        vector[StateObjectiveHolderFirstIndex] = StateObjectiveHolderNone;
        vector[StateObjectiveHolderSecondIndex] = StateObjectiveHolderNone;
        vector[StateCaptureClockFirstIndex] = StateUnwritten;
        vector[StateCaptureClockSecondIndex] = StateUnwritten;
        vector[StateSecondCountdownIndex] = StateTwentyFour;
        for (var index = StateCountdownSeedIndex; index <= StateCountdownSeedLastIndex; index++)
        {
            vector[index] = StateCountdownSeed;
        }

        vector[StateClockLowIndex] = (byte)(roundClockUnits & 0xff);
        vector[StateClockHighIndex] = (byte)((roundClockUnits >> 8) & 0xff);
        return [stateOpcode, .. vector];
    }
}