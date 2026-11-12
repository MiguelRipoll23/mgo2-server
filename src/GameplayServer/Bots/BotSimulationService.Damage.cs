using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The damage half of the bot simulation: what a blow takes off a bot, what a
/// stun does instead, and the death the room owes a kill-feed record for.
/// </summary>
/// <remarks>
/// Split out of <c>BotSimulationService.cs</c> to keep both files inside the
/// repository's size limit; the tick that aims and fires is there.
/// </remarks>
public sealed partial class BotSimulationService
{
    /// <summary>
    /// How long a stunned bot stays down, in milliseconds.
    /// </summary>
    /// <remarks>
    /// <b>Not measured, and stated as such.</b> The stun's own record says what it
    /// costs and not how long it lasts: the capture's <c>42</c> at t+221.45 spends
    /// the whole bar in its damage word and the victim's vitals never move, and the
    /// beats after it do not carry the recovery — a knocked-out body still stands in
    /// the actor block, and there is no record that says "up". Three seconds is the
    /// port's own knockdown, and it is the one number in this path that a capture
    /// here does not state.
    /// </remarks>
    public const int StunMilliseconds = 3_000;

    /// <summary>
    /// Takes a hit a client wrote against a bot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A hit is applied whether or not the room can name the shooter.</b> The
    /// blow's health is the victim's business; only the credit needs a name. A host
    /// that dropped the whole record when its roster had no entry for the reporting
    /// endpoint left the bot at full health through every ordinary shot, so the only
    /// hit that ever put a body down was the one whose damage word is the whole bar
    /// — the headshot — which is exactly the report this fixes.
    /// </para>
    /// <para>
    /// <b>A stun takes no health.</b> The record spends the victim's whole stun bar
    /// in the same word a headshot uses (<see cref="BotHitUtils.StunSubtype"/>), so a
    /// host reading it as damage killed the actor it had just been told was knocked
    /// out, and announced a kill for a stun.
    /// </para>
    /// </remarks>
    /// <param name="hit">The hit as decoded.</param>
    /// <param name="shooterSlot">
    /// Slot the kill is credited to, or <c>null</c> when the roster names no peer for
    /// the record. A death nobody is named for is counted and not credited.
    /// </param>
    /// <returns><c>true</c> when the hit named one of the room's bots.</returns>
    public bool NoteIncomingDamage(BotHit hit, byte? shooterSlot)
    {
        ArgumentNullException.ThrowIfNull(hit);

        if (Bodies().FirstOrDefault(candidate => candidate.SessionSlot == hit.VictimSlot) is not { } body)
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (hit.IsStun)
        {
            Stun(body, now);
            return true;
        }

        // Slot zero is this host's own seat, which is not a player of the round: a
        // hit credited to it would put the kill on server-99999's row.
        var killer = shooterSlot is > 0 ? shooterSlot.Value : (byte)0;
        ApplyDamage(body, hit.Damage, killer, hit.TakesWholeBar, hit.IsHeadshot, now);
        return true;
    }

    /// <summary>
    /// Knocks a bot out: it keeps its feet where the blow put it, holds no target
    /// and pulls no trigger until the stun runs out, and its health does not move.
    /// </summary>
    /// <param name="body">The body the blow landed on.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    public void Stun(BotBodyState body, long now)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.IsDead)
        {
            return;
        }

        body.StunnedUntilMilliseconds = now + StunMilliseconds;
        BotLocomotionService.NoteHit(body, now);

