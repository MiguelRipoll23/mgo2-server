namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The distance a bot prefers to fight at.
/// </summary>
/// <remarks>
/// <para>
/// It is a preference and not a measurement: no capture states how far a player
/// engaged from, and the weapon table's own range columns are unlabelled
/// (<c>docs/WEAPONS.md</c> lists them as <c>w3–w8</c> numeric params). It exists
/// so a role can state the band it wants without a distance claim being written
/// anywhere as game data.
/// </para>
/// </remarks>
public enum BotEngagementRange
{
    /// <summary>Close quarters: the bot wants the fight at touching distance.</summary>
    Close,

    /// <summary>Middle distance: the bot wants a fight it can break off from.</summary>
    Medium,

    /// <summary>Long distance: the bot wants to fight from cover and distance.</summary>
    Long,
}

/// <summary>
/// One bot's own preferences: the role it leans towards, the range it likes and
/// how hard it presses.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are design parameters and not game data.</b> Nothing in the captures
/// or the binaries states an aggression figure or a preferred range for a bot;
/// what they state is which weapons exist and which modes a room plays. The
/// values here are what a bot's decisions read, and every one of them is
/// restated from the role it plays so a designer has one place to tune.
/// </para>
/// <para>
/// A bot's role can be re-assigned for a round while its configuration is kept,
/// which is what lets it adapt to a match without losing what it was created
/// with: <see cref="WithRole"/> restates the role and leaves the rest.
/// </para>
/// </remarks>
/// <param name="PreferredRole">Role the bot was created to play.</param>
/// <param name="PreferredRange">Range band the bot prefers to fight at.</param>
/// <param name="Aggression">How readily the bot advances, 0 to 100.</param>
public sealed record BotConfiguration(
    BotRole PreferredRole,
    BotEngagementRange PreferredRange,
    int Aggression)
{
    /// <summary>The configuration a bot is created with when nothing else is stated.</summary>
    public static BotConfiguration Default { get; } = new(
        BotRole.Assault,
        BotEngagementRange.Medium,
        50);

    /// <summary>The same preferences with another role.</summary>
    /// <param name="role">Role the bot plays now.</param>
    /// <returns>A configuration whose role is restated.</returns>
    public BotConfiguration WithRole(BotRole role) => this with { PreferredRole = role };
}
