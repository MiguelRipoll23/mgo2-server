namespace Mgo2Server.Shared.Options;

/// <summary>
/// Options that state whether this host plays bots, and how they hold their
/// slots.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two settings the deployment states are text, and they stay text
/// here.</b> <c>BOT_TEAM</c> and <c>BOT_POPULATION_MODE</c> are read as written
/// and parsed by <c>BotOptionsUtils</c> at startup, so a misspelling is a named
/// startup fault rather than a silent fallback to a default the deployment did
/// not ask for. A host that cannot say which sides its bots may hold is a host
/// that must not guess.
/// </para>
/// <para>
/// <b>Count is this server's own number and not part of the two settings.</b>
/// <c>BOT_TEAM</c> says where a bot may stand and <c>BOT_POPULATION_MODE</c> says
/// whether a human may take its place; neither says how many bots a room holds.
/// The default is zero, and under <c>FILL</c> zero is the room's own size: a host
/// told no count fills the seats it advertises, so a deployment states a number
/// only when it wants a fill smaller than its room.
/// </para>
/// </remarks>
public sealed class BotOptions
{
    /// <summary>
    /// Sides bots may hold: <c>NONE</c>, <c>BOTH</c> or <c>ENEMY</c>. Parsed by
    /// <c>Mgo2Server.GameplayServer.Bots.BotOptionsUtils</c>.
    /// </summary>
    public string Team { get; set; } = "ENEMY";

    /// <summary>
    /// How a bot holds its slot: <c>FILL</c> or <c>PERMANENT</c>. Parsed by
    /// <c>Mgo2Server.GameplayServer.Bots.BotOptionsUtils</c>.
    /// </summary>
    public string PopulationMode { get; set; } = "FILL";

    /// <summary>
    /// The size of the room's bot population, capacity permitting. Under
    /// <c>FILL</c> it is the size of the fill in players, so a joining player takes
    /// a bot's seat and one bot leaves before the room is written to that player;
    /// under <c>PERMANENT</c> it is the bots themselves. Zero is the default and
    /// under <c>FILL</c> it is every seat the players are not in, so a host told no
    /// count fills the room it advertises.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Interval between two population passes, in seconds. It is short because a
    /// bot that fills a slot has to leave it when a human joins, and the room's
    /// roster is the only thing that says a human did.
    /// </summary>
    public int PopulationIntervalSeconds { get; set; } = 5;
}
