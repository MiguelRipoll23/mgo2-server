using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The combat half of the actor writer: it advances every bot, builds its block
/// from the body the tick leaves, and writes the hits the bots land.
/// </summary>
/// <remarks>
/// <para>
/// <b>A hit on a player is two records, and both are the capture's.</b> The
/// working-server host writes the damage record three below that player's state
/// record and then the player's vitals at the health it now holds — slot 8 falls
/// <c>250</c>, <c>197</c>, <c>141</c> as the bots hit it. A hit on another bot is
/// applied to that bot's body instead, and reaches the client through that bot's
/// own vitals record on the next beat.
/// </para>
/// <para>
/// <b>The player's position is the one the client reported</b>
/// (<see cref="PlayerPositionService"/>), which is what lets a bot walk towards
/// the player rather than at its own spawn, and the sides are the room's own
/// reading (<see cref="LobbySlotService"/>) so a bot does not shoot its teammate.
/// </para>
/// </remarks>
public sealed partial class BotActorPublishService
{
    private readonly Dictionary<byte, short> humanSides = [];
    private readonly Dictionary<byte, short> humanHealth = [];
    private readonly Dictionary<byte, long> humanRespawn = [];
    private readonly Lock combatGate = new();
    private long nextSideRead;

    /// <summary>Ordinal the round's state blob carries, which climbs with the beat.</summary>
    private byte stateBlobOrdinal = 2;

    /// <summary>When the round's state blob is written again, in monotonic milliseconds.</summary>
    private long nextStateBlobMilliseconds;

    /// <summary>
    /// Interval between the round's state blobs: the working host writes 522 of
    /// them in the ten minutes of its round, a little more than one a second.
    /// </summary>
    private const int StateBlobIntervalMilliseconds = 1200;

    /// <summary>Drops the combat state and its ordinals, because the room has emptied.</summary>
    public void Reset()
    {
        lock (combatGate)
        {
            humanSides.Clear();
            humanHealth.Clear();
            humanRespawn.Clear();
            nextSideRead = 0;
            stateBlobOrdinal = 2;
            nextStateBlobMilliseconds = 0;
        }
    }

    /// <summary>
    /// Advances every bot and returns the records of this beat: each bot's block,
    /// the hit it landed on a player, and any player whose vitals have come back.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="map">Map the round plays.</param>
    /// <param name="rule">The round's rule.</param>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>The records to write, in the order the capture writes them.</returns>
    private async Task<List<UdpMessage>> BuildActorRecordsAsync(
        PeerContext context,
        int map,
        int rule,
        MatchRecordGenerationUtils generation,
        long now)
    {
        var records = new List<UdpMessage>();
        var views = await ActorViewsAsync(context, map, now);

        foreach (var bot in bots.Bots.OrderBy(participant => participant.SessionSlot))
        {
            var advance = simulation.Advance(bot, map, rule, now, views);
            records.AddRange(BotActorRecordUtils.Build(
                bot.SessionSlot,
                generation,
                advance.Body,
                advance.Fired));

            if (advance.Shot is { } shot && !IsBotSlot(shot.VictimSlot))
            {
                records.AddRange(PlayerHit(shot, bot.SessionSlot, generation, now));
            }
        }

        // The kill feed: three bytes under the operations stream, `09 <killer>
        // <victim>`, numbered on that stream. The reference writes it on the beat a
        // victim's vitals reach zero and announces the death with it; the item-box
        // identifier this loop used to write under carries a chat macro, which the
        // client played as "Roger that" on every kill.
        foreach (var kill in simulation.DrainKills())
        {
            records.Add(new UdpMessage(
                generation.Operations,
                (byte)MatchKillFeedBodiesUtils.BodyLength,
                sequences.Take(generation.Operations),
                MatchKillFeedBodiesUtils.Build(kill.KillerSlot, kill.VictimSlot)));
        }

        // The round's state blob, every 1.2 s: twenty-two bytes, the presence map's
        // field 150 and the value the captured burst carries. The working host
        // writes 522 of them across its round, in the same body the settings burst
        // closes on.
        if (now >= nextStateBlobMilliseconds)
        {
            nextStateBlobMilliseconds = now + StateBlobIntervalMilliseconds;
            records.Add(new UdpMessage(
                UdpCommandConstants.GameStateBlob,
                (byte)(PlayerSettingsBurstService.StateBlobLength + 1),
                stateBlobOrdinal++,
                PlayerSettingsBurstService.BuildClosingStateBlob()));
        }

        records.AddRange(PlayerRespawns(generation, now));
        return records;
    }

