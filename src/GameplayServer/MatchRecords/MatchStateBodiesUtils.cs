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

    /// <summary>
    /// Length of the presence map a state blob opens with: one bit per field of
    /// a slot state, <c>ceil(164 / 8)</c> bytes.
    /// </summary>
    public const int StateBlobMapLength = 21;

    /// <summary>
    /// The one field of a slot state a state blob carries. A slot state has 164
    /// fields and exactly this one is flagged <c>0x0040</c>; it is a single byte
    /// at state offset 287.
    /// </summary>
    /// <remarks>
    /// The map's bit for this index is the <c>0x40</c> every recorded blob carries
    /// at body offset 18 — <c>150 >> 3 = 18</c>, <c>1 << (150 &amp; 7) = 0x40</c> —
    /// and its byte is the one that moves, at body offset 21.
    /// </remarks>
    public const int StateBlobFieldIndex = 150;

    /// <summary>Offset of a state blob's only varying field: one byte, behind the map.</summary>
    public const int StateBlobValueOffset = StateBlobMapLength;

    /// <summary>Value the sparse phase's blobs carry, and the host's own slot's.</summary>
    public const byte MeasuredStateBlobValue = 100;

    /// <summary>
    /// Value a peer's slot reads before the host has measured it: the first value
    /// every peer is named with in all five captures that carry a briefing, and the
    /// value the join-time burst carries.
    /// </summary>
    /// <remarks>
    /// <b>It is the ladder's first rung and not a default.</b> Every peer of every
    /// surveyed capture appears at 50 before anything else — `mgo2-game1`'s four
    /// measured joiners (`05`, `07`, `0c`, `10`), `mgo2-game2`'s `02`,
    /// `mgo2-game-started-on-briefing`'s `02`, `05` and `07`, and the ten slots of
    /// `mgo2-game-joined-when-red-team-then-briefing`, nine of which never leave it.
    /// What a peer is measured at afterwards is the host's own figure and is not on
    /// this wire: `game1`'s `10` climbs 50, 91, 90, 94, 98, 99 and its `07` 50, 69,
    /// 93, 99, 98, while `07`'s `0c` stays between 73 and 85 for the whole round.
    /// </remarks>
    public const byte UnmeasuredStateBlobValue = 50;

    /// <summary>
    /// Value a slot's record falls to once the player it named has gone: 0, measured
    /// twice and never anything else.
    /// </summary>
    /// <remarks>
    /// <b>The slot keeps its record and empties it.</b> `mgo2-game1`'s slot `15`
    /// reads 50 at t+6.7 and 0 at every tick from t+78.3 to the end of the capture;
    /// `mgo2-started-briefing-then-solo-game`'s slot `04` reads 50 at t+6.0 and 0
    /// from t+92.9, and its slot `08` walks 50, 38, 0. No capture drops a slot's
    /// record when its player leaves, and none writes a value above 0 or below 100.
    /// </remarks>
    public const byte EmptyStateBlobValue = 0;

    /// <summary>
    /// Value a slot's record carries for a peer whose round trip this host has
    /// measured: the ladder's top less that round trip, or the unmeasured value while
    /// nothing of ours has been answered.
    /// </summary>
    /// <remarks>
    /// <b>The ladder reads downwards from 100, and that is the only reading that fits
    /// every observed value.</b> The host's own player reads 100 in every capture that
    /// holds it; a peer starts at 50 and then sits at its own figure — 91 to 99 for a
    /// fast one (`mgo2-game1` slot `10`), 73 to 85 for a middling one (slot `0c`), 10
    /// for a slow one (`mgo2-game2` slot `02`) — and a slot whose player has gone
    /// reads 0, which is the one value a round trip above 100 ms produces. Under
    /// "100 less the round trip in milliseconds" those are 0 ms for the host's own
    /// stream, 50 ms for the default, and 1 to 9, 15 to 27 and 90 ms for the measured
    /// peers, all of them plausible for players in one room and none of them leaving
    /// the byte's range. **[I]**: nothing on either wire names the unit, and the value
    /// this host writes is its own measurement — see
    /// `docs/analysis/join-stall-after-mode2.md` §39.
    /// </remarks>
    /// <param name="roundTripMilliseconds">Measured round trip, or -1 while none is.</param>
    /// <returns>The value the slot's record carries.</returns>
    public static byte StateBlobValueFor(int roundTripMilliseconds) =>
        roundTripMilliseconds < 0
            ? UnmeasuredStateBlobValue
            : (byte)Math.Clamp(
                MeasuredStateBlobValue - roundTripMilliseconds,
                EmptyStateBlobValue,
                (int)MeasuredStateBlobValue);

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
    /// Builds a state blob: a <see cref="StateBlobMapLength"/>-byte presence map
    /// with <see cref="StateBlobFieldIndex"/>'s bit set, then that field's byte.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A state blob is a state transfer, and it parses exactly.</b> The record
    /// is <c>{ 0x0002, len, class, body }</c> with <c>len</c> counting the class
    /// byte, and a reader of <c>len</c> bytes — class first — is the whole record:
    /// one byte of slot-state index, <c>ceil(164 / 8) = 21</c> bytes of presence
    /// map, and the bytes of the fields whose bits are set. Every one of the 1429
    /// copies in the thirteen captures has <c>len = 23</c> and a map holding one
    /// bit, at index 150, whose field is one byte: <c>1 + 21 + 1 = 23</c> exactly.
    /// The class byte is that state index, so it is the player's slot byte
    /// (<c>key + 1</c>, the byte the loadout records carry) and names slot states
    /// <c>0x01</c>–<c>0x18</c>, which are the session's twenty-four slot states.
    /// </para>
    /// <para>
    /// <b>The parse is the client's own, read out of `MGO2.ELF`.</b> The body
    /// dispatcher <c>0x27E8B0</c> reads one byte as a state index into the 26-entry
    /// table <c>0x0187E990</c>, reads <c>ceil(count / 8)</c> bytes of map, and walks
    /// the map's bits — <c>srawi 3</c> for the byte, <c>clrlwi 0x1d</c> for the bit,
    /// <c>sraw r0, r0, r11</c> then <c>clrlwi 0x1f</c> — so a bit is the byte's low
    /// bit first and <c>0x40</c> in byte 18 is index <c>18 × 8 + 6 = 150</c>. Each
    /// set bit then runs the descriptor's handler object (<c>0x0122A5C8</c>, vtable
    /// <c>0x011F17F0</c>, slot 0 <c>0x27EC48</c>), which moves that field between the
    /// stream and the slot's 1248-byte state block. Field 150's entry in the 164-field
    /// table is <c>state+287</c>, one byte, and the only field of the 164 flagged
    /// <c>0x0040</c> — the flag that publishes a field whether or not it changed,
    /// which is why the record repeats on a tick instead of only on a change.
    /// </para>
    /// <para>
    /// <b>The value is the host's measurement of that peer, and its ladder is
    /// measured.</b> Surveyed over the five captures that hold a briefing: every
    /// peer is named with **50** first, the host's own slot reads **100** in every
    /// capture that holds it (`game1` slot `01`, `game2` slot `03`, the solo
    /// capture's `01`), a player who has been measured sits somewhere between
    /// (`game1`: 91, 90, 94, 98, 99 on slot `10`; 73 to 85 on `0c`; 10 on `game2`'s
    /// `02` and on `started-on-briefing`'s), and a slot whose player has gone falls
    /// to **0** (<see cref="EmptyStateBlobValue"/>) rather than disappearing. What
    /// the number counts is **[I]**: a round trip is the reading the numbers fit —
    /// 100 for the host's own player, 50 for a peer at 50 ms, 10 for one at 90 ms —
    /// and the same host holds a per-player ping over TCP (<c>0x4398</c>,
    /// <c>GamePlayer.Ping</c>), but
    /// nothing on either wire names the unit.
    /// </para>
    /// <para>
    /// <b>The identifier carries more than this one shape.</b> Measured over the six
    /// captures, <c>0x0002</c> appears under <b>nineteen</b> fourth-byte values —
    /// every value from <c>0x01</c> to <c>0x12</c>, and <c>0x15</c> — and the trailing
    /// value is not one number per phase: <c>game1</c> carries <c>0x0045</c>,
    /// <c>0x005d</c>, <c>0x0062</c> and <c>0x0063</c> as well as the sparse 100. What
    /// the other shapes are is **[U]**; this host writes the two it can name (the
    /// sparse 100 under <c>0x01</c> and the steady zero under <c>0x15</c>) and no
    /// value it cannot. The briefing's tick writes it once per player rather than
    /// once per room, and writes 50 for a peer it has not measured
    /// (<see cref="UnmeasuredStateBlobValue"/>, `BriefingRoomPublishService`, and
    /// `docs/analysis/join-stall-after-mode2.md` §28).
    /// </para>
    /// </remarks>
    /// <param name="value">Byte the carried field holds.</param>
    /// <returns>The record body.</returns>
    public static byte[] StateBlobBody(byte value)
    {
        var body = new byte[StateBlobLength];
        body[StateBlobFieldIndex / 8] = (byte)(1 << (StateBlobFieldIndex % 8));
        body[StateBlobValueOffset] = value;
        return body;
    }

    /// <summary>The sparse phase's 100-valued state blob.</summary>
    /// <returns>The record body.</returns>
    public static byte[] SparseStateBlob() => StateBlobBody(MeasuredStateBlobValue);

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