        // The stun holds the `c1 81` head for as long as it lasts, rather than the
        // two hundred milliseconds a wound does: a stunned body is the one the client
        // draws falling.
        body.HitReacting = true;
        body.HitReactUntilMilliseconds = body.StunnedUntilMilliseconds;
        body.BurstRemaining = 0;
        logger.LogDebug(
            "Bot in slot {BotSlot} was stunned and gets up in {StunMilliseconds} ms",
            body.SessionSlot,
            StunMilliseconds);
    }

    /// <summary>
    /// How long a grab holds a body after the last record that states it, in
    /// milliseconds.
    /// </summary>
    /// <remarks>
    /// <b>The interval is inferred; the hold is measured.</b> The grabber's client
    /// writes its hold every beat for as long as it holds (`CqcHoldUtils`), so this
    /// only has to survive a dropped record: three seconds is the stun's own length
    /// and far longer than a hold, and the reference's hold runs about two.
    /// </remarks>
    public const int HeldMilliseconds = 600;

    /// <summary>
    /// How long a corpse keeps naming the slot that put it down, in milliseconds.
    /// </summary>
    /// <remarks>
    /// The feed rides the death's own beat (`FeedDelayMilliseconds` is zero), and
    /// the working host names the killer across the whole second around it: its
    /// victim slot 13 writes <c>44 0b 01 …</c> from t+152.78 until the
    /// <c>09 0b 0d</c> feed at t+153.85. One second spans that with room for a
    /// dropped beat, and is short enough that the body stands again long before its
    /// <see cref="RespawnMilliseconds"/> respawn.
    /// </remarks>
    public const int KillerNamedMilliseconds = 1_000;

    /// <summary>
    /// Holds a bot in a client's grab: it stands, holds no target and pulls no
    /// trigger until the grab's records stop coming.
    /// </summary>
    /// <remarks>
    /// <b>The host's whole part in a grab is to stop writing the body out of it.</b>
    /// The hold is the grabber's client's — the reference's host sends nothing that
    /// gates one — so a simulated bot that kept aiming, firing and walking was a
    /// target whose client-side grab check could never settle, which is the grab this
    /// server refused.
    /// </remarks>
    /// <param name="slot">Slot the client's hold names as the actor it holds.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when a living bot holds the slot.</returns>
    /// <param name="slot">Slot the client's hold names as the actor it holds.</param>
    /// <param name="holder">Slot of the client that wrote the hold, which the body turns on.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when a living bot holds the slot.</returns>
    public bool HoldSlot(byte slot, byte holder, long now)
    {
        if (Bodies().FirstOrDefault(candidate => candidate.SessionSlot == slot) is not { } body
            || body.IsDead)
        {
            return false;
        }

        body.HeldUntilMilliseconds = Math.Max(body.HeldUntilMilliseconds, now + HeldMilliseconds);
        body.HeldBySlot = holder;
        return true;
    }

    /// <summary>
    /// Records a death the room owes a kill-feed record for, which is what a hit on
    /// a player leaves behind when the health it takes is the last of it.
    /// </summary>
    /// <param name="killerSlot">Slot the kill is credited to, or zero for nobody.</param>
    /// <param name="victimSlot">Slot that went down.</param>
    /// <param name="headshot">Whether the blow that ended it was a headshot.</param>
    public void NoteKill(byte killerSlot, byte victimSlot, bool headshot = false) =>
        kills.Enqueue(new BotKill(killerSlot, victimSlot, headshot));

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

    /// <summary>
    /// Takes health off a bot, and puts it down when it runs out.
    /// </summary>
    /// <param name="body">The body the hit landed on.</param>
    /// <param name="damage">Health points it takes.</param>
    /// <param name="killerSlot">Slot the death is credited to, or zero for nobody.</param>
    /// <param name="takesWholeBar">Whether the blow ends the body whatever it held.</param>
    /// <param name="headshot">Whether the blow is a headshot.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when the hit was the last of the body's health.</returns>
    private bool ApplyDamage(
        BotBodyState body,
        int damage,
        byte killerSlot,
        bool takesWholeBar,
        bool headshot,
        long now)
    {
        if (body.IsDead)
        {
            return false;
        }

        body.Health = takesWholeBar ? (short)0 : (short)Math.Max(0, body.Health - damage);
        if (body.Health != 0)
        {
            BotLocomotionService.NoteHit(body, now);
            return false;
        }

        // A body that dies in someone's grab is nobody's held actor any more: its
        // corpse record must not keep the hold's aim form, which names no target.
        body.HeldUntilMilliseconds = 0;
        body.HeldBySlot = BotBodyState.NoTarget;

        kills.Enqueue(new BotKill(killerSlot, body.SessionSlot, headshot));
        body.Deaths++;
        body.RespawnMilliseconds = now + RespawnMilliseconds;
        body.StunnedUntilMilliseconds = 0;

        // The body turns on the actor that put it down, so the record it writes
        // next still names shooter and victim of each other (BotDeathUtils).
        BotDeathUtils.NoteDeath(body, killerSlot, Bodies());

        // The shooter keeps naming the body it put down for as long as the victim
        // does: every `09 <killer> <victim>` in the working capture lands with the
        // killer's state still on the victim, and a killer that re-targets within
        // the feed's delay leaves the client no record naming both actors.
        if (Bodies().FirstOrDefault(other => other.SessionSlot == killerSlot) is { } shooter)
        {
            shooter.KillAimSlot = body.SessionSlot;
            shooter.KillAimUntilMilliseconds = now + KillerNamedMilliseconds;
            shooter.TargetSlot = body.SessionSlot;
            shooter.AimX = body.X;
            shooter.AimY = body.Y + BotLocomotionService.AimHeight;
            shooter.AimZ = body.Z;
        }
        logger.LogDebug(
            "Bot in slot {BotSlot} went down and stands up in {RespawnMilliseconds} ms",
            body.SessionSlot,
            RespawnMilliseconds);
        return true;
    }

    /// <summary>Every body the simulation holds.</summary>
    /// <returns>The bodies, which is how a hit by slot is placed on a bot.</returns>
    private IReadOnlyList<BotBodyState> Bodies() => locomotion.Bodies;
}
