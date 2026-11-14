using Mgo2Server.GameplayServer.Combat;
using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The player half of the actor writer's combat: the hit a bot lands on a player,
/// the vitals that hit leaves, the respawn it earns, and the question every hit
/// asks first — whether the actor it names is still standing.
/// </summary>
/// <remarks>
/// <b>A player's health is the host's, and a bot has no client to report it.</b>
/// The reference writes slot 8's vitals falling <c>250 → 217 → 183 → 127 → 93 →
/// 37 → 0</c> and then a standing <c>250</c> eight seconds later, so a hit is a
/// damage record on the victim and that player's vitals at the health it now
/// holds. A player at zero is not a target and not a victim: the working capture
/// holds seven of the client's own hits on a corpse and not one of them comes
/// back.
/// </remarks>
public sealed partial class BotActorPublishService
{
    /// <summary>Health each player holds, by slot. Zero is a corpse.</summary>
    private readonly Dictionary<byte, short> humanHealth = [];

    /// <summary>When each dead player stands up again, by slot.</summary>
    private readonly Dictionary<byte, long> humanRespawn = [];

    /// <summary>
    /// The records one bot hit on a player writes: its damage, then the player's
    /// vitals at the health it now holds.
    /// </summary>
    /// <param name="shot">The hit.</param>
    /// <param name="killerSlot">The bot that landed it, which the death is credited to.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>The damage record and the vitals record.</returns>
    private IReadOnlyList<UdpMessage> PlayerHit(
        BotShot shot,
        byte killerSlot,
        MatchRecordGenerationUtils generation,
        long now)
    {
        if (shot.Kind is BotWeaponKind.StunDart or BotWeaponKind.StunKnife)
        {
            return PlayerStun(shot, killerSlot, generation, now);
        }

        if (!TakePlayerHealth(
                shot.VictimSlot,
                shot.Damage,
                shot.Kind is BotWeaponKind.LethalKnife || shot.Headshot,
                killerSlot,
                shot.Headshot,
                now,
                out var health))
        {
            return [];
        }

        var vitalsRecord = new UdpMessage(
            PlayerVitalsFramesUtils.IdentifierFor(generation, shot.VictimSlot),
            3,
            BotActorRecordUtils.VitalsAttributeClass,
            [(byte)health, (byte)ActorStaminaUtils.MaximumStamina]);
        QueueVitalsRepeats(vitalsRecord, now);

        return
        [
            BotActorRecordUtils.BuildShot(
                shot.VictimSlot,
                killerSlot,
                generation,
                shot.Damage,
                shot.Weapon,
                shot.ImpactX,
                shot.ImpactY,
                shot.ImpactZ,
                shot.Bearing,
                shot.Headshot,
                shot.Pitch,
                shot.Kind),
            vitalsRecord,
        ];
    }

    /// <summary>
    /// Applies a hit a client wrote against a player, and reports whether the
    /// record named one this host holds the health of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A hit is a hit whichever side its victim stands on.</b> The working
    /// host's dump of one round (<c>.pcapngs/actions/actions.pcapng</c>) carries no
    /// "teammate" record at all: the client's hits name slots 1 to 15, and the only
    /// thing that says whether the victim was a friend is the side the room puts it
    /// on. So the health is taken here for a teammate exactly as for an enemy — a
    /// host that applied only a bot's own hits left friendly fire with no victim,
    /// and the player's bar the only thing that moved.
    /// </para>
    /// <para>
    /// <b>A blast's damage word names its shooter too.</b> The 28-byte record's word
    /// packs points and slot the same way a bullet's does, including for a claymore
    /// or C4 the victim's own client reported: the low five bits name the player who
    /// placed it. Only a word that encodes no slot falls back to the reporter, and a
    /// blast that lands on that slot is then a suicide — the kill-feed record names
    /// the same slot twice, which the replay parser reads as a death and not a kill
    /// (<see cref="MatchScoreService.NoteKill"/>).
    /// </para>
    /// </remarks>
    /// <param name="hit">The hit as decoded.</param>
    /// <param name="shooterSlot">
    /// Slot the damage word names, or the reporting peer when it names none.
    /// </param>
    /// <returns><c>true</c> when the victim is a player this host holds the health of.</returns>
    public bool NoteClientHit(DamageHit hit, byte? shooterSlot)
    {
        ArgumentNullException.ThrowIfNull(hit);

        if (IsBotSlot(hit.VictimSlot) || HumanEndpoint(hit.VictimSlot) is null)
        {
            return false;
        }

        var now = Environment.TickCount64;

        // A stun, a CQC impact and a sleeping device's blast spend the victim's
        // stamina and take no health, so nothing is counted here: the round's own
        // stats count a knock-out, and the client draws the fall. Read as damage,
        // each of them put the teammate it was aimed at down as if it had been shot.
        if (hit.IsStun)
        {
            return StunPlayer(hit, shooterSlot, now);
        }
        if (!TakePlayerHealth(
                hit.VictimSlot,
                hit.Damage,
                hit.TakesWholeBar,
                shooterSlot,
                hit.IsHeadshot,
                now,
                out var health))
        {
            return false;
        }

        QueueVitals(
            new UdpMessage(
                PlayerVitalsFramesUtils.IdentifierFor(roundState.Generation, hit.VictimSlot),
                3,
                BotActorRecordUtils.VitalsAttributeClass,
                [(byte)health, (byte)ActorStaminaUtils.MaximumStamina]),
            now);
        return true;
    }

