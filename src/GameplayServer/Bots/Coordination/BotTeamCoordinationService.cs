using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Shares a mode's objectives out among a room's bots so that they do not all
/// work on the same one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Distribution is by slot order and by the mode's own objective order.</b>
/// The bots are taken in the order their slots give, and the objectives in the
/// order the mode states them, each objective taken by one bot before any is taken
/// twice. A room of three in Base therefore gets one defender, one attacker and one
/// defender again, rather than three attackers: the assignment is deterministic, so
/// the same room assigns the same work every pass, and a bot that leaves is
/// replaced in its own place rather than by a reshuffle.
/// </para>
/// <para>
/// Role distribution is the role the bot was created with
/// (<see cref="BotRoleAssignmentService"/>); this service assigns the objective and
/// leaves the archetype alone, so a bot's role stays consistent through a match
/// while what the round asks it to do changes with the mode.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class BotTeamCoordinationService(ILogger<BotTeamCoordinationService> logger)
{
    /// <summary>
    /// Assigns an objective to each bot of a room.
    /// </summary>
    /// <param name="bots">The room's bots.</param>
    /// <param name="objective">The mode's objective this round.</param>
    /// <returns>One assignment per bot, in slot order.</returns>
    public IReadOnlyList<BotAssignment> Assign(
        IReadOnlyList<BotParticipant> bots,
        IBotObjective objective)
    {
        ArgumentNullException.ThrowIfNull(bots);
        ArgumentNullException.ThrowIfNull(objective);

        var ordered = bots.OrderBy(bot => bot.SessionSlot).ToList();
        var kinds = objective.ObjectiveKinds;
        var assignments = new List<BotAssignment>(ordered.Count);

        for (var index = 0; index < ordered.Count; index++)
        {
            var bot = ordered[index];
            var kind = kinds.Count == 0
                ? BotObjectiveKind.Unknown
                : kinds[index % kinds.Count];
            assignments.Add(new BotAssignment(bot.Name, bot.Role, kind, bot.Team));
        }

        if (assignments.Count > 0)
        {
            logger.LogDebug(
                "{Mode}: {BotCount} bot(s) assigned {Assignments}",
                objective.Name,
                assignments.Count,
                string.Join(
                    ", ",
                    assignments.Select(assignment =>
                        $"{assignment.BotName}={assignment.Role}/{assignment.Objective}")));
        }

        return assignments;
    }
}
