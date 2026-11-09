using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Writes the room's bots into the round: while one is open, every bot's actor
/// block goes to each peer at the capture's own cadence.
/// </summary>
/// <remarks>
/// <para>
/// <b>A roster entry puts a bot on the briefing screen; these records put it in
/// the map.</b> The client builds an actor from the position record its slot's
/// block carries — that is why a room whose host writes no block shows the bots
/// in the briefing and nothing at all in the game — and the state and vitals
/// records beside it are what make the actor alive rather than a corpse.
/// </para>
/// <para>
/// <b>The cadence is the recorded host's.</b> The working-server capture writes
/// actor records on the 33 ms match beat, with every actor in the room in the
/// same frame. This writer uses that same beat for the room's bots.
/// </para>
/// <para>
/// <b>It writes only while the round is open.</b> A briefing's client is not in
/// the map, and the room's own entry is the round's record; blocks written
/// ahead of the round are the "round start in place of the briefing screen"
/// failure the room's entry already avoids (<see cref="Rooms.TeamArrayService"/>).
/// </para>
/// </remarks>
/// <param name="bots">The room's bot population, which holds every bot's slot and side.</param>
/// <param name="roundState">The room's round, which says when one is playing and on which map.</param>
/// <param name="matchSettings">This host's room settings, which name the map before a round opens.</param>
/// <param name="roster">The room's roster, which hands out the run's ordinals.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotActorPublishService(
    BotPopulationService bots,
    MatchRoundStateService roundState,
    MatchSettingsService matchSettings,
    RoomRosterService roster,
    ILogger<BotActorPublishService> logger)
{
    /// <summary>Milliseconds between bot actor frames on the match beat.</summary>
    public const int TickMilliseconds = MatchRecordFramesUtils.FramePeriodMilliseconds;

    private readonly Lock gate = new();
    private readonly HashSet<PeerSession> writing = [];

    /// <summary>
    /// Starts the bot blocks for a peer, once, and returns before the first tick.
    /// </summary>
    /// <param name="context">Context of the pass the peer's report arrived through.</param>
    public void Start(PeerContext context)
    {
        lock (gate)
        {
            if (!writing.Add(context.Session))
            {
                return;
            }
        }

        logger.LogDebug(
            "UDP {LocalPort}: writing the room's bot actors to {RemoteAddress} while its rounds play",
            context.LocalPort,
            context.Remote);
        _ = RunAsync(context);
    }

    /// <summary>
    /// Writes a peer's bot blocks for every round the room opens, for as long as
    /// the peer is there.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    private async Task RunAsync(PeerContext context)
    {
        var session = context.Session;

        try
        {
            while (MatchRecordPeerUtils.IsAlive(session))
            {
                await roundState.WaitForOpenAsync(session, MatchRoundStateService.WaitWhilePeerIsThere);

                // The round's open is not the client's. A room opens the round
                // before a peer has been served its opening run — the live freeze of
                // 2026-11-09 12:45 is exactly that: the round was open at 12:45:21,
                // the bots were named and their blocks streamed from that second,
                // and the client never left the briefing because its own run only
                // went out at 12:45:31. The run is the boundary the reference's
                // append and blocks follow (`pcapng1` t+485.7, six seconds before
                // its run at t+491.58), so the writer waits for it and not for the
                // open.
                if (roundState.IsOpen)
                {
                    await roundState.WaitForRunWrittenAsync(
                        session,
                        MatchRoundStateService.OpenWaitMilliseconds);
                }

                if (!roundState.IsOpen || !roundState.HasRunWritten(session))
                {
                    // The waits are bounded, so a round without a run yet is not the
                    // end of the writer: the room's next one is still ahead of it and
                    // its bots belong in that one.
                    await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
                    continue;
                }

                // The bots are not named again here. The recorded host appends a
                // fill's bot entries and loadouts when the fill *seats* them, and
                // this host does that on the report that seats them; writing the
                // same append at every round start is what froze the client of
                // 2026-11-09 12:48 — it was sent the round's entries, the close, the
                // count and eight loadouts it had already read, then stopped drawing
                // frames and never reported again. The actor blocks below are the
                // only thing the round itself adds.
                // A bot's own round state goes out once, before its blocks: a
                // player in the round has told the host the settings it plays
                // with, and a bot has no client to do it. `pcapng1` t+485.74
                // writes one per occupied key six seconds before its run.
                await AnnouncePlayerListAsync(context);
                await AnnounceRoundStateAsync(context);

                using var period = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMilliseconds));
                while (roundState.IsOpen && MatchRecordPeerUtils.IsAlive(session))
                {
                    await PublishAsync(context);
                    await period.WaitForNextTickAsync();
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the bot actors to {RemoteAddress} ended",
                context.LocalPort,
                session.RemoteAddress);
        }
        finally
        {
            lock (gate)
            {
                writing.Remove(session);
            }
        }
    }

    /// <summary>
    /// Writes the round's player list: one two-byte record per player, its own
    /// key behind a zero byte, numbered from zero.
    /// </summary>
    /// <remarks>
    /// <b>This is where a remote player is created for a client.</b> `pcapng1`
    /// writes ten of them at t+491.12, half a second before its round's run, each
    /// two bytes and one ordinal: <c>0007</c>, <c>0014</c>, <c>0012</c>, <c>000d</c>,
    /// <c>000b</c>, <c>0013</c>, <c>0006</c>, <c>0005</c>, <c>000f</c>, <c>0010</c> —
    /// every key the room's entries mark occupied, in one frame, the human's
    /// included. Its identifier is the item-box group's own with the <c>0x08</c> bit
    /// added (<c>0x1a50</c> is <c>0x1a58</c>, its sibling <c>0x1250</c> is
    /// <c>0x1258</c>), so it travels under the generation the round plays under.
    /// Masking the <c>0x0800</c> bit out of the item-box identifier instead builds
    /// <c>0x0a58</c>, which no client knows — the run of 2026-11-09 13:08 wrote that
    /// and listed nobody.
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private async Task AnnouncePlayerListAsync(PeerContext context)
    {
        var players = roster.Snapshot()
            .Where(member => member.SessionSlot > 0)
            .OrderBy(member => member.SessionSlot)
            .ToList();
        if (players.Count == 0)
        {
            return;
        }

        var generation = roundState.Generation;
        var playerListType = (ushort)(generation.ItemBox | 0x0008);
        var records = new List<UdpMessage>(players.Count);
        for (var index = 0; index < players.Count; index++)
        {
            records.Add(new UdpMessage(
                playerListType,
                3,
                (byte)index,
                [0x00, players[index].SessionSlot]));
        }

        if (context.SendRecords is { } sendRecords)
        {
            await sendRecords(records);
        }
        else
        {
            foreach (var record in records)
            {
                await context.Send(record.Type, record.Body, record.Flags);
            }
        }

        logger.LogDebug(
            "UDP {LocalPort}: listed {PlayerCount} player(s) under 0x{ListType:x4} for {RemoteAddress}",
            context.LocalPort,
            players.Count,
            playerListType,
            context.Remote);
    }

    /// <summary>
    /// Writes each bot's own round-start settings body, one per bot, as the
    /// recorded host writes it for every player of its room.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    private async Task AnnounceRoundStateAsync(PeerContext context)
    {
        var seated = bots.Bots.OrderBy(bot => bot.SessionSlot).ToList();
        if (seated.Count == 0)
        {
            return;
        }

        var run = new List<RosterRecord>(seated.Count);
        foreach (var bot in seated)
        {
            run.Add(new RosterRecord(
                UdpCommandConstants.PlayerProfile,
                BotRoundStateBodiesUtils.Build(bot.SessionSlot),
                roster.TakeRunOrdinal()));
        }

        await RosterRunSendUtils.SendAsync(context, run);
        logger.LogDebug(
            "UDP {LocalPort}: wrote {BotCount} bot round state(s) to {RemoteAddress}",
            context.LocalPort,
            seated.Count,
            context.Remote);
    }

    /// <summary>
    /// Writes every bot's actor block to the peer as one frame.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    private async Task PublishAsync(PeerContext context)
    {
        var map = roundState.RoundSettings?.Map ?? matchSettings.Map;

        // Bots have no simulated movement, so their actor blocks use their side's
        // map spawn rather than stacking every bot at the peer's position.
        var generation = roundState.Generation;
        var records = new List<UdpMessage>();
        foreach (var bot in bots.Bots.OrderBy(participant => participant.SessionSlot))
        {
            records.AddRange(BotActorRecordUtils.Build(bot.SessionSlot, bot.Team, map, generation));
        }

        if (records.Count == 0)
        {
            return;
        }

        if (context.SendRecords is { } sendRecords)
        {
            await sendRecords(records);
            return;
        }

        foreach (var record in records)
        {
            await context.Send(record.Type, record.Body, record.Flags);
        }
    }
}
