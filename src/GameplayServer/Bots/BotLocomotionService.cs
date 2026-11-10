using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Walks a bot's body: it creates the body at its side's spawn, plans the route,
/// and steps it along that route at the recorded walking speed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rate is the working-server capture's.</b> A walking bot's position
/// record moves fifteen tenths of a stage unit on each 33 ms beat — forty-five
/// hundredths of a stage unit a millisecond — and the step here is that rate over
/// the time since the body's last tick, so a slow beat walks further rather than
/// walking slower.
/// </para>
/// <para>
/// <b>The heading is the direction of travel.</b> The capture's walking bots face
/// where they are going — a bot moving <c>+15</c> along <c>x</c> and
/// <c>-4</c> along <c>z</c> writes the heading word <c>18818</c>, which is
/// <c>atan2(15, -4)</c> in one turn of 65 536 — so the step turns the body onto
/// the waypoint it walks to.
/// </para>
/// </remarks>
/// <param name="destinations">Chooses the placement a bot walks to.</param>
/// <param name="navigation">Routes a bot across the stage.</param>
public sealed class BotLocomotionService(
    BotMovementDestinationService destinations,
    BotNavigationAgent navigation)
{
    /// <summary>
    /// Walking speed, in stage units per second: fifteen tenths of a unit on each
    /// 33 ms beat of the working-server capture.
    /// </summary>
    public const float WalkUnitsPerSecond = 450f;

    /// <summary>How often a bot plans its route again, in milliseconds.</summary>
    public const int RouteRefreshMilliseconds = 1500;

    /// <summary>Height above an actor's feet that a bot looks at.</summary>
    public const float AimHeight = 200f;

    private const float ArrivalDistance = 200f;

    private readonly Lock gate = new();
    private readonly Dictionary<string, BotBodyState> bodies = [];

    /// <summary>
    /// A bot's body, created at its side's spawn the first time it is asked for.
    /// </summary>
    /// <param name="bot">The bot.</param>
    /// <param name="map">The round's map.</param>
    /// <returns>The body the bot holds.</returns>
    public BotBodyState BodyOf(BotParticipant bot, int map)
    {
        ArgumentNullException.ThrowIfNull(bot);

        lock (gate)
        {
            if (bodies.TryGetValue(bot.Name, out var existing) && existing.SessionSlot == bot.SessionSlot)
            {
                return existing;
            }

            var body = new BotBodyState { SessionSlot = bot.SessionSlot };
            Place(body, bot, map);
            bodies[bot.Name] = body;
            return body;
        }
    }

    /// <summary>
    /// Puts a body on its side's spawn with its vitals whole, which is where a
    /// bot starts the round and where it comes back after a death.
    /// </summary>
    /// <param name="bot">The bot.</param>
    /// <param name="body">Its body.</param>
    /// <param name="map">The round's map.</param>
    public void Respawn(BotParticipant bot, BotBodyState body, int map)
    {
        Place(body, bot, map);
        body.Health = BotBodyState.MaximumVitals;
        body.Stamina = BotBodyState.MaximumVitals;
        body.TargetSlot = BotBodyState.NoTarget;
        body.Route = [];
        body.RouteIndex = 0;
        body.RouteKey = string.Empty;
        body.LastAdvanceMilliseconds = 0;
    }

    /// <summary>Drops every body, because the room has emptied.</summary>
    public void Reset()
    {
        lock (gate)
        {
            bodies.Clear();
        }
    }

    /// <summary>The placement a bot without an enemy walks to.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="map">The round's map.</param>
    /// <param name="rule">The round's rule.</param>
    /// <returns>The placement, or its side's spawn when the stage states none.</returns>
    public BotPosition PlacementOf(BotParticipant bot, int map, int rule)
    {
        ArgumentNullException.ThrowIfNull(bot);

        if (destinations.Choose(map, bot.SessionSlot, rule).Position is { } placement)
        {
            return placement;
        }

        var spawns = MapSpawnUtils.For(map);
        var side = (byte)(bot.Team == 1 ? 1 : 0);
        var facing = spawns.FacingOf(side);
        return new BotPosition(spawns.SpawnOf(side).Along, facing.First, facing.Along);
    }

    /// <summary>
    /// Plans a route when the destination moved or the last one is used up.
    /// </summary>
    /// <param name="bot">The bot.</param>
    /// <param name="body">Its body.</param>
    /// <param name="map">The round's map.</param>
    /// <param name="rule">The round's rule.</param>
    /// <param name="destination">Where it is walking to.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    public void PlanRoute(
        BotParticipant bot,
        BotBodyState body,
        int map,
        int rule,
        BotPosition? destination,
        long now)
    {
        if (destination is null)
        {
            body.Route = [];
            return;
        }

        var key = $"{destination.X:0}:{destination.Z:0}";
        var exhausted = body.RouteIndex >= body.Route.Count;
        if (key == body.RouteKey && !exhausted && now < body.NextRouteMilliseconds)
        {
            return;
        }

        var result = navigation.Navigate(
            bot.Name,
            new BotNavigationRequest(
                map,
                new BotPosition(body.X, body.Y, body.Z),
                destination,
                rule));

        body.Route = result.Path?.Waypoints ?? [];
        body.RouteIndex = 0;
        body.RouteKey = key;
        body.NextRouteMilliseconds = now + RouteRefreshMilliseconds;
    }

    /// <summary>
    /// Walks the body along its route for the time since the last tick.
    /// </summary>
    /// <param name="body">Its body.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    public void Step(BotBodyState body, long now)
    {
        var elapsed = body.LastAdvanceMilliseconds == 0
            ? 0f
            : Math.Clamp((now - body.LastAdvanceMilliseconds) / 1000f, 0f, 0.25f);
        body.LastAdvanceMilliseconds = now;
        body.Moving = false;

        var remaining = WalkUnitsPerSecond * elapsed;
        while (remaining > 0f && body.RouteIndex < body.Route.Count)
        {
            var waypoint = body.Route[body.RouteIndex];
            var deltaX = waypoint.X - body.X;
            var deltaZ = waypoint.Z - body.Z;
            var distance = MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));

            if (distance <= ArrivalDistance)
            {
                body.RouteIndex++;
                continue;
            }

            var step = MathF.Min(remaining, distance);
            body.X += deltaX / distance * step;
            body.Z += deltaZ / distance * step;
            body.Y = waypoint.Y;
            body.Yaw = YawTowards(deltaX, deltaZ);
            body.Moving = true;
            remaining -= step;

            if (distance - step <= ArrivalDistance)
            {
                body.RouteIndex++;
            }
        }

        if (!body.Moving)
        {
            AimForward(body);
        }
    }

    /// <summary>Every body the service holds.</summary>
    /// <returns>The bodies, which is how a hit by slot is placed on a bot.</returns>
    public IReadOnlyList<BotBodyState> Bodies
    {
        get
        {
            lock (gate)
            {
                return [.. bodies.Values];
            }
        }
    }

    /// <summary>Forgets a bot's body, because the bot has left the room.</summary>
    /// <param name="botName">The bot that left.</param>
    public void Forget(string botName)
    {
        lock (gate)
        {
            bodies.Remove(botName);
        }
    }

    /// <summary>Puts a body on its side's spawn, facing that spawn's heading.</summary>
    /// <param name="body">Its body.</param>
    /// <param name="bot">The bot.</param>
    /// <param name="map">The round's map.</param>
    private static void Place(BotBodyState body, BotParticipant bot, int map)
    {
        var spawns = MapSpawnUtils.For(map);
        var side = (byte)(bot.Team == 1 ? 1 : 0);
        var spawn = spawns.SpawnOf(side);
        var facing = spawns.FacingOf(side);

        body.X = spawn.Along;
        body.Y = facing.First;
        body.Z = facing.Along;
        body.Yaw = (ushort)spawn.First;
        AimForward(body);
    }

    /// <summary>
    /// Puts the aim point straight ahead of the body, which is where a bot with no
    /// enemy looks.
    /// </summary>
    /// <param name="body">Its body.</param>
    private static void AimForward(BotBodyState body)
    {
        var radians = body.Yaw * 2f * MathF.PI / 65536f;
        body.AimX = body.X + (MathF.Sin(radians) * 2000f);
        body.AimY = body.Y + AimHeight;
        body.AimZ = body.Z + (MathF.Cos(radians) * 2000f);
    }

    /// <summary>
    /// The recorded heading word for a direction: one turn of 65 536, from the
    /// acting bot's own heading against its movement.
    /// </summary>
    /// <param name="deltaX">Direction along <c>x</c>.</param>
    /// <param name="deltaZ">Direction along <c>z</c>.</param>
    /// <returns>The heading word.</returns>
    private static ushort YawTowards(float deltaX, float deltaZ) =>
        (ushort)((long)MathF.Round(MathF.Atan2(deltaX, deltaZ) / (2f * MathF.PI) * 65536f) & 0xffff);
}
