using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round-over record's body and the rule-gated choice of its verdict.
/// </summary>
/// <remarks>
/// <para>
/// The image builds the record at <c>P2P_PublishOpcode0E</c> (<c>0x754F18</c>),
/// which appends the opcode and one byte and dispatches script event
/// <c>0x160023</c> with the same byte; the queued handler <c>0x75715C</c>
/// dispatches that event on receipt. The byte is a verdict and not a count. Its
/// codes are rule-specific: BASE's judge (<c>0x7A58D0</c>) writes <c>1</c> for
/// its clock and <c>2</c> for all bases captured, while the side judge
/// (<c>0x762F38</c>, <c>0x764228</c>) writes <c>4</c> for its clock, <c>5</c> for
/// its ticket pool and <c>6</c>/<c>7</c> for one of the two sides. Sending BASE's
/// <c>2</c> into TDM shows "all the bases are captured" over a TDM round.
/// </para>
/// <para>
/// This host closes a round on its clock or on a side's ticket pool, so it maps
/// those reasons through the played round's rule: BASE clock is <c>1</c>, TDM
/// clock is <c>4</c> and TDM ticket exhaustion is <c>5</c>. A reason or rule the
/// image does not give this host a code for is written as
/// <see cref="RoundOverVerdict.None"/> rather than guessed. The mapping is kept
/// as the protocol reading; the dedicated host does **not** put it on the wire.
/// The reference ticket-limit and swap-spawn captures carry no <c>0x0e</c>, and
/// the record's script event is the client host's own (see
/// <c>MatchResultService.WriteResultAsync</c>).
/// </para>
/// </remarks>
public static class MatchRoundOverBodiesUtils
{
    /// <summary>
    /// The verdict a rule writes for a round-over reason.
    /// </summary>
    /// <remarks>
    /// Only the two rules whose close conditions this host maps are stated: BASE,
    /// whose judge has the clock and all-bases codes, and Team Deathmatch, whose
    /// side judge has the clock, ticket and side codes. Every other rule keeps
    /// <see cref="RoundOverVerdict.None"/>, because sending another rule's code
    /// shows its message on the client.
    /// </remarks>
    /// <param name="rule">Rule id the closed round played under.</param>
    /// <param name="reason">Why this host closed the round.</param>
    /// <param name="roundResult">Result code the round was closed with, for a side win.</param>
    /// <returns>The verdict byte to carry.</returns>
    public static RoundOverVerdict VerdictFor(
        int rule,
        RoundOverReason reason,
        byte? roundResult = null) =>
        reason switch
        {
            RoundOverReason.None => RoundOverVerdict.None,
            RoundOverReason.Clock when rule == MatchRuleUtils.Base =>
                RoundOverVerdict.BaseClock,
            RoundOverReason.AllBasesCaptured when rule == MatchRuleUtils.Base =>
                RoundOverVerdict.BaseAllBasesCaptured,
            RoundOverReason.Clock when rule == MatchRuleUtils.TeamDeathmatch =>
                RoundOverVerdict.Clock,
            RoundOverReason.Tickets when rule == MatchRuleUtils.TeamDeathmatch =>
                RoundOverVerdict.Tickets,
            RoundOverReason.SideWin when rule == MatchRuleUtils.TeamDeathmatch =>
                SideVerdict(roundResult),
            _ => RoundOverVerdict.None,
        };

    /// <summary>
    /// The side judge's verdict for a side win: <c>6</c> for side 0 and <c>7</c> for
    /// every other side, which is its own two-way split
    /// (<c>*(char *)(state + 0x1D) &lt; 1 ? 6 : 7</c>).
    /// </summary>
    /// <param name="roundResult">Result code that names the winning side.</param>
    /// <returns>The side verdict, or <see cref="RoundOverVerdict.None"/>.</returns>
    private static RoundOverVerdict SideVerdict(byte? roundResult) =>
        roundResult is { } code && MatchRecordBodiesUtils.TeamOfResult(code) is { } team
            // The verdict's side 0 is the client's first side, red; the room's
            // rows name that side 1, so the room's 0 is the verdict's side one.
            ? team == 1 ? RoundOverVerdict.SideZero : RoundOverVerdict.SideOne
            : RoundOverVerdict.None;

    /// <summary>
    /// The round-over body: the opcode and the verdict behind it.
    /// </summary>
    /// <param name="verdict">Verdict code that ended the round.</param>
    /// <returns>The record body.</returns>
    public static byte[] RoundOver(RoundOverVerdict verdict) =>
        [(byte)OperationsOpcode.RoundOver, (byte)verdict];
}
