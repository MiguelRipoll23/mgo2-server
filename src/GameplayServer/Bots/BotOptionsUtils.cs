namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Parses and validates the two bot settings a deployment states, so that an
/// unreadable value is a named startup fault rather than a silent fallback.
/// </summary>
/// <remarks>
/// <para>
/// <b>The values are matched exactly, ignoring case and nothing else.</b> A
/// deployment that writes <c>ENEMYS</c>, <c>0</c> or an empty string is told so;
/// mapping it onto the nearest setting would start a room with bots on a side
/// nobody asked for, and the operator's only clue would be the room's
/// population.
/// </para>
/// <para>
/// The parse functions are separate from the readers so a caller can ask whether
/// a value is readable without throwing: startup validation asks, and the code
/// that runs a room reads through <see cref="Team"/> and
/// <see cref="PopulationMode"/> only after it has been validated.
/// </para>
/// </remarks>
public static class BotOptionsUtils
{
    /// <summary>Whether a population mode counts bots on both sides.</summary>
    /// <param name="value">Text a deployment stated.</param>
    /// <param name="policy">The policy the text names.</param>
    /// <returns><c>true</c> when the text names a policy.</returns>
    public static bool TryParseTeam(string? value, out BotTeamPolicy policy)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "NONE":
                policy = BotTeamPolicy.None;
                return true;
            case "BOTH":
                policy = BotTeamPolicy.Both;
                return true;
            case "ENEMY":
                policy = BotTeamPolicy.Enemy;
                return true;
            default:
                policy = BotTeamPolicy.None;
                return false;
        }
    }

    /// <summary>Whether a population mode is <c>FILL</c> or <c>PERMANENT</c>.</summary>
    /// <param name="value">Text a deployment stated.</param>
    /// <param name="mode">The mode the text names.</param>
    /// <returns><c>true</c> when the text names a mode.</returns>
    public static bool TryParsePopulationMode(string? value, out BotPopulationMode mode)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "FILL":
                mode = BotPopulationMode.Fill;
                return true;
            case "PERMANENT":
                mode = BotPopulationMode.Permanent;
                return true;
            default:
                mode = BotPopulationMode.Fill;
                return false;
        }
    }

    /// <summary>
    /// The policy a deployment stated, or a refusal naming the value.
    /// </summary>
    /// <param name="options">Options read from the environment.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="InvalidOperationException">The value names no policy.</exception>
    public static BotTeamPolicy Team(Shared.Options.BotOptions options)
    {
        if (TryParseTeam(options.Team, out var policy))
        {
            return policy;
        }

        throw new InvalidOperationException(
            $"BOT_TEAM is '{options.Team}'; it must be NONE, BOTH or ENEMY.");
    }

    /// <summary>
    /// The population mode a deployment stated, or a refusal naming the value.
    /// </summary>
    /// <param name="options">Options read from the environment.</param>
    /// <returns>The mode.</returns>
    /// <exception cref="InvalidOperationException">The value names no mode.</exception>
    public static BotPopulationMode PopulationMode(Shared.Options.BotOptions options)
    {
        if (TryParsePopulationMode(options.PopulationMode, out var mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            $"BOT_POPULATION_MODE is '{options.PopulationMode}'; it must be FILL or PERMANENT.");
    }

    /// <summary>
    /// Every fault in the bot settings, as sentences a startup can print before
    /// it refuses to run.
    /// </summary>
    /// <param name="options">Options read from the environment.</param>
    /// <returns>The faults, or an empty list when the settings are readable.</returns>
    public static IReadOnlyList<string> Validate(Shared.Options.BotOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var faults = new List<string>();

        if (!TryParseTeam(options.Team, out _))
        {
            faults.Add($"BOT_TEAM is '{options.Team}'; it must be NONE, BOTH or ENEMY.");
        }

        if (!TryParsePopulationMode(options.PopulationMode, out _))
        {
            faults.Add(
                $"BOT_POPULATION_MODE is '{options.PopulationMode}'; it must be FILL or PERMANENT.");
        }

        if (options.Count < 0)
        {
            faults.Add($"BOT_COUNT is {options.Count}; a bot count cannot be negative.");
        }

        if (options.PopulationIntervalSeconds <= 0)
        {
            faults.Add(
                $"BOT_POPULATION_INTERVAL_SECONDS is {options.PopulationIntervalSeconds}; a "
                + "population pass needs a positive interval.");
        }

        return faults;
    }
}
