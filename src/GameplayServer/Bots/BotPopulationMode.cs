namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// How a bot holds its slot, as <c>BOT_POPULATION_MODE</c> states it.
/// </summary>
/// <remarks>
/// It is independent of <see cref="BotTeamPolicy"/>: the policy says which sides
/// a bot may stand on and this says whether a human may take the slot back, and
/// a deployment may state either combination. <c>NONE</c> wins over both
/// (<see cref="BotOptionsUtils.Team"/>), so a room with bots disabled has no
/// population to mode.
/// </remarks>
public enum BotPopulationMode
{
    /// <summary>
    /// A bot fills an available slot until a human takes it: joining humans
    /// displace the room's bots, newest bot first.
    /// </summary>
    Fill,

    /// <summary>
    /// A bot is a permanent participant: a joining human never displaces it, and
    /// the room is full when the humans and bots together reach its capacity.
    /// </summary>
    Permanent,
}