    /// <summary>
    /// Every actor the bots can place against: the room's bots at their simulated
    /// positions, and the players at the positions their clients reported.
    /// </summary>
    /// <param name="context">Context the peer is filed under.</param>
    /// <param name="map">Map the round plays.</param>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>The actors, with players whose position is unknown left out.</returns>
    private async Task<List<BotActorView>> ActorViewsAsync(PeerContext context, int map, long now)
    {
        var views = new List<BotActorView>();
        foreach (var bot in bots.Bots)
        {
            var body = simulation.BodyOf(bot, map);
            views.Add(new BotActorView(bot.SessionSlot, bot.Team ?? 0, body.X, body.Y, body.Z, IsBot: true));
        }

        var sides = await HumanSidesAsync(now);
        foreach (var member in roster.Snapshot().Where(member => !member.IsBot && member.SessionSlot > 0))
        {
            if (!sides.TryGetValue(member.SessionSlot, out var side)
                || !positions.TryGet(context.Session.RemoteAddress, out var reported))
            {
                continue;
            }

            // The client's record carries a tenth of a stage unit, which is the
            // unit the simulation walks in once it is multiplied back.
            views.Add(new BotActorView(
                member.SessionSlot,
                side,
                reported.X * 10f,
                reported.Y * 10f,
                reported.Z * 10f,
                IsBot: false));
        }

        return views;
    }

    /// <summary>
    /// The side each player in the room holds, re-read once a second so the beat
    /// does not read the roster's arrays on every tick.
    /// </summary>
    /// <param name="now">Monotonic milliseconds.</param>
    /// <returns>The side of every slot that holds one.</returns>
    private async Task<Dictionary<byte, short>> HumanSidesAsync(long now)
    {
        if (now < nextSideRead)
        {
            return humanSides;
        }

        nextSideRead = now + 1000;
        foreach (var slot in await lobbySlots.ReadAsync())
        {
            if (slot.Team is { } team)
            {
                humanSides[slot.Slot] = team;
            }
        }

        return humanSides;
    }

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
        short health;
        lock (combatGate)
        {
            if (!humanHealth.TryGetValue(shot.VictimSlot, out var held))
            {
                held = BotBodyState.MaximumVitals;
            }

            // The working host writes nothing on a slot between its zero and its
            // two hundred and fifty eight seconds later. A player at zero has
            // already been put down: another hit must not restate its health from
            // the maximum, nor push its respawn back, or it never stands up.
            if (held == 0)
            {
                return [];
            }

            health = (short)Math.Max(0, held - shot.Damage);
            humanHealth[shot.VictimSlot] = health;
            if (health == 0)
            {
                humanRespawn[shot.VictimSlot] = now + BotSimulationService.RespawnMilliseconds;

                // The reference announces the player's own death on the kill feed too
                // (`09 02 08` at t+311.03, slot 8's health reaching zero at 310.81).
                simulation.NoteKill(killerSlot, shot.VictimSlot);
            }
        }

        return
        [
            BotActorRecordUtils.BuildShot(
                shot.VictimSlot,
                generation,
                shot.Damage,
                shot.Weapon,
                shot.ImpactX,
                shot.ImpactY,
                shot.ImpactZ,
                shot.Bearing),
            new UdpMessage(
                PlayerVitalsFramesUtils.IdentifierFor(generation, shot.VictimSlot),
                3,
                BotActorRecordUtils.VitalsAttributeClass,
                [(byte)health, (byte)BotBodyState.MaximumVitals]),
        ];
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
                humanHealth[slot] = BotBodyState.MaximumVitals;
                records.Add(new UdpMessage(
                    PlayerVitalsFramesUtils.IdentifierFor(generation, slot),
                    3,
                    BotActorRecordUtils.VitalsAttributeClass,
                    [(byte)BotBodyState.MaximumVitals, (byte)BotBodyState.MaximumVitals]));
            }
        }

        return records;
    }

    /// <summary>Whether a slot belongs to one of the room's bots.</summary>
    /// <param name="slot">Slot to test.</param>
    /// <returns><c>true</c> when a bot holds it.</returns>
    private bool IsBotSlot(byte slot) => bots.Bots.Any(bot => bot.SessionSlot == slot);
}
