using Mgo2Server.GameplayServer.Gameplay;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The combat half of the actor writer: it advances every bot, builds its block
/// from the body the tick leaves, and writes the hits the bots land.
/// </summary>
/// <remarks>
/// <b>A hit is a damage record on the victim and, for a player, that player's
/// vitals at the health it now holds</b> — the capture writes both, for a bot's
/// hits on another bot too. The player's position is the one the client reported
/// (<see cref="PlayerPositionService"/>) and the sides are the room's reading
/// (<see cref="LobbySlotService"/>), so a bot does not shoot its teammate.
/// </remarks>
public sealed partial class BotActorPublishService
{
    private readonly Dictionary<byte, short> humanSides = [];
    private readonly Queue<UdpMessage> damageEchoes = new();
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

    /// <summary>
    /// How often a bot's vitals record is written while its health does not change:
    /// once a second, which is the capture's own cadence for a standing value.
    /// </summary>
    private const int VitalsIntervalMilliseconds = 1000;

    /// <summary>Drops the combat state and its ordinals, because the room has emptied.</summary>
    public void Reset()
    {
        // A death still waiting for its feed belongs to the round that emptied, and
        // announcing it into the next one would credit a kill nobody made.
        pendingFeeds.Clear();
        pendingVitals.Clear();
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

            // The vitals record rides on a change and once a second, which is the
            // capture's cadence for a bot — slot 1 writes 719 of them against
            // 15 900 position records — and not on every beat.
            var publishVitals = advance.Body.Health != advance.Body.LastPublishedHealth
                || now >= advance.Body.NextVitalsMilliseconds;
            if (publishVitals)
            {
                advance.Body.LastPublishedHealth = advance.Body.Health;
                advance.Body.NextVitalsMilliseconds = now + VitalsIntervalMilliseconds;
            }

            records.AddRange(BotActorRecordUtils.Build(
                bot.SessionSlot,
                generation,
                advance.Body,
                advance.Fired,
                advance.Body.ShotThisBeat,
                publishVitals));

            if (advance.Shot is { } shot)
            {
                // A hit is a damage record on the victim, bot or player: the capture
                // writes them for a bot's hits on another bot as well.
                if (IsBotSlot(shot.VictimSlot))
                {
                    records.Add(BotActorRecordUtils.BuildShot(
                        shot.VictimSlot,
                        generation,
                        shot.Damage,
                        shot.Weapon,
                        shot.ImpactX,
                        shot.ImpactY,
                        shot.ImpactZ,
                        shot.Bearing,
                        shot.Lethal));
                }
                else
                {
                    records.AddRange(PlayerHit(shot, bot.SessionSlot, generation, now));
                }
            }
        }

        // The host echoes a client's own hit back unchanged, which is what the
        // capture does: the same fifteen bytes under the same identifier appear in
        // the client's stream and the host's. The echo is what carries a hit's
        // subtype — and with it a headshot's impact point — to the client that
        // draws the death; this host dropped it, so every death was drawn as a
        // body hit.
        foreach (var echo in DrainDamageEchoes())
        {
            records.Add(echo);
        }

        // The kill feed, and the score behind it (`BotActorPublishService.Kills.cs`).
        DrainKills(generation, records);

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

        // The copies that follow a player's changed health, which the working host
        // writes four of for one change (BotActorPublishService.Players.cs).
        records.AddRange(DrainVitalsRepeats(now));
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

            // A corpse is not a target. The capture's bots leave a body alone once
            // its health is zero — its 350 host-written hits name only a living
            // actor — and a bot that kept firing at one would write damage on a
            // body the client is drawing as dead.
            if (body.IsDead)
            {
                continue;
            }

            views.Add(new BotActorView(bot.SessionSlot, bot.Team ?? 0, body.X, body.Y, body.Z, IsBot: true));
        }

        var sides = await HumanSidesAsync(now);
        foreach (var member in roster.Snapshot().Where(member => !member.IsBot && member.SessionSlot > 0))
        {
            if (!sides.TryGetValue(member.SessionSlot, out var side)
                || !positions.TryGet(context.Session.RemoteAddress, out var reported)
                || IsVictimDown(member.SessionSlot))
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
    /// Keeps a hit a client wrote, so the next beat echoes it back byte for byte,
    /// which is what the working host does.
    /// </summary>
    /// <param name="identifier">Identifier the hit arrived under.</param>
    /// <param name="body">The hit's fifteen bytes.</param>
    public void NoteDamageEcho(ushort identifier, byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        lock (combatGate)
        {
            // A hit on an actor that is already down is dropped rather than echoed.
            // The capture holds seven of the client's own hits on a corpse and not
            // one of them comes back: the host relays a living actor's damage and
            // leaves a dead one alone, which is why a captured body does not take
            // damage when it is shot. Echoing them is what made ours flinch and
            // bleed health it does not have, and what a client drawing a corpse
            // reads as damage on the body.
            if (BotActorRecordUtils.SlotOfDamageIdentifier(identifier) is not { } victim
                || !IsVictimDownLocked(victim))
            {
                damageEchoes.Enqueue(new UdpMessage(
                    identifier,
                    (byte)(body.Length + 1),
                    BotActorRecordUtils.DamageAttributeClass,
                    body));
            }
        }
    }

    /// <summary>Takes the hits waiting to be echoed, oldest first.</summary>
    /// <returns>One record per hit a client wrote since the last call.</returns>
    private IReadOnlyList<UdpMessage> DrainDamageEchoes()
    {
        lock (combatGate)
        {
            var taken = new List<UdpMessage>();
            while (damageEchoes.Count > 0)
            {
                taken.Add(damageEchoes.Dequeue());
            }

            return taken;
        }
    }

    /// <summary>Whether a slot belongs to one of the room's bots.</summary>
    /// <param name="slot">Slot to test.</param>
    /// <returns><c>true</c> when a bot holds it.</returns>
    private bool IsBotSlot(byte slot) => bots.Bots.Any(bot => bot.SessionSlot == slot);
}
