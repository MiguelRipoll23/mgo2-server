namespace Mgo2Server.GameplayServer.Gameplay;

/// <summary>
/// The health rule both a bot's body and a player's slot use: a blow takes points
/// off the bar, a whole-bar blow empties it, and zero is fatal.
/// </summary>
public static class ActorHealthUtils
{
    /// <summary>Health a living actor opens with, and the bar's ceiling.</summary>
    public const short MaximumHealth = 250;

    /// <summary>What one blow leaves behind.</summary>
    /// <param name="Health">Health after the blow.</param>
    /// <param name="Fatal">Whether the blow emptied the bar.</param>
    public readonly record struct Change(short Health, bool Fatal);

    /// <summary>
    /// Takes a blow off a health bar.
    /// </summary>
    /// <param name="health">Health the victim holds.</param>
    /// <param name="damage">Points the blow takes.</param>
    /// <param name="takesWholeBar">Whether the blow ends the victim whatever it held.</param>
    /// <returns>The health left and whether it is fatal.</returns>
    public static Change Apply(short health, int damage, bool takesWholeBar)
    {
        var left = takesWholeBar ? (short)0 : (short)Math.Max(0, health - damage);
        return new(left, IsFatal(left));
    }

    /// <summary>Whether a health bar is empty.</summary>
    /// <param name="health">Health to test.</param>
    /// <returns><c>true</c> at zero or below.</returns>
    public static bool IsFatal(short health) => ActorKillUtils.IsFatal(health);
}
