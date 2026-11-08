using Mgo2Server.GameplayServer.Rooms;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// Reads what a bot can see of the room it is in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two sources are the room's own.</b> Who is present is the roster
/// (<see cref="RoomRosterService.Snapshot"/>), and which side each player is on
/// is the lobby-slot read (<see cref="LobbySlotService.ReadAsync"/>) — the same
/// pair the team arrays are built from, so a bot's idea of an enemy is exactly
/// the idea the client is drawn. Nothing here reads a packet: a position, an
/// aim or a weapon another player carries is not on either source, and no field
/// is invented to stand in for one.
/// </para>
/// <para>
/// <b>A bot on no side sees nobody as an enemy.</b> Without a side to compare
/// against, every other player is neither teammate nor enemy, and the snapshot
/// says so by holding both lists empty. It is the honest reading: the match
/// states no side for this bot, so no other player can be placed against it.
/// </para>
/// </remarks>
/// <param name="roster">Roster of the room, which owns who is in it.</param>
/// <param name="lobbySlots">Reader of the room's players and their sides.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotPerceptionService(
    RoomRosterService roster,
    LobbySlotService lobbySlots,
    ILogger<BotPerceptionService> logger)
{
    /// <summary>
    /// Reads one bot's view of the room.
    /// </summary>
    /// <param name="bot">The bot reading.</param>
    /// <param name="cancellationToken">Token that cancels the sides' read.</param>
    /// <returns>What the bot can see, with what it cannot left unstated.</returns>
    public async Task<BotPerceptionSnapshot> ReadAsync(
        BotParticipant bot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);

        if (bot.Team is null)
        {
            logger.LogDebug(
                "Bot {BotName} is on no side, so it sees no teammate and no enemy",
                bot.Name);
            return BotPerceptionSnapshot.None;
        }

        var sides = await lobbySlots.ReadAsync(cancellationToken);
        var sideOfSlot = sides
            .GroupBy(slot => slot.Slot)
            .ToDictionary(group => group.Key, group => group.First().Team);

        var teammates = new List<BotPerceptionEntry>();
        var enemies = new List<BotPerceptionEntry>();

        foreach (var member in roster.Snapshot())
        {
            if (member.SessionSlot == bot.SessionSlot)
            {
                continue;
            }

            if (!sideOfSlot.TryGetValue(member.SessionSlot, out var side)
                || !RoomSideUtils.IsSide(side))
            {
                // A player the room puts on no side is neither an ally nor a
                // target: the match states nothing to place it against.
                continue;
            }

            var entry = new BotPerceptionEntry(
                member.Name,
                member.SessionSlot,
                side,
                member.IsBot);

            if (side == bot.Team)
            {
                teammates.Add(entry);
            }
            else
            {
                enemies.Add(entry);
            }
        }

        logger.LogDebug(
            "Bot {BotName} on side {Side} sees {TeammateCount} teammate(s) and {EnemyCount} enemy(s); "
            + "positions known: no",
            bot.Name,
            bot.Team,
            teammates.Count,
            enemies.Count);

        return new BotPerceptionSnapshot(teammates, enemies, OwnHealth: null, PositionsKnown: false);
    }
}
