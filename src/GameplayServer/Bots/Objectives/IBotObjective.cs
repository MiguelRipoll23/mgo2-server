namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// What a mode asks a bot to accomplish, independent of how any one mode is
/// played.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every mode is an implementation of this and nothing in the bot system
/// names a rule.</b> A mode answers two questions: which kinds of objective it
/// scores, and how valuable each of those is to a bot in a given state. The
/// decision system does the rest, so a new mode is one implementation beside the
/// others and no change to perception, navigation or combat.
/// </para>
/// <para>
/// The kinds are deliberately coarse. A base, a capture target, a disk and a
/// rescue point are all <see cref="BotObjectiveKind.CaptureObjective"/> to a bot
/// because what it does is the same — go there and hold it — and the mode knows
/// which of its own objects that means. What the kinds do not say is where
/// anything is, because this host does not know.
/// </para>
/// </remarks>
public interface IBotObjective
{
    /// <summary>The rule this objective is the behaviour of.</summary>
    int Rule { get; }

    /// <summary>The mode's name, for a log line.</summary>
    string Name { get; }

    /// <summary>
    /// The kinds of objective the mode scores, the most important first.
    /// </summary>
    IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; }

    /// <summary>
    /// The actions the mode values for a bot in a given state.
    /// </summary>
    /// <param name="context">The match state the bot is deciding in.</param>
    /// <returns>One candidate per action the mode values, scored zero to one.</returns>
    IReadOnlyList<BotObjectivePlan> Plans(BotContext context);
}

/// <summary>
/// The kinds of thing a mode asks a bot to do.
/// </summary>
public enum BotObjectiveKind
{
    /// <summary>Kill players on the other side.</summary>
    EliminateEnemies,

    /// <summary>Keep the bot's own players alive and fighting.</summary>
    SupportTeammates,

    /// <summary>Take an object the mode scores: a base, a target, a disk.</summary>
    CaptureObjective,

    /// <summary>Hold an object the mode scores against the other side.</summary>
    DefendObjective,

    /// <summary>Collect or carry the mode's own object.</summary>
    RetrieveObjective,

    /// <summary>Keep a player or an object the mode protects alive.</summary>
    ProtectTarget,

    /// <summary>Find and eliminate the player the mode names.</summary>
    EliminateTarget,

    /// <summary>Get past the other side without being seen.</summary>
    Infiltrate,

    /// <summary>Walk the map with no fixed object.</summary>
    Patrol,

    /// <summary>A mode with nothing this server can state asks for.</summary>
    Unknown,
}

/// <summary>
/// One action a mode values, and why.
/// </summary>
/// <param name="Kind">The objective the action serves.</param>
/// <param name="Action">The action proposed.</param>
/// <param name="Utility">How valuable the action is, zero to one.</param>
/// <param name="Reason">Why the mode values it, in prose.</param>
public sealed record BotObjectivePlan(
    BotObjectiveKind Kind,
    BotAction Action,
    double Utility,
    string Reason);
