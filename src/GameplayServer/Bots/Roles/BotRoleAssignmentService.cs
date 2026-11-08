using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Decides which role a new bot plays, and how a room's bots share the roles out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Distribution is round-robin over the catalog, so a team of five bots holds
/// one of each archetype</b> rather than five snipers because five bots joined in
/// a row. It is deterministic: the bot's own sequence number is the index, so the
/// same room in the same order assigns the same roles, and a replaced bot's
/// successor takes the next place in the rotation rather than a random one.
/// </para>
/// <para>
/// <b>The mode tilts the order and does not replace it.</b> A sneaking round wants
/// more reconnaissance and infiltration on the field, so those archetypes come
/// first in that mode's rotation. Every archetype is still reached as bots are
/// added, which is what keeps a bot able to adapt rather than being locked to a
/// round's flavour.
/// </para>
/// </remarks>
public sealed class BotRoleAssignmentService
{
    private static readonly BotRole[] StandardOrder =
    [
        BotRole.Assault,
        BotRole.Support,
        BotRole.Sniper,
        BotRole.Recon,
        BotRole.Infiltrator,
    ];

    private static readonly BotRole[] StealthOrder =
    [
        BotRole.Recon,
        BotRole.Infiltrator,
        BotRole.Support,
        BotRole.Assault,
        BotRole.Sniper,
    ];

    private static readonly BotRole[] DefenceOrder =
    [
        BotRole.Support,
        BotRole.Sniper,
        BotRole.Assault,
        BotRole.Recon,
        BotRole.Infiltrator,
    ];

    /// <summary>
    /// The role a bot with a sequence number plays in a mode.
    /// </summary>
    /// <param name="number">The bot's sequence number, counting from one.</param>
    /// <param name="rule">The round's rule, in the replay decoder's numbering.</param>
    /// <returns>The role the bot plays.</returns>
    public BotRole Choose(int number, int rule)
    {
        var order = OrderFor(rule);
        var index = Math.Max(0, number - 1) % order.Length;
        return order[index];
    }

    /// <summary>The rotation a mode plays its bots' roles in.</summary>
    /// <param name="rule">The round's rule.</param>
    /// <returns>The rotation, most wanted first.</returns>
    private static BotRole[] OrderFor(int rule) => rule switch
    {
        MatchRuleUtils.TeamSneaking or MatchRuleUtils.Sneaking => StealthOrder,
        MatchRuleUtils.Base or MatchRuleUtils.Bomb => DefenceOrder,
        _ => StandardOrder,
    };
}