    /// <summary>
    /// Takes a blow off a player's health and, when it is the last of it, puts the
    /// player down and hands the death to the score.
    /// </summary>
    /// <remarks>
    /// <b>The working host writes nothing on a slot between its zero and its two
    /// hundred and fifty eight seconds later.</b> A player at zero has already been
    /// put down: another hit must not restate its health from the maximum, nor push
    /// its respawn back, or it never stands up.
    /// </remarks>
    /// <param name="slot">The player's slot.</param>
    /// <param name="damage">Health points the blow takes.</param>
    /// <param name="takesWholeBar">Whether the blow ends the player whatever it held.</param>
    /// <param name="killerSlot">Slot the kill is credited to, or <c>null</c> for nobody.</param>
    /// <param name="headshot">Whether the blow is a headshot.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <param name="health">The health the blow leaves, when it landed.</param>
    /// <returns><c>true</c> when the blow landed on a player still standing.</returns>
    private bool TakePlayerHealth(
        byte slot,
        int damage,
        bool takesWholeBar,
        byte? killerSlot,
        bool headshot,
        long now,
        out short health)
    {
        health = 0;
        lock (combatGate)
        {
            if (!humanHealth.TryGetValue(slot, out var held))
            {
                held = ActorHealthUtils.MaximumHealth;
            }

            if (ActorHealthUtils.IsFatal(held))
            {
                return false;
            }

            var change = ActorHealthUtils.Apply(held, damage, takesWholeBar);
            health = change.Health;
            humanHealth[slot] = health;

            // The beat writes the peer's vitals record under this player's own
            // actor identifier, so the value held there is what the client's bar
            // reads. A constant full health on that record is a bar that never
            // moves, which is the room of 2026-11-11.
            if (HumanEndpoint(slot) is { } hitEndpoint)
            {
                vitals.NoteDamage(
                    hitEndpoint,
                    new PlayerVitals(health, ActorStaminaUtils.MaximumStamina));
            }

            if (!change.Fatal)
            {
                return true;
            }

            humanRespawn[slot] = now + BotSimulationService.RespawnMilliseconds;
            ClearPlayerStun(slot);

            // The reference announces the player's own death on the kill feed too
            // (`09 02 08` at t+311.03, slot 8's health reaching zero at 310.81).
            // A death with nobody to credit is still a death; a death credited to
            // this host's own seat is a kill the room cannot resolve.
            var killer = ActorKillUtils.CreditSlot(killerSlot);
            if (killer != ActorKillUtils.NoKiller)
            {
                simulation.NoteKill(killer, slot, headshot);
            }
            else
            {
                score.NoteDeath(slot);
            }

            return true;
        }
    }

    /// <summary>
    /// Queues a vitals record and the copies the working host writes behind it.
    /// </summary>
    /// <param name="record">The record to write.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    private void QueueVitals(UdpMessage record, long now)
    {
        pendingVitals.Add((now, record));
        QueueVitalsRepeats(record, now);
    }

