namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The per-rule coefficients a player's score is computed from, and the client's
/// own clamp.
/// </summary>
/// <remarks>
/// <para>
/// <b>The table is the game's, read off the disc.</b> The client's
/// <c>ComputeScore</c> walks a 37-column × 11-row table of <c>s8</c> coefficients,
/// row = game rule, filled at runtime from the <c>-rule N -score &lt;37 ints&gt;</c>
/// directives in the stage script; the rows were read out of
/// <c>stage/n002a|n003a|n004a/scenerio.gcx</c> and are byte-identical across
/// stages. It recomputes the whole total from the player's live counters every
/// tick and clamps it to <c>[0, 65535]</c> before storing it, so a score is never
/// banked and a round that takes a player below zero states zero
/// (<c>docs/PROTOCOL.md</c>, "SETTLED 2026-07-27"; <c>docs/proto/inbound/mgo2_cmd_4390_c2s.ksy</c>).
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
    /// Kills coefficient per rule, indexed by the rule id the room states: DM
    /// <c>0</c>, TDM <c>1</c>, Rescue <c>2</c>, Capture <c>3</c>, Sneaking
    /// <c>4</c>, Base <c>5</c>, Team Sneaking <c>7</c>. Rule <c>6</c> is emitted by
    /// no stage script and scores nothing, and neither do <c>8</c>, <c>9</c> and
    /// <c>10</c>.
    /// </summary>
    private static readonly int[] KillsCoefficients = [3, 3, 7, 5, 3, 3, 0, 5, 0, 0, 0];

    /// <summary>
    /// Deaths coefficient per rule, indexed the same way. A rule whose row states
    /// none does not deduct for a death, and a total that comes out negative is
    /// clamped to zero by <see cref="Score"/> — the measurement behind a scoreless
    /// round wiring <c>0</c> against a raw negative.
    /// </summary>
    private static readonly int[] DeathsCoefficients = [-2, -2, 0, 0, -2, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// A player's score in a rule, from the kills and deaths this host has counted
    /// for the round.
    /// </summary>
    /// <param name="rule">Rule id the round is played under.</param>
    /// <param name="kills">Kills counted for the player this round.</param>
    /// <param name="deaths">Deaths counted for the player this round.</param>
    /// <returns>The score, clamped to <c>[0, 65535]</c> as the client clamps it.</returns>
    public static int Score(int rule, int kills, int deaths)
    {
        if (rule is < 0 || rule >= KillsCoefficients.Length)
        {
            return 0;
        }

        return Math.Clamp(
            kills * KillsCoefficients[rule] + deaths * DeathsCoefficients[rule],
            0,
            ScoreCeiling);
    }
}
