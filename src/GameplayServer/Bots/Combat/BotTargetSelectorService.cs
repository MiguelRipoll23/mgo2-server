using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Chooses which enemy a bot aims at, from the players it can see.
/// </summary>
/// <remarks>
/// <para>
/// <b>The choice is the lowest slot, and the reason says why that is all this host
/// can do.</b> A target is normally chosen by distance, threat or objective value;
/// none of those is readable here — no position, health or aim is read for any
/// player — so the only stable ordering is the one the roster gives. It is
/// deterministic, which means a later reading of positions can replace the ordering
/// without changing anything around it.
/// </para>
/// <para>
/// No enemy visible is not an error: the bot has nothing to shoot at and the
/// caller is told so in prose.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class BotTargetSelectorService(ILogger<BotTargetSelectorService> logger)
{
    /// <summary>
    /// The enemy a bot would aim at.
    /// </summary>
    /// <param name="perception">What the bot can see.</param>
    /// <param name="reason">Why this target, or why none.</param>
    /// <returns>The target, or <c>null</c> when the bot sees no enemy.</returns>
    public BotPerceptionEntry? Select(BotPerceptionSnapshot perception, out string reason)
    {
        ArgumentNullException.ThrowIfNull(perception);

        if (perception.Enemies.Count == 0)
        {
            reason = "No enemy is visible to the bot.";
            return null;
        }

        var target = perception.Enemies.OrderBy(enemy => enemy.SessionSlot).First();
        reason =
            $"No distance, health or aim is read for a player, so the lowest slot is the target: {target.Name}.";
        logger.LogDebug(
            "Target {TargetName} in slot {TargetSlot} chosen from {EnemyCount} enemy(s) by slot order",
            target.Name,
            target.SessionSlot,
            perception.Enemies.Count);
        return target;
    }
}
