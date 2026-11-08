namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Which sides of a match may hold bots, as <c>BOT_TEAM</c> states it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It names a rule about sides and never a side.</b> <c>ENEMY</c> is resolved
/// per match against the sides the players are actually on
/// (<see cref="BotTeamAssignmentService"/>), so a host never assumes that one
/// particular side is its own and the same build plays correctly on either side.
/// </para>
/// <para>
/// <c>NONE</c> is not a degenerate <c>BOTH</c>: it is the switch that keeps every
/// bot out of the room whatever the population mode says, so a deployment that
/// turns bots off does not have to reason about which mode it left behind.
/// </para>
/// </remarks>
public enum BotTeamPolicy
{
    /// <summary>Bots are disabled: no bot is spawned, whatever the population mode.</summary>
    None,

    /// <summary>Bots may hold a side on both sides of the match.</summary>
    Both,

    /// <summary>Bots may hold only the side the human players are not on.</summary>
    Enemy,
}
