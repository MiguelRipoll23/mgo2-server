namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// How much of a character's health and stamina is left, in the units the
/// gameplay channel counts them in.
/// </summary>
/// <remarks>
/// <para>
/// The unit is one byte on the wire and <see cref="MaximumValue"/> is its
/// ceiling, not a percentage: a character at full health reads <c>250</c>, not
/// <c>100</c>. Health falls in steps — the capture has it going
/// <c>250 → 194 → 160 → 127 → 70 → 14 → 0</c> — so the scale is whatever the
/// game decided it is rather than a round number a designer would pick.
/// </para>
/// <para>
/// <b>Zero health is death.</b> The capture shows health sitting at
/// <see cref="MaximumValue"/> between fights, falling only when the character
/// is hit, reaching zero, and returning to <see cref="MaximumValue"/> after —
/// three deaths and three restores over one round. Nothing else in the capture
/// reads as a life total, and no value between the two ends is special.
/// </para>
/// <para>
/// <b>Stamina is the second byte, and that is an inference [I], not a
/// measurement.</b> What is measured is that the second byte read
/// <see cref="MaximumValue"/> in every one of the 3 269 records that carried
/// both, while the first byte is the one that fell and rose. Naming the first
/// byte health and the second stamina follows from the first of those two facts
/// and from nothing else: no capture record shows stamina moving, so its
/// ceiling of <see cref="MaximumValue"/> is the ceiling it was never seen to
/// exceed, not one it was seen to reach. Nothing here treats a full stamina as
/// anything but the default.
/// </para>
/// </remarks>
/// <param name="Health">Health remaining; zero is death.</param>
/// <param name="Stamina">Stamina remaining.</param>
public sealed record PlayerVitals(int Health, int Stamina)
{
    /// <summary>
    /// The most of either vital the wire can carry, and what an untouched
    /// character reads.
    /// </summary>
    public const int MaximumValue = 250;

    /// <summary>Whether the character is out of health.</summary>
    public bool IsDead => Health <= 0;

    /// <summary>Health of a character that has taken no damage.</summary>
    public static PlayerVitals Full { get; } = new(MaximumValue, MaximumValue);

    /// <summary>Health and stamina clamped into the range the wire carries.</summary>
    /// <returns>The vitals with both values inside <c>0</c> and <see cref="MaximumValue"/>.</returns>
    public PlayerVitals Clamped() => new(Math.Clamp(Health, 0, MaximumValue), Math.Clamp(Stamina, 0, MaximumValue));
}