    /// <summary>
    /// How many times one change of a player's health is written: four, which is
    /// what the working host writes for its own player.
    /// </summary>
    /// <remarks>
    /// <b>Measured on <c>game-with-bots-workingserver.pcapng</c>.</b> Slot 8's
    /// vitals record falls to <c>68</c> (104) at t+310.608 and is written again at
    /// 310.628, 310.645 and 310.660; the fatal blow at 310.810 zeroes it and the
    /// zero is written at 310.810, 310.827, 310.844 and 310.862. A single record is
    /// what this host wrote, and a client whose bar is driven by the record it
    /// reads is given one beat of a change it was never told again.
    /// </remarks>
    private const int VitalsRepeatCount = 4;

    /// <summary>Interval between two copies of that record, in milliseconds.</summary>
    private const int VitalsRepeatIntervalMilliseconds = 17;

    /// <summary>Copies of a player's vitals record still to be written, and when.</summary>
    private readonly List<(long Due, UdpMessage Record)> pendingVitals = [];

    /// <summary>
    /// Queues the three copies that follow the one a hit writes on its own beat.
    /// </summary>
    /// <param name="record">The vitals record the hit carries.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    private void QueueVitalsRepeats(UdpMessage record, long now)
    {
        for (var copy = 1; copy < VitalsRepeatCount; copy++)
        {
            pendingVitals.Add((now + copy * VitalsRepeatIntervalMilliseconds, record));
        }
    }

    /// <summary>The copies of a vitals record whose beat has come.</summary>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>The records to write in this beat, oldest first.</returns>
    private IReadOnlyList<UdpMessage> DrainVitalsRepeats(long now)
    {
        if (pendingVitals.Count == 0)
        {
            return [];
        }

        var due = pendingVitals
            .Where(entry => entry.Due <= now)
            .Select(entry => entry.Record)
            .ToList();
        if (due.Count > 0)
        {
            pendingVitals.RemoveAll(entry => entry.Due <= now);
        }

        return due;
    }

    /// <summary>
    /// The vitals of every player whose health has come back after a death.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>One vitals record per player standing up.</returns>
    private IReadOnlyList<UdpMessage> PlayerRespawns(MatchRecordGenerationUtils generation, long now)
    {
        var records = new List<UdpMessage>();
        lock (combatGate)
        {
            foreach (var slot in humanRespawn.Keys.ToList())
            {
                if (now < humanRespawn[slot])
                {
                    continue;
                }

                humanRespawn.Remove(slot);
                humanHealth[slot] = ActorHealthUtils.MaximumHealth;
                ClearPlayerStun(slot);
                if (HumanEndpoint(slot) is { } respawnEndpoint)
                {
                    vitals.Restore(respawnEndpoint);
                }
                records.Add(new UdpMessage(
                    PlayerVitalsFramesUtils.IdentifierFor(generation, slot),
                    3,
                    BotActorRecordUtils.VitalsAttributeClass,
                    [(byte)ActorHealthUtils.MaximumHealth, (byte)ActorStaminaUtils.MaximumStamina]));
            }
        }

        return records;
    }

    /// <summary>
    /// Whether the actor holding a slot is down: a player whose health the beat
    /// holds at zero, or a bot whose body is.
    /// </summary>
    /// <param name="slot">Slot to test.</param>
    /// <returns><c>true</c> when the slot is a corpse.</returns>
    private bool IsVictimDown(byte slot)
    {
        lock (combatGate)
        {
            return IsVictimDownLocked(slot);
        }
    }

    /// <summary>
    /// The same question, for a caller that already holds
    /// <see cref="combatGate"/>.
    /// </summary>
    /// <param name="slot">Slot to test.</param>
    /// <returns><c>true</c> when the slot is a corpse.</returns>
    private bool IsVictimDownLocked(byte slot)
    {
        if (humanHealth.TryGetValue(slot, out var health) && ActorHealthUtils.IsFatal(health))
        {
            return true;
        }

        var map = roundState.RoundSettings?.Map ?? matchSettings.Map;
        return bots.Bots.Any(bot =>
        {
            if (bot.SessionSlot != slot)
            {
                return false;
            }

            return simulation.BodyOf(bot, map).IsDead;
        });
    }

    /// <summary>
    /// The endpoint of the player holding a slot, which is how the vitals service
    /// files a player.
    /// </summary>
    /// <param name="slot">Slot to name.</param>
    /// <returns>The endpoint, or <c>null</c> when no player holds the slot.</returns>
    private string? HumanEndpoint(byte slot) =>
        roster.Snapshot()
            .FirstOrDefault(member => !member.IsBot && member.SessionSlot == slot)
            ?.RemoteAddress;
}
