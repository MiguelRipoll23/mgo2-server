using Mgo2Server.GameplayServer.Bots;
using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Domain.Games;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Reads the room's own lobby-slot assignments: which slot of the 24-entry
/// player table each player of the room holds, and which side it is on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two halves come from the two places that own them.</b> The slot is the
/// host's own allocation, kept in the roster because the host is what handed it
/// out (<see cref="RoomRosterService.Snapshot"/>), and the side is a row the
/// client reported to the lobby and the lobby stored, so it is read through the
/// games domain's own service rather than out of its table
/// (<see cref="GameService.GetPlayerTeamsAsync"/>). Neither is this server's to
/// derive: a host that numbered the players itself would name slots the clients
/// were not given, and a host that picked sides would put players on teams the
/// lobby shows them as not being on.
/// </para>
/// <para>
/// The read is per publication and not cached. The pair is written when a round
/// opens and again on every confirmation the client sends, and a room whose
/// players changed sides in between is a room whose next pair should say so; a
/// cached read would keep telling a client the teams of the briefing before.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, which holds every player's slot.</param>
/// <param name="match">This host's own room, whose rows name the sides.</param>
/// <param name="gameService">The games domain's service, which owns those rows.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbySlotService(
    RoomRosterService roster,
    MatchService match,
    GameService gameService,
    BotSideService bots,
    ILogger<LobbySlotService> logger)
{
    /// <summary>
    /// The room's players, each with the slot it holds and the side it is on.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the read of the sides.</param>
    /// <returns>
    /// One entry per player in the room, in no order the caller depends on, or an
    /// empty list while this host has published no room.
    /// </returns>
    public async Task<List<LobbyTeamSlot>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var members = roster.Snapshot();
        var matchIdentifier = match.MatchIdentifier;
        if (matchIdentifier == 0)
        {
            logger.LogWarning(
                "This host has published no room yet, so the lobby-slot arrays can name no player of it");
            return [];
        }

        var teams = await gameService.GetPlayerTeamsAsync(matchIdentifier, cancellationToken);
        var slots = new List<LobbyTeamSlot>(members.Count);
        var unknown = 0;

        // Bots are named by the population service with the side it resolved for
        // them; their roster rows hold no team, because a bot has no row there.
        foreach (var member in members.Where(member => !member.IsBot))
        {
            var team = teams.TryGetValue(member.CharacterId, out var stored) ? stored : (short?)null;
            if (!RoomSideUtils.IsSide(team))
            {
                // A side the match does not have — including the 0xFE the client
                // reports for itself before a lobby puts it on one — and a player
                // the room's rows do not hold at all: the slot is still the
                // player's and the pair marks it taken. The orange side is one of
                // the match's sides and is not this case.
                team = null;
                unknown++;
            }

            slots.Add(new LobbyTeamSlot(member.SessionSlot, team));
        }

        slots.AddRange(bots.TeamSlots);

        logger.LogDebug(
            "Match {GameIdentifier}: the lobby-slot arrays name {PlayerCount} player(s) in slot(s) {Slots}; "
            + "{UnknownCount} of them the room's rows put on no side",
            matchIdentifier,
            slots.Count,
            string.Join(", ", slots.Select(slot => slot.Slot)),
            unknown);
        return slots;
    }
}
