using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Team Sneaking: get through, or keep the other side from getting through.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two halves are one objective because the bot's side decides which half
/// it is, and nothing on this host states which side attacks.</b> The mode is
/// asymmetric — one side infiltrates and the other defends — and the side a bot
/// holds does not say which it is: the rotation's triple carries the rule and no
/// attacker/defender bit, and no capture read here names one. So the objective
/// offers both, infiltration weighted above defence, and a bot's side is what a
/// later reading will use to choose between them. It is left open rather than
/// guessed.
/// </para>
/// </remarks>
public sealed class TeamSneakingBotObjective : IBotObjective
{
    /// <inheritdoc />
    public int Rule => MatchRuleUtils.TeamSneaking;

    /// <inheritdoc />
    public string Name => "Team Sneaking";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } =
        [BotObjectiveKind.Infiltrate, BotObjectiveKind.DefendObjective];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new(
                BotObjectiveKind.Infiltrate,
                BotAction.Advance,
                0.7,
                "One side of the mode infiltrates, and it is offered first."),
            new(
                BotObjectiveKind.DefendObjective,
                BotAction.DefendObjective,
                0.6,
                "The other side of the mode holds the line, and it is offered too."),
        ];
    }
}

/// <summary>
/// Sneaking: find and eliminate the player the mode names, or protect it.
/// </summary>
/// <remarks>
/// The mode is played around one player rather than a line, so a bot values
/// hunting that player and holding its position above fighting whoever else it
/// meets. Which player is the target is not on the sources this host reads, so
/// the objective names the behaviour and not a slot.
/// </remarks>
public sealed class SneakingBotObjective : IBotObjective
{
    /// <inheritdoc />
    public int Rule => MatchRuleUtils.Sneaking;

    /// <inheritdoc />
    public string Name => "Sneaking";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } =
        [BotObjectiveKind.EliminateTarget, BotObjectiveKind.ProtectTarget];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return
        [
            new(
                BotObjectiveKind.EliminateTarget,
                BotAction.Patrol,
                0.7,
                "The round is played around one player, and finding it is the work."),
            new(
                BotObjectiveKind.ProtectTarget,
                BotAction.Hold,
                0.55,
                "The other half of the mode is keeping that player where it is."),
        ];
    }
}
