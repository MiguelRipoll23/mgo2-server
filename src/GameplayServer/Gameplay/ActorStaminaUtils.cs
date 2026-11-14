namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The stamina rule both a bot's body and a player's slot use: a knock-out blow
/// spends the bar, and it refills at the capture's measured rate.
/// </summary>
/// <remarks>
/// <b>Measured on <c>.pcapngs/actions/cqc.pcapng</c>.</b> Seven runs climb at
/// 18.0 to 18.9 points a second — 16 to 25 in 0.496 s and 72 to 206 over 4.900 s —
/// so the recovery is a rate, not a reset when the stun ends.
/// </remarks>
public static class ActorStaminaUtils
{
    /// <summary>Stamina a living actor opens with, and the bar's ceiling.</summary>
    public const short MaximumStamina = 250;

    /// <summary>Stamina recovered per second, in points.</summary>
    public const int RecoveryPerSecond = 18;

    /// <summary>
    /// What one recovery tick leaves behind.
    /// </summary>
    /// <param name="Stamina">Stamina after the tick.</param>
    /// <param name="Credit">Fractional point carried to the next tick, in point-milliseconds.</param>
    public readonly record struct Recovery(short Stamina, long Credit);

    /// <summary>
    /// Spends a knock-out blow's points, saturating at zero.
    /// </summary>
    /// <param name="stamina">Stamina the victim holds.</param>
    /// <param name="points">Points the blow spends.</param>
    /// <returns>The stamina left.</returns>
    public static short Spend(short stamina, int points) =>
        (short)Math.Clamp(stamina - points, 0, MaximumStamina);

    /// <summary>
    /// Time for a stamina bar to refill at the measured rate.
    /// </summary>
    /// <param name="stamina">Stamina the actor holds.</param>
    /// <returns>Milliseconds to the maximum; zero when already full.</returns>
    public static int RecoveryMilliseconds(short stamina) =>
        stamina >= MaximumStamina
            ? 0
            : (int)Math.Ceiling((MaximumStamina - stamina) * 1000.0 / RecoveryPerSecond);

    /// <summary>
    /// Refills stamina for the time since the last tick, carrying the fraction that
    /// has not yet made a whole point.
    /// </summary>
    /// <param name="stamina">Stamina the actor holds.</param>
    /// <param name="credit">Fraction carried from the last tick.</param>
    /// <param name="elapsedMilliseconds">Time since the last tick.</param>
    /// <returns>The stamina and the new carried fraction.</returns>
    public static Recovery Recover(short stamina, long credit, long elapsedMilliseconds)
    {
        if (stamina >= MaximumStamina)
        {
            return new(stamina, 0);
        }

        credit += elapsedMilliseconds * RecoveryPerSecond;
        var recovered = (int)(credit / 1000);
        if (recovered <= 0)
        {
            return new(stamina, credit);
        }

        credit -= recovered * 1000L;
        return new((short)Math.Min(MaximumStamina, stamina + recovered), credit);
    }
}
