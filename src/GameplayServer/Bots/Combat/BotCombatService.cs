using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Whether a bot can fight, and what it would fight with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Engaging is refused, with the reason, and that is the implementation.</b>
/// Aiming, firing and reloading are operations-stream records the client sends:
/// this host has no builder for them, and the operations docs show no measured
/// shape for a host writing one on a player's behalf
/// (<c>docs/analysis/supervisor-opcode-builders.md</c>). A bot that "fired" would
/// be a bot whose shot exists in a log and not in the match, which is exactly the
/// invented mechanic the project forbids. So the evaluation resolves the weapon and
/// the target and says it does not shoot.
/// </para>
/// <para>
/// What it does establish is the seam: a weapon profile, a selected target and a
/// reason, all deterministic. The measurement that changes this is one record.
/// </para>
/// </remarks>
/// <param name="weapons">Catalog the weapon is resolved from.</param>
/// <param name="targets">Selector the target is chosen by.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotCombatService(
    BotWeaponCatalogService weapons,
    BotTargetSelectorService targets,
    ILogger<BotCombatService> logger)
{
    /// <summary>
    /// What the bot would do if it could fight.
    /// </summary>
    /// <param name="context">The match state the evaluation is for.</param>
    /// <returns>The weapon it holds, the target it has, and why it does not engage.</returns>
    public BotCombatOutcome Evaluate(BotContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var weapon = weapons.Preferred(context.Role)
            .Select(weapons.Profile)
            .FirstOrDefault();

        var target = targets.Select(context.Perception, out var reason);
        if (target is null)
        {
            return new BotCombatOutcome(false, reason, weapon);
        }

        logger.LogDebug(
            "Bot {BotName} has {TargetName} in view with {Weapon} and does not shoot: no aim, fire or "
            + "reload record has been measured for a host",
            context.Participant.Name,
            target.Name,
            weapon?.Identifier.ToString() ?? "no weapon");

        return new BotCombatOutcome(
            CanEngage: false,
            $"A target is visible and the weapon is resolved, but no aim, fire or reload record has been "
            + "measured for a host, so the bot does not shoot.",
            weapon);
    }
}

/// <summary>
/// The result of a bot's combat evaluation.
/// </summary>
/// <param name="CanEngage">Whether the bot can act on its weapon this tick.</param>
/// <param name="Reason">Why it can or cannot, in prose.</param>
/// <param name="Weapon">The weapon profile it holds, or <c>null</c> when none resolved.</param>
public sealed record BotCombatOutcome(bool CanEngage, string Reason, BotWeaponProfile? Weapon);
