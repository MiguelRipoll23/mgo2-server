namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The geometry of a bot's engagement: how far its enemy is, where it stands to
/// hold the recorded distance, and the bearing a shot carries.
/// </summary>
/// <remarks>
/// Split out of <see cref="BotSimulationService"/> so neither file exceeds the
/// repository's size limit. Nothing here reads a clock or a body's state; every
/// value is the two positions the caller hands in.
/// </remarks>
public static class BotEngagementUtils
{
    /// <summary>
    /// Where a bot walks while it holds an enemy: the point that keeps it at
    /// <see cref="BotSimulationService.EngagementDistance"/>, or where it already
    /// stands when it is that close already.
    /// </summary>
    /// <param name="body">Its body.</param>
    /// <param name="target">The actor it holds.</param>
    /// <returns>The point it walks to.</returns>
    public static BotPosition Standoff(BotBodyState body, BotActorView target)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(target);

        var deltaX = target.X - body.X;
        var deltaZ = target.Z - body.Z;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
        if (distance <= BotSimulationService.EngagementDistance)
        {
            return new BotPosition(body.X, body.Y, body.Z);
        }

        var scale = (distance - BotSimulationService.EngagementDistance) / distance;
        return new BotPosition(
            body.X + (deltaX * scale),
            target.Y,
            body.Z + (deltaZ * scale));
    }

    /// <summary>
    /// The recorded bearing word from the shooter to its target, which is the
    /// heading the damage record carries.
    /// </summary>
    /// <param name="body">The shooter's body.</param>
    /// <param name="actor">The target.</param>
    /// <returns>The bearing word.</returns>
    public static ushort Bearing(BotBodyState body, BotActorView actor)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(actor);

        return (ushort)((long)MathF.Round(
            MathF.Atan2(actor.X - body.X, actor.Z - body.Z) / (2f * MathF.PI) * 65536f) & 0xffff);
    }

    /// <summary>Distance between a body and an actor, in stage units.</summary>
    /// <param name="body">The bot's body.</param>
    /// <param name="actor">The actor.</param>
    /// <returns>The distance.</returns>
    public static float Distance(BotBodyState body, BotActorView actor)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(actor);

        var deltaX = actor.X - body.X;
        var deltaZ = actor.Z - body.Z;
        return MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }
}
