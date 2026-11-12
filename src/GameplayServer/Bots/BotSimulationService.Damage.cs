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

        kills.Enqueue(new BotKill(killerSlot, body.SessionSlot, headshot));
        body.Deaths++;
        body.RespawnMilliseconds = now + RespawnMilliseconds;
        body.StunnedUntilMilliseconds = 0;

        // The body turns on the actor that put it down, so the record it writes
        // next still names shooter and victim of each other (BotDeathUtils).
        BotDeathUtils.NoteDeath(body, killerSlot, Bodies());
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
