namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The arithmetic of a bot population: how many bots a room should hold for the
/// humans in it and the mode it is in.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the whole difference between the two modes.</b> <c>FILL</c> counts the
/// humans first and gives the bots what is left, which is what makes a joining
/// player displace a bot rather than be refused; <c>PERMANENT</c> counts the bots
/// first and bounds them by the room alone, so a human who cannot be seated is
/// turned away rather than the bot. Both are bounded by the room's capacity, and
/// neither invents a number: the count is the deployment's.
/// <para>
/// <b><c>FILL</c> fills a room a player is in, and an empty room is nobody's to
/// fill.</b> With no human on the roster it answers zero and the room's bots are
/// removed, so a host that has been running with nobody in it holds no population
/// and writes no briefing for one; the first human to arrive brings the bots back
/// on the next pass. <c>PERMANENT</c> is the mode that holds its bots with or
/// without a player, which is what makes them permanent.
/// </para>
/// </para>
/// </remarks>
public static class BotPopulationUtils
{
    /// <summary>The bots a room should hold.</summary>
    /// <param name="mode">How a bot holds its slot.</param>
    /// <param name="configured">The deployment's bot count.</param>
    /// <param name="humanCount">Humans on the roster, the host not counted.</param>
    /// <param name="capacity">Player slots the room has.</param>
    /// <returns>The number of bots the room should hold.</returns>
    public static int TargetCount(
        BotPopulationMode mode,
        int configured,
        int humanCount,
        int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(configured);
        ArgumentOutOfRangeException.ThrowIfNegative(humanCount);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (mode == BotPopulationMode.Fill)
        {
            // A room with no player in it is not filled: FILL gives a joining human
            // the seats the bots hold, and a room nobody has joined holds none of
            // them. The first human to arrive brings the population back.
            if (humanCount == 0)
            {
                return 0;
            }

            // The deployment's count when it states one, and every seat the
            // players are not in when it does not. A fill of the room alone put
            // sixteen bots in a seventeen-seat room whatever `BOT_COUNT` said, and
            // the reference room it is measured against holds seven; a count that
            // is set is the number the operator asked for, and the room's remaining
            // seats are the bound rather than the target.
            var free = Math.Max(0, capacity - humanCount);
            return configured > 0 ? Math.Min(configured, free) : free;
        }

        return Math.Min(configured, capacity);
    }
}
