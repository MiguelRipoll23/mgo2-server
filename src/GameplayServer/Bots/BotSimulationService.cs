using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Moves, aims and fires the room's bots, and keeps the health their vitals
/// records report.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rate here is the working-server capture's.</b> A bot holds one enemy
/// slot — its state record names it and <c>0xff</c> when it holds none — and
/// alternates between action <c>2</c> and action <c>4</c> at a few hundred
/// milliseconds. A hit's damage word is a hundred-and-twenty-eighth of a health
/// point, and the victim's vitals fall by that much a beat later. Nothing is given
/// a value the capture does not show.
/// </para>
/// <para>
/// <b>A bot walks towards the enemy it holds and stands on its placement when it
/// holds none.</b> The walking itself is <see cref="BotLocomotionService"/>: it
/// owns the body and the route, and this service decides where the route goes and
/// when the body fires.
/// </para>
/// </remarks>
/// <param name="locomotion">Walks and places the bots.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotSimulationService(
    BotLocomotionService locomotion,
    ILogger<BotSimulationService> logger)
{
    /// <summary>
    /// Interval between one bot's shots, in milliseconds. The working host's 362
    /// hits over its 640 s round land on a given victim about five times a minute,
    /// which is one hit every twelve seconds.
    /// </summary>
    public const int ShotIntervalMilliseconds = 12_000;

    /// <summary>Health points one bot hit takes, as the capture's hits measure.</summary>
    public const int ShotDamage = 33;

    /// <summary>Weapon id the capture's bot hits carry, <c>0x18</c>.</summary>
    public const byte ShotWeapon = BotActorRecordUtils.CarriedWeapon;

    /// <summary>
    /// Distance a bot holds its trigger within, in stage units: the working host's
    /// bots write the firing byte at 282 to 6 043 units from the enemy they hold,
    /// a median of 1 414, across the 640 s of its round.
    /// </summary>
    public const float ShotRange = 6000f;

    /// <summary>
    /// Distance a bot keeps from the enemy it holds, in stage units: the reference's
    /// bots close to about 1 400 and stay there, every one of them between 1 233 and
    /// 1 525, a median of 1 414.
    /// </summary>
    public const float EngagementDistance = 1400f;

    /// <summary>How long a dead bot stays down, in milliseconds.</summary>
    public const int RespawnMilliseconds = 8000;

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
                body.TargetSlot = BotBodyState.NoTarget;
                return new BotAdvance(body, false, null);
            }

            locomotion.Respawn(bot, body, map);
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

            // The trigger, not the hit. The working host's bots write the firing
            // byte on about seven beats in ten while they hold an enemy, and their
            // hits land far less often than that, so the byte the client draws a
            // bot firing from is held while the enemy is in range and the damage
            // record keeps the cadence below. Writing it only on the beat a hit
            // landed is a bot that never appears to shoot: the capture of
            // 2026-11-10 21:00 has it set once or twice in a minute.
            firing = Distance(body, target) <= ShotRange;

            if (firing && now >= body.NextShotMilliseconds)
            {
                body.NextShotMilliseconds = now + ShotIntervalMilliseconds;
                shot = new BotShot(
                    target.SessionSlot,
                    ShotDamage,
                    ShotWeapon,
                    target.X,
                    target.Y + BotLocomotionService.AimHeight,
                    target.Z,
                    Bearing(body, target));

                if (target.IsBot)
                {
                    ApplyDamage(target.SessionSlot, ShotDamage, bot.SessionSlot, now);
                }
            }
        }

        var destination = target is null
            ? locomotion.PlacementOf(bot, map, rule)
            : Standoff(body, target);

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
    /// <param name="killerSlot">Slot the kill is credited to, which is the shooter's.</param>
    /// <returns><c>true</c> when the hit named a bot.</returns>
    public bool NoteIncomingDamage(ushort identifier, ushort damage, byte killerSlot)
    {
        if (BotActorRecordUtils.SlotOfDamageIdentifier(identifier) is not { } slot)
        {
            return false;
        }

        var points = damage / BotActorRecordUtils.DamageScale;
        ApplyDamage(slot, Math.Max(points, 1), killerSlot, Environment.TickCount64);
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
    private void ApplyDamage(byte slot, int damage, byte killerSlot, long now)
    {
        var body = Bodies().FirstOrDefault(candidate => candidate.SessionSlot == slot);
        if (body is null || body.IsDead)
        {
            return;
        }

        body.Health = (short)Math.Max(0, body.Health - damage);
        if (body.Health != 0)
        {
            return;
        }

        kills.Enqueue(new BotKill(killerSlot, slot));
        body.RespawnMilliseconds = now + RespawnMilliseconds;
        logger.LogDebug(
            "Bot in slot {BotSlot} went down and stands up in {RespawnMilliseconds} ms",
            slot,
            RespawnMilliseconds);
    }

    /// <summary>Every body the simulation holds.</summary>
    /// <returns>The bodies, which is how a hit by slot is placed on a bot.</returns>
    private IReadOnlyList<BotBodyState> Bodies() => locomotion.Bodies;

    /// <summary>
    /// The enemy this bot holds: the nearest one, which is what keeps it moving as
    /// the room moves. The recorded host's bots change target constantly — each of
    /// its eight names four or five different slots over its round, and its firing
    /// distance shows them closing on each in turn — where a bot bound to one index
    /// of the room stands where it spawned once that index is dead or far away.
    /// </summary>
    /// <param name="bot">The bot choosing.</param>
    /// <param name="body">Its body, which the distance is measured from.</param>
    /// <param name="actors">Every actor in the room.</param>
    /// <returns>The actor it holds, or <c>null</c> when the room holds no enemy.</returns>
    private static BotActorView? ChooseTarget(
        BotParticipant bot,
        BotBodyState body,
        IReadOnlyList<BotActorView> actors)
    {
        if (bot.Team is not { } side)
        {
            return null;
        }

        return actors
            .Where(actor => actor.SessionSlot != bot.SessionSlot && actor.Side != side)
            .OrderBy(actor => Distance(body, actor))
            .ThenBy(actor => actor.SessionSlot)
            .FirstOrDefault();
    }

    /// <summary>
    /// Where a bot walks while it holds an enemy: the point that keeps it at
    /// <see cref="EngagementDistance"/>, or where it already stands when it is that
    /// close already.
    /// </summary>
    /// <param name="body">Its body.</param>
    /// <param name="target">The actor it holds.</param>
    /// <returns>The point it walks to.</returns>
    private static BotPosition Standoff(BotBodyState body, BotActorView target)
    {
        var deltaX = target.X - body.X;
        var deltaZ = target.Z - body.Z;
        var distance = MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
        if (distance <= EngagementDistance)
        {
            return new BotPosition(body.X, body.Y, body.Z);
        }

        var scale = (distance - EngagementDistance) / distance;
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
    private static ushort Bearing(BotBodyState body, BotActorView actor) =>
        (ushort)((long)MathF.Round(
            MathF.Atan2(actor.X - body.X, actor.Z - body.Z) / (2f * MathF.PI) * 65536f) & 0xffff);

    /// <summary>Distance between a body and an actor, in stage units.</summary>
    /// <param name="body">The bot's body.</param>
    /// <param name="actor">The actor.</param>
    /// <returns>The distance.</returns>
    private static float Distance(BotBodyState body, BotActorView actor)
    {
        var deltaX = actor.X - body.X;
        var deltaZ = actor.Z - body.Z;
        return MathF.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }
}
