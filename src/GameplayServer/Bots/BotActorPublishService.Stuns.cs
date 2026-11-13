using Mgo2Server.GameplayServer.Combat;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The player half of the knock-out family: a stun, a CQC impact or a sleeping
/// device takes a player's stamina and not their health.
/// </summary>
/// <remarks>
/// <b>The client draws its own fall; the host keeps the state that says who is
/// down and for how long.</b> A bot's stun lives on <see cref="BotBodyState"/> and
/// stops its aim and trigger; a player has no simulated body, so the same interval
/// is kept here by slot. Reading a stun as health damage is what put the victim
/// down as if it had been shot, which is the stun that did not work.
/// </remarks>
public sealed partial class BotActorPublishService
{
    /// <summary>When each player's knock-out ends, in monotonic milliseconds.</summary>
    private readonly Dictionary<byte, long> humanStunnedUntil = [];

    /// <summary>Slot that knocked each stunned player out, or zero when nobody is named.</summary>
    private readonly Dictionary<byte, byte> humanStunnedBy = [];

    /// <summary>Stamina each player holds after a knock-out, by slot.</summary>
    private readonly Dictionary<byte, short> humanStamina = [];

    /// <summary>When each player's stamina was last ticked, in monotonic milliseconds.</summary>
    private readonly Dictionary<byte, long> humanStaminaTickedAt = [];

    /// <summary>Fractional stamina recovery carried between ticks, in point-milliseconds.</summary>
    private readonly Dictionary<byte, long> humanStaminaCredit = [];

    /// <summary>When each recovering player's vitals may be written again.</summary>
    private readonly Dictionary<byte, long> humanStaminaDue = [];

    /// <summary>When each sleeping player stands up, in monotonic milliseconds.</summary>
    private readonly Dictionary<byte, long> humanSleepUntil = [];

    /// <summary>
    /// Knocks a player out without taking health, and reports whether the record
    /// named a living player.
    /// </summary>
    /// <param name="slot">Player the blow landed on.</param>
    /// <param name="shooterSlot">Slot the blow is credited to, or <c>null</c> for nobody.</param>
    /// <param name="points">Stamina points the blow spends.</param>
    /// <param name="kind">Which knock-out blow it was.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when a living player holds the slot.</returns>
    public bool StunPlayer(
        byte slot,
        byte? shooterSlot,
        int points,
        DamageKind kind,
        long now)
    {
        if (HumanEndpoint(slot) is null)
        {
            return false;
        }

        lock (combatGate)
        {
            if (humanHealth.TryGetValue(slot, out var held) && held <= 0)
            {
                return false;
            }

            var health = humanHealth.GetValueOrDefault(slot, BotBodyState.MaximumVitals);
            var stamina = (short)Math.Clamp(
                humanStamina.GetValueOrDefault(slot, BotBodyState.MaximumVitals) - points,
                0,
                BotBodyState.MaximumVitals);
            humanStamina[slot] = stamina;
            humanStaminaTickedAt[slot] = now;
            humanStaminaCredit[slot] = 0;
            humanStaminaDue[slot] = now + 1000;

            // Only the CQC family lays an emptied bar on the floor. A tranquilliser's
            // 250 points at `actions.pcapng` t+221.45 write no lying body, and the
            // capture's two 8-second sleeps follow CQC blows.
            if (stamina == 0 && kind is DamageKind.Knockout or DamageKind.ExplosiveStun)
            {
                humanSleepUntil[slot] = Math.Max(
                    humanSleepUntil.GetValueOrDefault(slot),
                    now + BotSimulationService.SleepMilliseconds);
            }

            var until = now + BotSimulationService.StunMilliseconds;
            humanStunnedUntil[slot] = Math.Max(
                humanStunnedUntil.GetValueOrDefault(slot),
                Math.Max(until, humanSleepUntil.GetValueOrDefault(slot)));
            humanStunnedBy[slot] = shooterSlot ?? 0;

            // The stun bar is stamina, and the damage word is the part of it the
            // blow spends. The reference's body hit (0x41) spends 36 of 250 and its
            // head hit (0x42) spends the whole bar; a vitals record that always
            // wrote 250 is the bar this report says never moved.
            QueueVitals(
                new UdpMessage(
                    PlayerVitalsFramesUtils.IdentifierFor(roundState.Generation, slot),
                    3,
                    BotActorRecordUtils.VitalsAttributeClass,
                    [(byte)health, (byte)stamina]),
                now);
        }

        logger.LogDebug(
            "The player in slot {VictimSlot} was knocked out with {Kind} by slot {ShooterSlot} and gets up in {StunMilliseconds} ms",
            slot,
            kind,
            shooterSlot,
            BotSimulationService.StunMilliseconds);
        return true;
    }

