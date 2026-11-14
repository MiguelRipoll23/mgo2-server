namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The per-rule coefficients a player's score is computed from, and the client's
/// own clamp.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table is the game's, read off the disc.</b> The client's
/// <c>ComputeScore</c> walks a table of <c>s8</c> coefficients, row = game rule,
/// filled at runtime from the stage script's own directives: every gameplay stage's
/// <c>proc 24</c> switches on <c>COM_svGetRule</c> and writes the rule's row as
/// <c>[ede912] -score &lt;14 ints&gt;</c> plus a shorter <c>[adb9e7] -score</c> row, and
/// the rows are identical across the stages. The client's table is wider than the
/// fourteen values the script writes, but **column 0 is the kill value and column 1
/// the death value** in both, which is what this file uses. It recomputes the whole
/// total from the player's live counters every tick and clamps it to
/// <c>[0, 65535]</c> before storing it, so a score is never banked and a round that
/// takes a player below zero states zero (<c>docs/PROTOCOL.md</c>, "SETTLED
/// 2026-07-27"; <c>docs/proto/inbound/mgo2_cmd_4390_c2s.ksy</c>;
/// <c>docs/analysis/gameplay-data-functions.md</c> §3).
/// </para>
/// <para>
/// <b>Only the two counters this host measures are here.</b> The other
/// thirty-five columns read live counters the gameplay channel does not carry —
/// headshots, stuns dealt and received, wakes, combo, assists, hacks, the
/// objective counters, the team-win bonus, and the off-wire <c>OTHER</c> counter
/// at column 36. A counter the host does not have contributes nothing, which is
/// what the table states for a player with none of it rather than a guessed
/// value standing in for one. The totals are therefore exact for a round in which
/// nothing else scored, and a floor in the three rules where <c>OTHER</c> pays.
/// </para>
/// </remarks>
public static class MatchScoreRulesUtils
{
    /// <summary>
    /// The client's own upper bound on a recomputed score: it clamps the total to
    /// <c>[0, 65535]</c> before the one write that stores it.
    /// </summary>
    public const int ScoreCeiling = 65_535;

    /// <summary>
    /// Kills coefficient per rule, indexed by the rule id the room states. The
    /// values are **column 0** of the stage script's own <c>-score</c> row for that
    /// rule: DM <c>3</c>, TDM <c>3</c>, Rescue <c>7</c>, Capture <c>3</c>, Sneaking
    /// <c>3</c>, Base <c>3</c>, Bomb <c>3</c>, Team Sneaking <c>5</c>, SDM <c>7</c>,
    /// SCAP <c>3</c>; <c>8</c>–<c>11</c>, <c>13</c>–<c>15</c> state none.
    /// </summary>
    /// <remarks>
    /// <b>Capture's 5 and Bomb's 0 were wrong, and Bomb's "no stage script emits
    /// 6" was too.</b> `n001a`'s <c>proc 24</c> has a <c>case 6</c> — Bomb — and
    /// writes <c>3 0 3 0 0 5 -5 7 5 0 1 0 0 2</c>, whose column 0 is <c>3</c>; the
    /// Capture row is <c>3 0 3 0 0 5 -5 7 5 0 1 0 7 2</c>, also <c>3</c>. The other
    /// rows this file already carried agree with the script's column 0, which is
    /// why only those two moved.
    /// </remarks>
    private static readonly int[] KillsCoefficients =
        [3, 3, 7, 3, 3, 3, 3, 5, 0, 0, 0, 0, 7, 0, 0, 0, 3];

    /// <summary>
    /// Deaths coefficient per rule, indexed the same way. A rule whose row states
    /// none does not deduct for a death, and a total that comes out negative is
    /// clamped to zero by <see cref="Score"/> — the measurement behind a scoreless
    /// round wiring <c>0</c> against a raw negative.
    /// </summary>
    private static readonly int[] DeathsCoefficients =
        [-2, -2, 0, 0, -2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Headshot coefficient per rule. DM and TDM pay <c>1</c> on the reference host:
    /// slot 10 of <c>working-server-v2.pcapng</c> stands at 4 kills, 3 headshots and
    /// combo 6 and scores 21, which is <c>12 + 3 + 6</c>. The other rows are the
    /// stage script's own column.
    /// </summary>
    private static readonly int[] HeadshotCoefficients = [1, 1, 3, 3, 2, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Combo coefficient per rule: one point per combo point in DM and TDM, where the
    /// loader clamps column 9 to one, and nothing measured elsewhere.
    /// </summary>
    private static readonly int[] ComboCoefficients = [1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// A player's score in a rule, from the kills and deaths this host has counted
    /// for the round.
    /// </summary>
    /// <param name="rule">Rule id the round is played under.</param>
    /// <param name="kills">Kills counted for the player this round.</param>
    /// <param name="deaths">Deaths counted for the player this round.</param>
    /// <param name="headshots">Headshot kills counted for the player this round.</param>
    /// <param name="combo">The player's combo total: one less than the streak, added on each consecutive kill.</param>
    /// <returns>The score, clamped to <c>[0, 65535]</c> as the client clamps it.</returns>
    public static int Score(int rule, int kills, int deaths, int headshots = 0, int combo = 0)
    {
        if (rule is < 0 || rule >= KillsCoefficients.Length)
        {
            return 0;
        }

        return Math.Clamp(
            kills * KillsCoefficients[rule]
            + deaths * DeathsCoefficients[rule]
            + headshots * HeadshotCoefficients[rule]
            + combo * ComboCoefficients[rule],
            0,
            ScoreCeiling);
    }
}
