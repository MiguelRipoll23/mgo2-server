using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Moves, aims and fires the room's bots, and keeps the health their vitals
/// records report.
/// </summary>
/// <remarks>
/// <b>Every rate here is the working-server capture's.</b> A bot walks a recorded
/// track while no enemy is in range, stands and faces the enemy it takes, and
/// writes a <c>0x47</c> fire beat about 1.3 times a second while engaged; a hit's
/// damage word is a hundred-and-twenty-eighth of a health point. The walking
/// belongs to <see cref="BotLocomotionService"/>.
/// </remarks>
/// <param name="locomotion">Walks and places the bots.</param>
/// <param name="destinations">Chooses the point a bot without an enemy patrols to.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotSimulationService(
    BotLocomotionService locomotion,
    BotMovementDestinationService destinations,
    ILogger<BotSimulationService> logger)
{
    /// <summary>
    /// Interval between one bot's hits, counted from the beat the last one landed
    /// on: five seconds, which is the capture's rate once its bots' engagement is
    /// taken out.
    /// </summary>
    /// <remarks>
    /// <b>Measured, not copied.</b> The working capture writes 374 host-written hits
    /// across its 640 s, one every 13.7 s for each of its eight bots; a room of
    /// 2026-11-11 wrote 26 in 123 s, one every 33 s for each of its seven, because a
    /// bot only lands a hit while an enemy is inside
    /// <see cref="ShotRange"/> and that is a little over a third of its round. The
    /// interval that lands at the recorded rate is therefore 13.7 s times that
    /// fraction, five seconds — the twelve this host waited until now was a bot
    /// firing a third as often as the working host's.
    /// </remarks>
    public const int ShotIntervalMilliseconds = 5_000;

    /// <summary>
    /// Health points one bot hit takes: the capture's <c>0x11</c> hits mean a
    /// damage word of 6 400, which is 50 of an actor's 250.
    /// </summary>
    public const int ShotDamage = 50;

    /// <summary>Weapon id the capture's bot hits carry, <c>0x18</c>.</summary>
    public const byte ShotWeapon = BotActorRecordUtils.CarriedWeapon;

    /// <summary>
    /// Distance a bot holds its trigger within: the capture's bots fire at 282 to
    /// 6 043 units.
    /// </summary>
    public const float ShotRange = 6000f;

    /// <summary>
    /// Distance a bot keeps from its enemy: the capture's bots sit between 1 233 and
    /// 1 525.
    /// </summary>
    public const float EngagementDistance = 1400f;

    /// <summary>
    /// How long a dead bot stays down: the capture writes a death's corpse on 240
    /// beats and the vitals come back on the beat it stops.
    /// </summary>
    public const int RespawnMilliseconds = 8000;

    /// <summary>Interval between <c>0x47</c> fire beats while engaged: 1.3 a second.</summary>
    public const int FireBeatIntervalMilliseconds = 750;

    /// <summary>Deaths the room owes the client a kill-feed record for.</summary>
    private readonly Queue<BotKill> kills = new();

    /// <summary>
    /// A bot's body, created at its side's spawn the first time it is asked for.
    /// </summary>
    /// <param name="bot">The bot.</param>
    /// <param name="map">The round's map.</param>
    /// <returns>The body the bot holds.</returns>
    public BotBodyState BodyOf(BotParticipant bot, int map) => locomotion.BodyOf(bot, map);

    /// <summary>
    /// Takes one bot's tick: it picks the enemy it holds, walks towards it, aims
    /// at it and fires when it is in range.
    /// </summary>
    /// <param name="bot">The bot being advanced.</param>
    /// <param name="map">The round's map.</param>
    /// <param name="rule">The round's rule, which picks the placement family.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <param name="actors">Every actor in the room: the bots and the players.</param>
    /// <returns>Its body, whether it fired, and the hit it landed.</returns>
    public BotAdvance Advance(
        BotParticipant bot,
        int map,
        int rule,
        long now,
        IReadOnlyList<BotActorView> actors)
    {
        var body = locomotion.BodyOf(bot, map);

        if (body.IsDead)
        {
            if (now < body.RespawnMilliseconds)
            {
                body.Moving = false;

                // The beat the blow landed on still names the killer, and the beats
                // after it stand. A corpse that named nobody on the beat the client
                // reads its death from is a body nobody was seen to have shot.
                body.TargetSlot = body.DiedThisBeat ? body.KillerSlot : BotBodyState.NoTarget;
                body.DiedThisBeat = false;
                body.HitReacting = false;
                body.ShotThisBeat = false;
                return new BotAdvance(body, false, null);
            }

            locomotion.Respawn(bot, body, map);
        }

        if (body.HitReacting && now >= body.HitReactUntilMilliseconds)
        {
            body.HitReacting = false;
        }

        body.ShotThisBeat = false;

        // A bot's first shot is its own beat of the twelve seconds. A room that
        // arms eight triggers on the same millisecond fires as one volley, which
        // reads as the room shooting once and then nothing; the capture's bots fire
        // apart.
        if (body.NextShotMilliseconds == 0)
        {
            body.NextShotMilliseconds = now + (bot.SessionSlot * 900L);
        }

        var target = ChooseTarget(bot, body, actors);
        BotShot? shot = null;
        var firing = false;
        if (target is null)
        {
            body.TargetSlot = BotBodyState.NoTarget;
        }
        else
        {
            body.TargetSlot = target.SessionSlot;
            body.AimX = target.X;
            body.AimY = target.Y + BotLocomotionService.AimHeight;
            body.AimZ = target.Z;

            // The trigger, not the hit: the capture holds the firing byte on seven
            // beats in ten while an enemy is in range and lands hits far less often.
            firing = BotEngagementUtils.Distance(body, target) <= ShotRange;

            if (firing && now >= body.NextFireBeatMilliseconds)
            {
                body.ShotThisBeat = true;
                body.NextFireBeatMilliseconds = now + FireBeatIntervalMilliseconds;
            }

            if (BotBurstUtils.Ready(body, firing, now))
            {
                // Every round of a burst rides its own fire beat, which is what the
                // recorded `0x47` records beside a run of hits are.
                body.ShotThisBeat = true;

                // The health is read before the hit lands because the blow that
                // takes the last of it is written as the capture's 0x12 record
                // carrying a whole health bar's blow, not an ordinary hit's 50 points
                // (BotActorRecordUtils.FatalDamageSubtype).
                var lethal = target.IsBot
                    && ApplyDamage(target.SessionSlot, ShotDamage, bot.SessionSlot, now);

                shot = new BotShot(
                    target.SessionSlot,
                    ShotDamage,
                    ShotWeapon,
                    target.X,
                    target.Y + BotLocomotionService.AimHeight,
                    target.Z,
                    BotEngagementUtils.Bearing(body, target))
                {
                    Lethal = lethal,
                };
            }
        }

        BotPosition? destination = null;
        if (target is not null && BotEngagementUtils.Distance(body, target) <= EngagementDistance)
        {
            // Close enough to shoot: stand, face it, and keep a route out of it.
            body.Route = [];
            body.Moving = false;
            body.Yaw = BotLocomotionService.YawTowards(target.X - body.X, target.Z - body.Z);
        }
        else
        {
            destination = target is null
                ? Patrol(bot, body, map, rule)
                : BotEngagementUtils.Standoff(body, target);
        }

        locomotion.PlanRoute(bot, body, map, rule, destination, now);
        locomotion.Step(body, now);

        return new BotAdvance(body, firing, shot);
    }

    /// <summary>
    /// Applies a hit a client wrote against a bot.
    /// </summary>
    /// <remarks>
    /// The client's own damage record is the hit: it names the victim's slot in
    /// the identifier and carries the health it takes at <c>+2</c>, a sixteenth of
    /// a point. A bot whose health reaches zero goes down and stands up again
    /// after <see cref="RespawnMilliseconds"/>.
    /// </remarks>
    /// <param name="identifier">Identifier the hit arrived under.</param>
    /// <param name="damage">Damage word the hit carried.</param>
    /// <param name="killerSlot">
    /// Slot the kill is credited to, or <c>null</c> when the roster names no peer: a
    /// zero is announced as the host's own.
    /// </param>
    /// <returns><c>true</c> when the hit named a bot.</returns>
    public bool NoteIncomingDamage(ushort identifier, ushort damage, byte? killerSlot)
    {
        if (BotActorRecordUtils.SlotOfDamageIdentifier(identifier) is not { } slot)
        {
            return false;
        }

        var points = damage / BotActorRecordUtils.DamageScale;

        ApplyDamage(slot, Math.Max(points, 1), killerSlot ?? slot, Environment.TickCount64);
        return true;
    }

    /// <summary>
    /// Records a death the room owes a kill-feed record for, which is what a hit on
    /// a player leaves behind when the health it takes is the last of it.
    /// </summary>
    /// <param name="killerSlot">Slot the kill is credited to.</param>
    /// <param name="victimSlot">Slot that went down.</param>
    public void NoteKill(byte killerSlot, byte victimSlot) =>
        kills.Enqueue(new BotKill(killerSlot, victimSlot));

    /// <summary>
    /// Takes the deaths the room owes a kill-feed record for, oldest first.
    /// </summary>
    /// <returns>One entry per player that went down since the last call.</returns>
    public IReadOnlyList<BotKill> DrainKills()
    {
        var taken = new List<BotKill>();
        while (kills.Count > 0)
        {
            taken.Add(kills.Dequeue());
        }

        return taken;
    }

    /// <summary>Drops every body and owed death, because the room has emptied.</summary>
    public void Reset()
    {
        kills.Clear();
        locomotion.Reset();
    }

    /// <summary>Forgets a bot's body, because the bot has left the room.</summary>
    /// <param name="botName">The bot that left.</param>
    public void Forget(string botName) => locomotion.Forget(botName);

    /// <summary>
    /// Takes health off the bot holding a slot, and puts it down when it runs out.
    /// </summary>
    /// <param name="slot">Slot the hit landed on.</param>
    /// <param name="damage">Health points it takes.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>
    /// <c>true</c> when the hit was the last of the body's health, which the
    /// damage record then carries as the <c>0x12</c> subtype.
    /// </returns>
    private bool ApplyDamage(byte slot, int damage, byte killerSlot, long now)
    {
        var body = Bodies().FirstOrDefault(candidate => candidate.SessionSlot == slot);
        if (body is null || body.IsDead)
        {
            return false;
        }

        body.Health = (short)Math.Max(0, body.Health - damage);
        if (body.Health != 0)
        {
            BotLocomotionService.NoteHit(body, now);
            return false;
        }

        kills.Enqueue(new BotKill(killerSlot, slot));
        body.Deaths++;
        body.RespawnMilliseconds = now + RespawnMilliseconds;

        // The body turns on the actor that put it down, so the record it writes
        // next still names shooter and victim of each other (BotDeathUtils).
        BotDeathUtils.NoteDeath(body, killerSlot, Bodies());
        logger.LogDebug(
            "Bot in slot {BotSlot} went down and stands up in {RespawnMilliseconds} ms",
            slot,
            RespawnMilliseconds);
        return true;
    }

    /// <summary>Every body the simulation holds.</summary>
    /// <returns>The bodies, which is how a hit by slot is placed on a bot.</returns>
    private IReadOnlyList<BotBodyState> Bodies() => locomotion.Bodies;

    /// <summary>
    /// The enemy this bot holds: each of the capture's eight bots names four or
    /// five different slots over its round, so its target is not one fixed index.
    /// </summary>
    /// <param name="bot">The bot choosing.</param>
    /// <param name="body">Its body, which the distance is measured from.</param>
    /// <param name="actors">Every actor in the room.</param>
    /// <returns>The actor it holds, or <c>null</c> when the room holds no enemy.</returns>
    /// <remarks>
    /// <b>The enemy the fewest bots already hold comes first</b> — slot 2 of the
    /// capture holds <c>8</c> for 190 records, <c>5</c> for 133 and <c>1</c> for
    /// 69 — and only an enemy within <see cref="ShotRange"/> is taken at all.
    /// </remarks>
    private BotActorView? ChooseTarget(
        BotParticipant bot,
        BotBodyState body,
        IReadOnlyList<BotActorView> actors)
    {
        if (bot.Team is not { } side)
        {
            return null;
        }

        var claims = Bodies()
            .Where(other => other.SessionSlot != bot.SessionSlot)
            .Select(other => other.TargetSlot)
            .ToList();

        return actors
            .Where(actor => actor.SessionSlot != bot.SessionSlot && actor.Side != side
                && BotEngagementUtils.Distance(body, actor) <= ShotRange)
            .OrderBy(actor => claims.Count(claim => claim == actor.SessionSlot))
            .ThenBy(actor => BotEngagementUtils.Distance(body, actor))
            .ThenBy(actor => (actor.SessionSlot + 24 - bot.SessionSlot) % 24)
            .FirstOrDefault();
    }

    /// <summary>
    /// The point a bot walks to while it holds no enemy: the next point of its
    /// patrol, which it takes when it arrives at the last one.
    /// </summary>
    /// <remarks>
    /// <b>It walks a recorded track rather than its spawn</b> — slot 1 of the
    /// working capture crosses <c>x 20750 → 25160</c> in twenty-one seconds — see
    /// <see cref="BotPatrolRouteUtils"/>.
    /// </remarks>
    /// <param name="bot">The bot walking.</param>
    /// <param name="body">Its body.</param>
    /// <param name="map">Map the round plays.</param>
    /// <param name="rule">The round's rule.</param>
    /// <returns>The point it walks to.</returns>
    private BotPosition Patrol(BotParticipant bot, BotBodyState body, int map, int rule)
    {
        var chosen = destinations.ChoosePatrol(map, bot.SessionSlot, body.PatrolIndex);
        if (chosen.Position is not { } point)
        {
            return locomotion.PlacementOf(bot, map, rule);
        }

        var deltaX = point.X - body.X;
        var deltaZ = point.Z - body.Z;
        if ((deltaX * deltaX) + (deltaZ * deltaZ) > PatrolArrivalDistance * PatrolArrivalDistance)
        {
            return point;
        }

        body.PatrolIndex++;
        return destinations.ChoosePatrol(map, bot.SessionSlot, body.PatrolIndex).Position ?? point;
    }

    /// <summary>Distance at which a patrol point counts as reached, in stage units.</summary>
    private const float PatrolArrivalDistance = 600f;

}
