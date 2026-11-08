using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Base: take the other side's base and hold the bot's own.
/// </summary>
/// <remarks>
/// The rule's own name states both halves, and the round is scored on holding
/// ground rather than on kills, so the objective values standing on a point above
/// engaging and pressing an attack above waiting.
/// </remarks>
public sealed class BaseBotObjective : IBotObjective
{
    /// <inheritdoc />
    public int Rule => MatchRuleUtils.Base;

    /// <inheritdoc />
    public string Name => "Base";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } =
        [BotObjectiveKind.DefendObjective, BotObjectiveKind.CaptureObjective];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new(
                BotObjectiveKind.DefendObjective,
                BotAction.DefendObjective,
                0.7,
                "The mode is scored on holding a base, and the bot's own is worth holding."),
            new(
                BotObjectiveKind.CaptureObjective,
                BotAction.CaptureObjective,
                0.65,
                "The other side's base is the mode's other half."),
        ];
    }
}

/// <summary>
/// Capture: take and carry the mode's object.
/// </summary>
/// <remarks>
/// A capture round is scored on the object rather than on kills, so the objective
/// values movement towards it and defence of it above engagement, and support
/// higher than in a deathmatch because a carrier is worth escorting.
/// </remarks>
public sealed class CaptureBotObjective : IBotObjective
{
    /// <inheritdoc />
    public int Rule => MatchRuleUtils.Capture;

    /// <inheritdoc />
    public string Name => "Capture";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } =
        [BotObjectiveKind.CaptureObjective, BotObjectiveKind.RetrieveObjective, BotObjectiveKind.SupportTeammates];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new(
                BotObjectiveKind.CaptureObjective,
                BotAction.CaptureObjective,
                0.75,
                "The round is scored on taking the objective."),
            new(
                BotObjectiveKind.RetrieveObjective,
                BotAction.RetrieveObjective,
                0.7,
                "The mode's object is what a capture round is played for."),
            new(
                BotObjectiveKind.SupportTeammates,
                BotAction.Support,
                0.45,
                "A carrier is worth escorting."),
        ];
    }
}

/// <summary>
/// Rescue: retrieve the mode's object while the side's lives are limited.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lives are the mode's own rule and the objective respects them.</b> A rescue
/// round gives each side a small number of lives, so a bot values retrieving the
/// object above a fight and values breaking contact above pressing one: a death
/// costs the side one of the few it has.
/// </para>
/// <para>
/// What the mode protects is stated as one kind rather than two, because whether
/// the object is a disk, a target or a player is the mode's business and the bot's
/// behaviour is the same.
/// </para>
/// </remarks>
public sealed class RescueBotObjective : IBotObjective
{
    /// <inheritdoc />
    public int Rule => MatchRuleUtils.Rescue;

    /// <inheritdoc />
    public string Name => "Rescue";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } =
        [BotObjectiveKind.RetrieveObjective, BotObjectiveKind.ProtectTarget, BotObjectiveKind.EliminateEnemies];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new(
                BotObjectiveKind.RetrieveObjective,
                BotAction.RetrieveObjective,
                0.8,
                "The round is won by retrieving, not by kills."),
            new(
                BotObjectiveKind.ProtectTarget,
                BotAction.Hold,
                0.55,
                "The mode protects an object the side cannot lose."),
            new(
                BotObjectiveKind.EliminateEnemies,
                BotAction.Escape,
                0.3,
                "A death costs the side one of its few lives, so it keeps its distance."),
        ];
    }
}
