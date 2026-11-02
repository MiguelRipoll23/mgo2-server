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
    /// A player's briefing state: the opcode, its slot and the state, so
    /// <c>1f 05 01</c> is slot 5 going ready.
    /// </summary>
    BriefingReady = 0x1f,
}
