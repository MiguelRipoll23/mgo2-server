namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The body states an action puts a bot in, and the transitions between them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a table and not a conditional chain.</b> Every action maps to the
/// state its character would be in, and a death is the one state nothing leaves
/// until a round ends. Keeping the mapping here means a new action is one line
/// beside the others rather than another branch in a controller.
/// </para>
/// <para>
/// No state here sends anything. A state says where a bot's body would be; the
/// records that would tell a client about it have not been measured for this
/// host, so a state is a decision's shadow and not a packet.
/// </para>
/// </remarks>
public static class BotStateMachineUtils
{
    /// <summary>
    /// The state an action moves a bot into, from the state it is in.
    /// </summary>
    /// <param name="current">The state the bot is in.</param>
    /// <param name="decision">The action just chosen.</param>
    /// <returns>The state the bot is in afterwards.</returns>
    public static BotState Next(BotState current, BotDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        // A dead bot stays dead: the round, and not a decision, is what relives it.
        if (current == BotState.Dead)
        {
            return BotState.Dead;
        }

        return decision.Action switch
        {
            BotAction.Advance
                or BotAction.Retreat
                or BotAction.Patrol
                or BotAction.CaptureObjective
                or BotAction.DefendObjective
                or BotAction.RetrieveObjective
                or BotAction.Escape => BotState.Moving,
            BotAction.Engage => BotState.Aiming,
            BotAction.Support => BotState.Waiting,
            _ => BotState.Waiting,
        };
    }

    /// <summary>
    /// Whether a state is one a bot can act from.
    /// </summary>
    /// <param name="state">State to test.</param>
    /// <returns><c>true</c> for every state but death.</returns>
    public static bool IsActing(BotState state) => state != BotState.Dead;
}
