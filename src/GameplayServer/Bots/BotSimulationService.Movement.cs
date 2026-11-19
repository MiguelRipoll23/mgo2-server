namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The walking half of a bot's tick, split from the aiming and firing half so
/// both files stay inside the repository's size limit.
/// </summary>
public sealed partial class BotSimulationService
{
    /// <summary>
    /// Walks a bot towards its objective, or holds it still while the room's
    /// bots are paused, and turns it towards the enemy it holds.
    /// </summary>
    /// <remarks>
    /// <b>A paused bot plans no route and takes no step.</b> It keeps its aim and
    /// its target, so a player can still be seen and a hit still lands on it, but
    /// it stands where the pause found it. The route key and deadline are cleared
    /// so the first tick after a resume plans again instead of waiting out a
    /// schedule from before the pause.
    /// </remarks>
    /// <param name="bot">The bot being moved.</param>
    /// <param name="body">Its body, which the step changes.</param>
    /// <param name="map">Map the round plays.</param>
    /// <param name="rule">Round's rule, which picks the placement family.</param>
    /// <param name="target">The enemy it holds, or <c>null</c>.</param>
    /// <param name="shot">The shot this beat fired, when it fired one.</param>
    /// <param name="firing">Whether it held its trigger this beat.</param>
    /// <param name="botsPaused">Whether the room's bots are held in place.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    private void MoveOrHold(
        BotParticipant bot,
        BotBodyState body,
        int map,
        int rule,
        BotActorView? target,
        BotShot? shot,
        bool firing,
        bool botsPaused,
        long now)
    {
        if (botsPaused)
        {
            body.Route = [];
            body.RouteIndex = 0;
            body.RouteKey = string.Empty;
            body.NextRouteMilliseconds = 0;
            body.Moving = false;
        }
        else
        {
            BotPosition? destination = null;
            if (target is not null && BotEngagementUtils.Distance(body, target) <= EngagementDistance)
            {
                // Close enough to shoot: stand, face it, and keep a route out of it.
                body.Route = [];
                body.Moving = false;
                body.Yaw = BotLocomotionService.YawTowards(target.X - body.X, target.Z - body.Z);
            }
            else
            {
                destination = target is null
                    ? Patrol(bot, body, map, rule)
                    : BotEngagementUtils.Standoff(body, target);
            }

            locomotion.PlanRoute(bot, body, map, rule, destination, now);
            locomotion.Step(body, now);
        }

        // A bot that holds its trigger faces its enemy, whichever way it walks: the
        // heading its position record carries is the one its hits name (BotShotUtils).
        // A paused bot still turns towards the enemy it sees, so it responds to a
        // player without moving or firing.
        if (target is not null && (firing || botsPaused))
        {
            body.Yaw = shot?.Bearing ?? BotEngagementUtils.Bearing(body, target);
        }
    }
}
