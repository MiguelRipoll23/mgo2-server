namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Chooses the objective the round's rule plays, and names the gap when it has
/// none.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule comes from the room's own rotation and nothing else.</b> The round
/// a host opens names its rule (<c>MatchSettings.Round</c>), and that number is
/// what this service resolves, so a bot changes mode exactly when the room does —
/// on a rotation, not on a timer and not on a map. A room that switches from
/// Team Deathmatch to Base between rounds switches the bots' objectives with it.
/// </para>
/// <para>
/// An unresolved rule is logged at debug and answered with
/// <see cref="FallbackBotObjective"/>, whose plan holds the bot still. It is not
/// a guess at the nearest mode.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class BotObjectiveService(ILogger<BotObjectiveService> logger)
{
    private readonly Dictionary<int, IBotObjective> objectives =
        new List<IBotObjective>
        {
            new TeamDeathmatchBotObjective(),
            new RescueBotObjective(),
            new CaptureBotObjective(),
            new SneakingBotObjective(),
            new BaseBotObjective(),
            new TeamSneakingBotObjective(),
        }.ToDictionary(objective => objective.Rule);

    private static readonly IBotObjective Fallback = new FallbackBotObjective();

    /// <summary>Every mode this server has a behaviour for.</summary>
    public IReadOnlyList<IBotObjective> Supported => [.. objectives.Values];

    /// <summary>
    /// The objective a rule plays.
    /// </summary>
    /// <param name="rule">The round's rule, in the replay decoder's numbering.</param>
    /// <returns>The mode's objective, or the fallback.</returns>
    public IBotObjective Resolve(int rule)
    {
        if (objectives.TryGetValue(rule, out var objective))
        {
            return objective;
        }

        logger.LogDebug(
            "Rule {Rule} has no objective in this server; bots walk and watch for the round",
            rule);
        return Fallback;
    }
}
