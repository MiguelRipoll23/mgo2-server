namespace Mgo2Server.GameplayServer.Match;

/// <summary>
/// The rules a room's rotation can carry, and the names each is written under.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ids are the replay decoder's and the names are its own.</b>
/// <c>docs/analysis/replay-console/rpdt.js</c> holds the <c>MODE_NAMES</c> table
/// <c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2 measures a room's rotation
/// against — <c>0</c> DM, <c>1</c> TDM, <c>2</c> RES, <c>3</c> CAP, <c>4</c> SNE,
/// <c>5</c> BASE, <c>6</c> BOMB, <c>7</c> TSNE, <c>12</c> SDM, <c>13</c>
/// INTERVAL, <c>15</c> SCAP, <c>16</c> RACE — and the table below is that one
/// ported rather than a second spelling of the same rules. A configuration names
/// a rule the way the decoder and the documents do, and <see cref="Parse"/> is
/// where the name becomes the byte a rotation carries.
/// </para>
/// <para>
/// A name the table does not hold is refused rather than mapped onto a nearby id.
/// A room that runs a rule no client was told about is a room the client is not
/// in, so there is no sensible guess to make: <see cref="Parse"/> throws, and the
/// caller that has a name to resolve — a startup, with the whole process to stop
/// — decides what an unresolvable one costs.
/// </para>
/// </remarks>
public static class MatchRuleUtils
{
    /// <summary>Deathmatch, the rule a rotation writes as <c>0</c>.</summary>
    public const int Deathmatch = 0;

    /// <summary>Team Deathmatch, the rule a rotation writes as <c>1</c>.</summary>
    public const int TeamDeathmatch = 1;

    /// <summary>Rescue, the rule a rotation writes as <c>2</c>.</summary>
    public const int Rescue = 2;

    /// <summary>Capture, the rule a rotation writes as <c>3</c>.</summary>
    public const int Capture = 3;

    /// <summary>Sneaking, the rule a rotation writes as <c>4</c>.</summary>
    public const int Sneaking = 4;

    /// <summary>Base, the rule a rotation writes as <c>5</c>.</summary>
    public const int Base = 5;

    /// <summary>Bomb, the rule a rotation writes as <c>6</c>.</summary>
    public const int Bomb = 6;

    /// <summary>Team Sneaking, the rule a rotation writes as <c>7</c>.</summary>
    public const int TeamSneaking = 7;

    /// <summary>
    /// Stealth Deathmatch, the rule a rotation writes as <c>12</c>. It is the
    /// decoder's <c>SDM</c>, and the same rule the lobby's host environment calls
    /// Stealth Deathmatch.
    /// </summary>
    public const int StealthDeathmatch = 12;

    /// <summary>Interval, the rule a rotation writes as <c>13</c>.</summary>
    public const int Interval = 13;

    /// <summary>Sneaking Capture, the rule a rotation writes as <c>15</c>.</summary>
    public const int SneakingCapture = 15;

    /// <summary>Race, the rule a rotation writes as <c>16</c>.</summary>
    public const int Race = 16;

    /// <summary>
    /// Every rule a rotation can carry, in the decoder's own order, with the name
    /// it is written under.
    /// </summary>
    private static readonly (int Rule, string Name)[] Rules =
    [
        (Deathmatch, "DM"),
        (TeamDeathmatch, "TDM"),
        (Rescue, "RES"),
        (Capture, "CAP"),
        (Sneaking, "SNE"),
        (Base, "BASE"),
        (Bomb, "BOMB"),
        (TeamSneaking, "TSNE"),
        (StealthDeathmatch, "SDM"),
        (Interval, "INTERVAL"),
        (SneakingCapture, "SCAP"),
        (Race, "RACE"),
    ];

    /// <summary>The name a rule is written under, or <c>null</c> when the id is no rule.</summary>
    /// <param name="rule">Rule id from a rotation triple.</param>
    /// <returns>The rule's name, or <c>null</c>.</returns>
    public static string? Name(int rule)
    {
        foreach (var entry in Rules)
        {
            if (entry.Rule == rule)
            {
                return entry.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// The rule id a name states.
    /// </summary>
    /// <remarks>
    /// The name is matched without case, because the name is what a person writes
    /// in a configuration rather than a byte on a wire, and nothing about uppercase
    /// distinguishes one rule from another.
    /// </remarks>
    /// <param name="name">Rule name, as the decoder spells it.</param>
    /// <returns>The rule id.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name is no rule.</exception>
    public static int Parse(string name) =>
        TryParse(name, out var rule)
            ? rule
            : throw new InvalidOperationException(
                $"'{name}' is not a rule a room's rotation can carry; it is one of {Names}.");

    /// <summary>
    /// Whether a name is one of the rules, and the id it states.
    /// </summary>
    /// <param name="name">Name to look up.</param>
    /// <param name="rule">Rule id the name states, or zero when it is no rule.</param>
    /// <returns><c>true</c> when the name is one of the rules.</returns>
    public static bool TryParse(string? name, out int rule)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var entry in Rules)
            {
                if (string.Equals(entry.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    rule = entry.Rule;
                    return true;
                }
            }
        }

        rule = 0;
        return false;
    }

    /// <summary>Every rule name, for an error message that tells a reader what to write.</summary>
    private static string Names => string.Join(", ", Rules.Select(entry => entry.Name));
}
