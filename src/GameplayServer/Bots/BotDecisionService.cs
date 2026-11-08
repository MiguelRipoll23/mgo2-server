using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Chooses one action from the candidates the objective and the role proposed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The highest utility wins and ties are broken by the action's own order.</b>
/// Two actions worth the same are not a coin toss: the tie goes to whichever is
/// listed first in <see cref="BotAction"/>, so the same room in the same state
/// always produces the same decision. A bot that changed its mind between two
/// equally good actions every tick would look unstable and would be untestable.
/// </para>
/// <para>
/// An empty candidate list is not an error: it is a mode with nothing it can
/// value yet, and the bot holds, with the reason said out loud.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class BotDecisionService(ILogger<BotDecisionService> logger)
{
    /// <summary>
    /// The best candidate, or a hold when nothing was proposed.
    /// </summary>
    /// <param name="context">The match state the decision is taken in.</param>
    /// <param name="choices">Actions the mode and the role proposed.</param>
    /// <returns>The chosen action.</returns>
    public BotDecision Decide(BotContext context, IReadOnlyList<BotActionChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(choices);

        if (choices.Count == 0)
        {
            logger.LogDebug(
                "Bot {BotName}: {Objective} proposed no action in round {RoundNumber}; it holds",
                context.Participant.Name,
                context.Objective.Name,
                context.RoundNumber);
            return new BotDecision(
                BotAction.Hold,
                0,
                $"No action was proposed by {context.Objective.Name}.");
        }

        var best = choices
            .OrderByDescending(choice => choice.Utility)
            .ThenBy(choice => choice.Action)
            .First();

        logger.LogDebug(
            "Bot {BotName}: {Action} at {Utility:0.00} chosen from {CandidateCount} candidate(s) — {Reason}",
            context.Participant.Name,
            best.Action,
            best.Utility,
            choices.Count,
            best.Reason);

        return new BotDecision(best.Action, best.Utility, best.Reason);
    }
}
