namespace Mgo2Server.Shared.Types;

/// <summary>
/// The operation an operations-stream body carries, in the body's first byte.
/// </summary>
/// <remarks>
/// <para>
/// <b>The stream is told apart by its opcode, not by its identifier.</b> Every operations
/// record travels under the same identifier pair (<c>0x1a54</c> and its <c>0x800</c>
/// sibling, <c>0x1254</c>), so the identifier says nothing about the body: the first byte
/// does. The mapping is the replay parser's, measured over its replays
/// (<c>docs/analysis/replay-console/rpdt.js</c>, "operation opcodes"), and the bodies are
/// mapped in <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>.
/// </para>
/// <para>
/// <b>Which of these this host writes, and which it only reads.</b> A dedicated host
/// writes <see cref="StateBlock"/>, <see cref="RoundClock"/>, <see cref="StateWrite"/>,
/// <see cref="BriefingCountdown"/> and <see cref="UnnamedShort"/>; a client writes
/// <see cref="KillFeed"/> and <see cref="BriefingReady"/>.
/// <see cref="ObjectiveFeed"/>, <see cref="SlotRecord"/>, <see cref="RoundOver"/>,
/// <see cref="RaceGoal"/> and <see cref="Rating"/> are the parser's readings of records
/// this host has no measured body for, so they are named here and written nowhere — the
/// capture's own rounds carry them.
/// </para>
/// <para>
/// <b>Seven more come from the image and the captures rather than from the parser.</b>
/// <see cref="HostMark"/> and <see cref="OwnSlotRecord"/> have their bodies read out of the
/// builders that write them (<c>0x7548F0</c>, <c>0x754A90</c>); <see cref="HostTriple"/>
/// and <see cref="HostClampedPair"/> are host records the corpus carries once each, and
/// <see cref="TwoBytePair"/> an opcode the image builds and no capture carries;
/// <see cref="JoinerRecord"/> and <see cref="JoinerTail"/> are the joiner's own, identical
/// in all eleven captures that carry them. The reading is
/// <c>docs/analysis/supervisor-opcode-builders.md</c>.
/// </para>
/// </remarks>
public enum OperationsOpcode : byte
{
    /// <summary>
    /// The round's state block: eighty-nine state bytes behind the opcode, two of which
    /// are the round's and the match's result.
    /// </summary>
    StateBlock = 0x00,

    /// <summary>The round's clock, a little-endian count of 125 ms units.</summary>
    RoundClock = 0x01,

    /// <summary>
    /// A write into the state block: the offset, the count and the values, so
    /// <c>02 02 01 02</c> is "state[2] = 2".
    /// </summary>
    StateWrite = 0x02,


    /// <summary>
    /// The objective feed: a two-byte body naming the slot that took or lost an
    /// objective. The parser reads it; this host does not write it.
    /// </summary>
    ObjectiveFeed = 0x04,

    /// <summary>
    /// A body naming one player's slot whose meaning is **[U]** — the parser keeps it
    /// so it can be tested and names nothing.
    /// </summary>
    SlotRecord = 0x05,

    /// <summary>
    /// A host record of four bytes, <c>07 05 0b 00</c>: three one-byte arguments behind the
    /// opcode, built at <c>0x7557CC</c> in the PS3 image and at <c>0x101efc20</c> in the native port, whose builder takes three byte arguments and asserts that the caller is the host.
    /// </summary>
    /// <remarks>
    /// <b>The shape is the image's and the fields are [U].</b> The builder appends the opcode
    /// and then three single bytes its caller passes, and the sibling arm at <c>0x756284</c>
    /// builds the same body. The one sample is <c>mgo2-started-on-game-round1</c>
    /// (t+1 353.344), and it follows the host's writes of <c>state[8] = 0x0f</c>,
    /// <c>state[13] = 1</c> and <c>state[15] = 5</c> — too little to name the three values,
    /// which are small enough to be teams, phases or counts. This host writes none
    /// (<c>docs/analysis/supervisor-opcode-builders.md</c> §2).
    /// </remarks>
    HostTriple = 0x07,

    /// <summary>
    /// A host record of three bytes, <c>08 04 02</c>: two one-byte arguments, each clamped
    /// into <c>0…255</c> on the way in, built at <c>0x7556FC</c>, and at <c>0x101efd00</c> in the native port, which packs the same pair into one byte as <c>(a &lt;&lt; 4) | (b &amp; 0x0f)</c> — so both values are four-bit quantities, while the captured revision writes them as two plain bytes.
    /// </summary>
    /// <remarks>
    /// The sample is <c>mgo2-started-on-game-round1</c> (t+1 368.348) — the instant its
    /// briefing countdown reaches zero, 0.4 s before the round's boxes are set out — and one
    /// sample leaves the two values **[U]**. The clamping is the builder's own
    /// (<c>sradi</c>/<c>and</c> against the sign, then <c>andc</c> against <c>0xff</c>), so a
    /// caller hands it signed numbers.
    /// </remarks>
    HostClampedPair = 0x08,

