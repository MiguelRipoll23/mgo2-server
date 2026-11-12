using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Walks a bot's body: it creates the body at its side's spawn, plans the route,
/// and steps it along that route at the recorded walking speed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rate is the working-server capture's.</b> A walking bot's position
/// record moves fifteen wire units — a hundred and fifty stage units — on each
/// 33 ms beat, which is four and a half thousand stage units a second, and the
/// step here is that rate over the time since the body's last tick, so a slow beat
/// walks further rather than walking slower. Measured over the working-server
/// capture's walking runs (t+262.2 to 262.9 of `game-with-bots-workingserver`,
/// fifteen to a beat without a gap); this capture of 2026-11-10 walked at a tenth
/// of it and read as bodies wading.
/// </para>
/// <para>
/// <b>The height is the body's and not the route's</b> — see <see cref="Step"/>.
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
    /// Walking speed, in stage units per second: fifteen wire units — a hundred and
    /// fifty stage units — on each 33 ms beat of the working-server capture, which
    /// is four and a half thousand a second.
    /// </summary>
    public const float WalkUnitsPerSecond = 4500f;

    /// <summary>How often a bot plans its route again, in milliseconds.</summary>
    public const int RouteRefreshMilliseconds = 1500;

    /// <summary>
    /// How long the <c>c1 81</c> position head lasts after a hit, in milliseconds.
    /// </summary>
    /// <remarks>
    /// The working host writes it in runs of six or seven beats — about 200 ms —
    /// after a hit that does not kill (t+258.745 to 258.910 on slot 6, and every
    /// bot at t+289.0), and again as a corpse's fall. This host writes the run and
    /// not the fall, which the corpse body carries.
    /// </remarks>
    public const int HitReactionMilliseconds = 200;

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
        body.ShotThisBeat = false;
        body.HitReacting = false;
        body.HitReactUntilMilliseconds = 0;
        body.StunnedUntilMilliseconds = 0;
        body.PatrolIndex = 0;
    }

    /// <summary>
    /// Marks a body as taking a hit, which is the <c>c1 81</c> head the capture
    /// writes over the beats after one.
    /// </summary>
    /// <param name="body">The body that was hit.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    public static void NoteHit(BotBodyState body, long now)
    {
        if (body.IsDead)
        {
            return;
        }

        body.HitReacting = true;
        body.HitReactUntilMilliseconds = now + HitReactionMilliseconds;
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

            // The height stays the body's own, and no cell's is adopted. The
            // extracted cells are a lattice sampled over the stage's bounding box
            // (`tools/stage-navigation`: `bounds.MinY + (y + 0.5) * step`), so their
            // Y is where the box was divided and not where the floor is: Blood Bath's
            // cells state -1000 while its recorded ground is 800. Taking the cell's Y
            // put every bot of the capture of 2026-11-10 20:49 a thousand units under
            // the map and popped it back to 800 at each respawn — the teleporting the
            // same report names. A route therefore moves a body across the stage and
            // never up or down it, which is the honest reading of this data.
            body.X += deltaX / distance * step;
            body.Z += deltaZ / distance * step;
            body.Yaw = YawTowards(deltaX, deltaZ);
            body.Moving = true;
            remaining -= step;

            if (distance - step <= ArrivalDistance)
            {
                body.RouteIndex++;
            }
        }

        // A bot holding an enemy keeps looking at it; only one with no enemy looks
        // down its own heading, walking or not. Leaving a walking bot's aim where
        // the last fight left it is a bot that stares at a wall down every corridor.
        if (body.TargetSlot == BotBodyState.NoTarget)
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
    public static ushort YawTowards(float deltaX, float deltaZ) =>
        (ushort)((long)MathF.Round(MathF.Atan2(deltaX, deltaZ) / (2f * MathF.PI) * 65536f) & 0xffff);
}
