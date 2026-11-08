using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Runs one bot for one tick: it perceives, takes its objective's candidates, scores
/// them with its role, decides, and updates its body state.
/// </summary>
/// <remarks>
/// <para>
/// <b>The tick is the orchestration the whole architecture exists for.</b> Every
/// other system is a plain object this controller calls in order, so a bot is
/// assembled from parts rather than implemented as one class with branches: the
/// mode supplies candidates, the role reweights them, the decision system chooses,
/// the state machine moves the body and the combat evaluation says what the bot
/// could do about what it sees.
/// </para>
/// <para>
/// <b>Nothing is sent.</b> A tick ends in a state, a decision and reasons; the
/// records that would show a client a bot moving, aiming or shooting have not been
/// measured for this host, so the tick is the intelligence and not the wire.
/// </para>
/// </remarks>
/// <param name="perception">Reads what the bot can see.</param>
/// <param name="objectives">Resolves the round's mode to its objective.</param>
/// <param name="decisions">Chooses between the candidates.</param>
/// <param name="combat">States whether the bot can act on its weapon.</param>
/// <param name="navigation">Asks for a route when there is somewhere to go.</param>
/// <param name="destinations">Chooses where the bot would go, if anywhere.</param>
/// <param name="logger">Logger of this controller.</param>
public sealed class BotController(
    BotPerceptionService perception,
    BotObjectiveService objectives,
    BotDecisionService decisions,
    BotCombatService combat,
    BotNavigationAgent navigation,
    BotMovementDestinationService destinations,
    ILogger<BotController> logger)
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, BotState> states = [];

    /// <summary>
    /// Takes one bot's tick.
    /// </summary>
    /// <param name="bot">The bot being ticked.</param>
    /// <param name="matchIdentifier">The room it is in.</param>
    /// <param name="roundNumber">The round the room is on.</param>
    /// <param name="rule">The round's rule.</param>
    /// <param name="map">The round's map.</param>
    /// <param name="cancellationToken">Token that cancels the perception read.</param>
    /// <returns>What the bot saw, chose, would do and where it can walk.</returns>
    public async Task<BotTick> TickAsync(
        BotParticipant bot,
        int matchIdentifier,
        int roundNumber,
        int rule,
        int map,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var seen = await perception.ReadAsync(bot, cancellationToken);
        var objective = objectives.Resolve(rule);
        var context = new BotContext(
            matchIdentifier,
            roundNumber,
            rule,
            map,
            bot,
            bot.Role,
            seen,
            objective);

        var choices = objective.Plans(context)
            .Select(plan => new BotActionChoice(
                plan.Action,
                BotUtilityUtils.Combine(
                    plan.Utility,
                    BotRoleCatalogUtils.Weight(bot.Role, plan.Action)),
                plan.Reason))
            .ToList();

        var decision = decisions.Decide(context, choices);

        BotState state;
        lock (gate)
        {
            states.TryGetValue(bot.Name, out var previous);
            state = BotStateMachineUtils.Next(previous, decision);
            states[bot.Name] = state;
        }

        var outcome = combat.Evaluate(context);
        var destination = destinations.Choose(map, bot.SessionSlot, rule);
        var route = destination.Position is { } target
            ? navigation.Navigate(
                bot.Name,
                new BotNavigationRequest(map, From: null, To: target, Rule: rule))
            : BotNavigationResult.Unsupported(destination.Reason);

        logger.LogDebug(
            "Bot {BotName} in slot {BotSlot}: {State} after {Action} ({Reason}); may engage: {CanEngage}; "
            + "route: {RouteStatus} — {RouteReason}",
            bot.Name,
            bot.SessionSlot,
            state,
            decision.Action,
            decision.Reason,
            outcome.CanEngage,
            route.Status,
            route.Reason);

        return new BotTick(state, decision, outcome, route, seen);
    }

    /// <summary>Forgets a bot's body state, because the bot is gone.</summary>
    /// <param name="botName">The bot that left.</param>
    public void Forget(string botName)
    {
        lock (gate)
        {
            states.Remove(botName);
        }
    }
}

/// <summary>
/// One bot's tick, as the controller leaves it.
/// </summary>
/// <param name="State">The body state the bot is in.</param>
/// <param name="Decision">The action it chose and why.</param>
/// <param name="Combat">Its weapon and whether it can engage.</param>
/// <param name="Navigation">Where it could walk, or why nowhere.</param>
/// <param name="Perception">What it could see.</param>
public sealed record BotTick(
    BotState State,
    BotDecision Decision,
    BotCombatOutcome Combat,
    BotNavigationResult Navigation,
    BotPerceptionSnapshot Perception);
