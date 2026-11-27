using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round-over record's body: its opcode and the verdict that ended the round.
/// </summary>
/// <remarks>
/// <para>
/// The image builds the record at <c>P2P_PublishOpcode0E</c> (<c>0x754F18</c>),
/// which appends the opcode and one byte and dispatches script event
/// <c>0x160023</c> with the same byte; the queued handler <c>0x75715C</c>
/// dispatches that event on receipt. The byte is a verdict and not a count. The
/// side judges publish <c>6</c> for side 0 and <c>7</c> for side 1
/// (<c>0x762F38</c>, <c>0x764228</c>, and the observed <c>0e 07</c> beside a
/// side-1 state result in <c>mgo2-game1</c>). The other codes are one rule's
/// own: BASE's <c>0x7A58D0</c> publishes <c>1</c> for its clock (`param_2` is
/// the remaining time; method 6 of the same class tests it against 45 000 ms)
/// and <c>2</c> for all bases captured — sending <c>2</c> in TDM shows that
/// message over a TDM round. The side judge publishes <c>4</c> for the clock and
/// <c>5</c> for its ticket branch (flag bit 6 set and <c>+0x20</c> below one).
/// </para>
/// <para>
/// This host ends a round on its clock or on a side's ticket pool, which are the
/// side judge's <c>4</c> and <c>5</c> branches: bit 6 of the judge's flags is its
/// ticket mode, and <c>+0x20</c> the counter that reaching zero ends it. It does
/// not write <c>2</c> — that is BASE's all-bases verdict, and TDM shows that
/// message when it receives it — nor <c>1</c>, BASE's clock code.
/// </para>
/// </remarks>
public static class MatchRoundOverBodiesUtils
{
    /// <summary>A round that closed with no verdict of its own.</summary>
    public const byte VerdictNone = 0;

    /// <summary>
    /// A side's ticket pool reached zero: the side judge's <c>5</c> branch, taken
    /// when its ticket-mode flag is set and its counter at <c>+0x20</c> is below
    /// one (<c>0x762F38</c>, <c>0x764228</c>).
    /// </summary>
    public const byte VerdictTickets = 5;

    /// <summary>The round's clock ran out: the side judge's <c>4</c> branch.</summary>
    public const byte VerdictClock = 4;

    /// <summary>
    /// The round-over body: the opcode and the verdict behind it.
    /// </summary>
    /// <param name="verdict">Verdict code that ended the round.</param>
    /// <returns>The record body.</returns>
    public static byte[] RoundOver(byte verdict) =>
        [(byte)OperationsOpcode.RoundOver, verdict];
}
