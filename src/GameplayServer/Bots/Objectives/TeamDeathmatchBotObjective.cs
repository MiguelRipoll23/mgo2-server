using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Team Deathmatch: eliminate the other side and keep the team fighting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Engagement comes first and support second, in that order and by a small
/// margin.</b> The rule scores kills twice over — a ticket limit and the score the
/// round is decided on (<c>docs/protocol/GAME_SETTINGS_WIRE.md</c> §2 gives TDM
/// both a time and a ticket count) — so killing is the mode's own work, and a bot
/// that never fights is a bot whose team loses a player's worth of tickets.
/// Assistance is valued below it so a bot chooses support when it has nothing to
/// shoot at rather than in preference to shooting.
/// </para>
/// <para>
/// Deathmatch is the same behaviour without sides, so it reads through this
/// objective too: the mode's own plan is the same even when every bot is alone on
/// its side.
/// </para>
/// </remarks>
public sealed class TeamDeathmatchBotObjective : IBotObjective
{
    /// <inheritdoc />
    public int Rule => MatchRuleUtils.TeamDeathmatch;

    /// <inheritdoc />
    public string Name => "Team Deathmatch";

    /// <inheritdoc />
    public IReadOnlyList<BotObjectiveKind> ObjectiveKinds { get; } =
        [BotObjectiveKind.EliminateEnemies, BotObjectiveKind.SupportTeammates];

    /// <inheritdoc />
    public IReadOnlyList<BotObjectivePlan> Plans(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plans = new List<BotObjectivePlan>(2);

        if (context.HasEnemies)
        {
            plans.Add(new BotObjectivePlan(
                BotObjectiveKind.EliminateEnemies,
                BotAction.Engage,
                0.85,
                "The round is decided on kills and an enemy is visible."));
        }
        else
        {
            plans.Add(new BotObjectivePlan(
                BotObjectiveKind.EliminateEnemies,
                BotAction.Patrol,
                0.6,
                "No enemy is visible, so the bot looks for one."));
        }

        if (context.Perception.Teammates.Count > 0)
        {
            plans.Add(new BotObjectivePlan(
                BotObjectiveKind.SupportTeammates,
                BotAction.Support,
                0.5,
                "The bot has a teammate it can cover."));
        }

        return plans;
    }
}