    /// <summary>
    /// Whether a player is still in the knock-out a blow put them in.
    /// </summary>
    /// <param name="slot">Player to test.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns><c>true</c> when the stun has not run out.</returns>
    public bool IsPlayerStunned(byte slot, long now)
    {
        lock (combatGate)
        {
            return humanStunnedUntil.TryGetValue(slot, out var until) && until > now;
        }
    }

    /// <summary>
    /// Refills every stunned player's stamina at the measured rate, and writes
    /// the vitals record once a second while it climbs.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>One vitals record per player whose bar is due.</returns>
    public IReadOnlyList<UdpMessage> RecoverPlayerStamina(
        MatchRecordGenerationUtils generation,
        long now)
    {
        var records = new List<UdpMessage>();
        lock (combatGate)
        {
            foreach (var slot in humanStamina.Keys.ToList())
            {
                var stamina = humanStamina[slot];
                if (stamina >= BotBodyState.MaximumVitals)
                {
                    humanStamina.Remove(slot);
                    humanStaminaTickedAt.Remove(slot);
                    humanStaminaCredit.Remove(slot);
                    humanStaminaDue.Remove(slot);
                    continue;
                }

                var tickedAt = humanStaminaTickedAt.GetValueOrDefault(slot, now);
                var elapsed = now - tickedAt;
                if (elapsed > 0)
                {
                    humanStaminaTickedAt[slot] = now;
                    humanStaminaCredit[slot] += elapsed * BotSimulationService.StaminaRecoveryPerSecond;
                    var recovered = (int)(humanStaminaCredit[slot] / 1000);
                    if (recovered > 0)
                    {
                        humanStaminaCredit[slot] -= recovered * 1000L;
                        stamina = (short)Math.Min(
                            BotBodyState.MaximumVitals,
                            stamina + recovered);
                        humanStamina[slot] = stamina;
                    }
                }

                if (now < humanStaminaDue.GetValueOrDefault(slot))
                {
                    continue;
                }

                humanStaminaDue[slot] = now + 1000;
                records.Add(new UdpMessage(
                    PlayerVitalsFramesUtils.IdentifierFor(generation, slot),
                    3,
                    BotActorRecordUtils.VitalsAttributeClass,
                    [
                        (byte)humanHealth.GetValueOrDefault(slot, BotBodyState.MaximumVitals),
                        (byte)stamina,
                    ]));
            }
        }

        return records;
    }

    /// <summary>Ends a player's knock-out, because the player died or stood up.</summary>
    /// <param name="slot">Player whose state is cleared.</param>
    private void ClearPlayerStun(byte slot)
    {
        lock (combatGate)
        {
            humanStunnedUntil.Remove(slot);
            humanStunnedBy.Remove(slot);
            humanStamina.Remove(slot);
            humanStaminaTickedAt.Remove(slot);
            humanStaminaCredit.Remove(slot);
            humanStaminaDue.Remove(slot);
            humanSleepUntil.Remove(slot);
        }
    }
}