    /// <summary>The kill feed: the killer's slot and the victim's, three bytes in all.</summary>
    KillFeed = 0x09,

    /// <summary>
    /// The briefing's countdown: the seconds left, the room's ready set and two zeros.
    /// A briefing writes this where a round writes <see cref="RoundClock"/>.
    /// </summary>
    BriefingCountdown = 0x0b,

    /// <summary>The end of a round, with the reason the parser reads off it.</summary>
    RoundOver = 0x0e,

    /// <summary>
    /// The two-byte body the match-start run carries. It is the one opcode the parser's
    /// table has no entry for, so its byte stays the recorded one.
    /// </summary>
    UnnamedShort = 0x0f,

    /// <summary>
    /// A two-argument record, <c>11 a b</c>, built at <c>0x754570</c>; no capture carries one.
    /// </summary>
    /// <remarks>
    /// <b>The opcode exists in the image and the corpus's <c>11 …</c> bodies are not it.</b>
    /// The fifteen-byte bodies a low-byte filter reports on the operations identifier travel
    /// on <c>0x0854</c>, another player's **damage** slot — five below its position record,
    /// the role the replay grammar gives <c>VID_DAMAGE</c> — and are a damage payload
    /// (<c>docs/analysis/supervisor-opcode-builders.md</c> §3).
    /// </remarks>
    TwoBytePair = 0x11,

    /// <summary>
    /// A Race goal: the team's flags, the goal it now has and the spawn point it uses.
    /// The parser maps it; this host does not write it.
    /// </summary>
    RaceGoal = 0x12,

    /// <summary>
    /// One row of the per-player rating: an array of signed sixteens behind the count.
    /// The parser maps it; this host does not write it.
    /// </summary>
    Rating = 0x13,

    /// <summary>
    /// A two-byte record naming the writer's own slot: <c>1b 11</c> is slot <c>0x11</c>.
    /// </summary>
    /// <remarks>
    /// <b>The body is settled and its purpose is not.</b> The builder is <c>0x754A90</c>: its
    /// caller's argument chooses the opcode — <c>1</c> for <c>0x1b</c>, anything else for the
    /// <c>0x1c</c> sibling — and the byte behind it is <c>0x26D588()</c>, the writer's own
    /// slot, which is the pair the capture shows. What the record announces stays **[U]**;
    /// <c>mgo2-started-on-game-round1</c>'s client carries it once
    /// (<c>docs/analysis/supervisor-opcode-builders.md</c> §1).
    /// </remarks>
    OwnSlotRecord = 0x1b,

    /// <summary>
    /// A player's briefing state: the opcode, its slot and the state, so
    /// <c>1f 05 01</c> is slot 5 going ready.
    /// </summary>
    BriefingReady = 0x1f,

    /// <summary>
    /// The joiner's own block: five seven-byte records <c>1d slot index a b 00 00</c> whose
    /// index runs <c>0</c> to <c>4</c>, sent when the player enters its round's stream.
    /// </summary>
    /// <remarks>
    /// <b>Its bodies are the same in every capture, whatever the player.</b> Index <c>0</c>
    /// carries <c>01 03</c>, index <c>1</c> carries <c>02 01</c> and the three behind are
    /// zero, over eleven captures, four players and four stages — so the block states the
    /// client's own record set and not anything about the match. What each index means is
    /// **[U]**: the client's state table stops at <c>0x19</c> and the parser has no entry
    /// (<c>docs/protocol/STATE_VECTOR.md</c> §1, <c>udp-record-dispatch.md</c> §3.6).
    /// </remarks>
    JoinerRecord = 0x1d,

    /// <summary>
    /// The joiner's own twenty-two-byte record, sent with <see cref="JoinerRecord"/>:
    /// its slot and eighteen bytes that do not change across the captures.
    /// </summary>
    JoinerTail = 0x22,

    /// <summary>
    /// The host's own three-byte record, <c>26 02 06</c> in every round of the corpus.
    /// </summary>
    /// <remarks>
    /// <b>It names a subject's slot and a signed byte, about a player who is not the
    /// writer.</b> The builder is <c>0x7548F0</c> — the opcode and two single-byte arguments —
    /// and both of its callers compare the record's slot against <c>0x26D588()</c>, the
    /// writer's own, and skip the send when the two are equal. So the capture's
    /// <c>26 02 06</c> is slot 2 and value 6, sent to a client about slot 2; what the two
    /// values say is **[U]** (<c>docs/analysis/supervisor-opcode-builders.md</c> §1).
    /// </remarks>
    HostMark = 0x26,
}
