namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The arithmetic a utility score is made of: one bounded number per action, and
/// the role's weight applied to it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scores are bounded to zero and one so two of them can be compared.</b> An
/// objective states how valuable an action is on its own terms and a role states
/// how much it favours the action; multiplying the two keeps the ordering the
/// objective meant while letting a role lean towards a way of playing. A score
/// that ran unbounded would let whichever system produced the largest number win
/// regardless of how valuable the action was.
/// </para>
/// <para>
/// Nothing here reads the world: the arithmetic is separated so a score can be
/// explained in a test or a log without a room around it.
/// </para>
/// </remarks>
public static class BotUtilityUtils
{
    /// <summary>Clamps a score into the range decisions compare within.</summary>
    /// <param name="value">Score to clamp.</param>
    /// <returns>The score, clamped to zero and one.</returns>
    public static double Clamp01(double value) => value switch
    {
        < 0 => 0,
        > 1 => 1,
        _ => value,
    };

    /// <summary>
    /// An objective's score with a role's weight applied, still bounded.
    /// </summary>
    /// <param name="utility">How valuable the action is to the mode.</param>
    /// <param name="roleWeight">How much the role favours the action.</param>
    /// <returns>The combined score.</returns>
    public static double Combine(double utility, double roleWeight) =>
        Clamp01(Clamp01(utility) * Clamp01(roleWeight));

    /// <summary>
    /// A role weight as a multiplier: one is neutral, above favours, below
    /// discourages.
    /// </summary>
    /// <param name="weight">Weight a role states.</param>
    /// <returns>The weight, floored at zero.</returns>
    public static double Weight(double weight) => Math.Max(0, weight);
}
