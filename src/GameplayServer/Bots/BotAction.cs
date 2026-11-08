namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What a bot decides to do next, as the utility system names it.
/// </summary>
/// <remarks>
/// <para>
/// <b>An action is a goal and not a keystroke.</b> <see cref="Advance"/> says the
/// bot wants to close distance; <see cref="Engage"/> says it wants to fight what
/// it can see. How either is carried out belongs to the navigation and combat
/// systems, and which of them a mode offers belongs to its objective
/// (<see cref="IBotObjective"/>). Keeping the three apart is what stops a role
/// from implementing a mode and a mode from implementing a role.
/// </para>
/// </remarks>
public enum BotAction
{
    /// <summary>Stay where the bot is and watch.</summary>
    Hold,

    /// <summary>Close distance: towards an objective or a target.</summary>
    Advance,

    /// <summary>Break contact and give ground.</summary>
    Retreat,

    /// <summary>Bring a weapon onto a target and fire.</summary>
    Engage,

    /// <summary>Help a teammate: covering fire, a revive, a resupply.</summary>
    Support,

    /// <summary>Walk a route with no fixed objective.</summary>
    Patrol,

    /// <summary>Take an objective the mode scores: a base, a target, a disk.</summary>
    CaptureObjective,

    /// <summary>Hold an objective the mode scores against the other side.</summary>
    DefendObjective,

    /// <summary>Collect or carry the mode's own object.</summary>
    RetrieveObjective,

    /// <summary>Leave a losing fight while the bot still can.</summary>
    Escape,
}

/// <summary>
/// One action a bot could take, with the utility the decision system compares it
/// by and the reason it was proposed.
/// </summary>
/// <remarks>
/// The appearance of a numeric utility is what makes this a utility system and
/// not a chain of conditionals: every candidate is scored the same way by the
/// objective and the role, and the highest wins. The reason travels with the
/// score so a chosen action can be logged as a sentence rather than a number.
/// </remarks>
/// <param name="Action">The action proposed.</param>
/// <param name="Utility">How valuable the action is, zero to one.</param>
/// <param name="Reason">Why the action was proposed, in prose.</param>
public sealed record BotActionChoice(BotAction Action, double Utility, string Reason);

/// <summary>
/// The action a bot takes this tick, with the score that chose it.
/// </summary>
/// <param name="Action">The chosen action.</param>
/// <param name="Utility">The score that chose it.</param>
/// <param name="Reason">Why it was chosen, in prose.</param>
public sealed record BotDecision(BotAction Action, double Utility, string Reason);
