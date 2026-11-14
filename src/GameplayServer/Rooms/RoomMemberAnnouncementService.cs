using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Announces a member who has just joined to the peers already in the room: that
/// member's roster entry, the close, the room's count and its loadout. When the
/// member first reports a position in a playing round, it adds the spawn entry,
/// the close and the member's settings header.
/// </summary>
/// <remarks>
/// <para>
/// <b>The join is an append, not a second roster run.</b> A client already in the
/// room has read every other member's entry, so repeating the run costs the
/// exchange a whole set of ordinals and tells the client nothing it does not hold.
/// The recorded late joins in <c>pcapng2</c> write the new member's profile, the
/// close, the room's count and its loadout, in that order (t+256.639 and
/// t+291.906), which is the same append <see cref="BotRosterPublishService"/>
/// writes for a seat added to a live room.
/// </para>
/// <para>
/// <b>The spawn entry belongs to the first position and not to the join.</b> The
/// same capture writes the 25-byte entry five to twenty seconds after the profile,
/// beside the first position records the room receives from that player: the
/// entry, the close and the player's twenty-four-byte settings header
/// (<see cref="SettingsHeaderBodiesUtils.Build"/>). A client handed the entry while
/// it is still loading a map has a body for a player who is not in the map yet,
/// which is why the entry waits for the position that says the player has spawned.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, which owns the exchange's ordinals.</param>
/// <param name="loadouts">Reader of the room's own players and their appearances.</param>
/// <param name="lobbySlots">Reader of the slot and side the room puts the member on.</param>
/// <param name="burst">Builder of the loadout body the settings burst uses.</param>
/// <param name="roundState">The room's round, which names the map and says whether a spawn is due.</param>
/// <param name="matchSettings">The room's stage, used when no round names one.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class RoomMemberAnnouncementService(
    RoomRosterService roster,
    LobbyLoadoutService loadouts,
    LobbySlotService lobbySlots,
    PlayerSettingsBurstService burst,
    MatchRoundStateService roundState,
    MatchSettingsService matchSettings,
    ILogger<RoomMemberAnnouncementService> logger)
{
    /// <summary>
    /// Writes the new member's join append to every other peer in the room.
    /// </summary>
    /// <param name="context">Context of the profile that seated the member.</param>
    /// <param name="member">The member to announce, as the roster holds it.</param>
    /// <param name="includeEndpointPairs">
    /// Whether the profile carries the member's two endpoint pairs, as the run
    /// answered to a native client does.
    /// </param>
    /// <param name="cancellationToken">Token that cancels the loadout read.</param>
    public async Task AnnounceAsync(
        PeerContext context,
        RosterMember member,
        bool includeEndpointPairs,
        CancellationToken cancellationToken = default)
    {
        var records = new List<RosterRecord>(4)
        {
            new(
                UdpCommandConstants.PlayerProfile,
                member.BuildRecord(includeEndpointPairs),
                roster.TakeRunOrdinal()),
            new(
                UdpCommandConstants.PlayerProfile,
                PlayerProfileRecordUtility.BuildRosterClose(),
                roster.TakeRunOrdinal()),
            new(
                UdpCommandConstants.PlayerProfile,
                RoundStateBodiesUtils.JoinPlayerCountBody((byte)(roster.JoinerCount + 1)),
                roster.TakeRunOrdinal()),
        };

        // The loadout is the only place a client reads a player's appearance from
        // (docs/protocol/UDP_SETTINGS_PAYLOAD.md). A read that fails still leaves
        // the entry and the room's count on the wire, which is what a client needs
        // to list the player at all.
        try
        {
            var room = await loadouts.ReadAsync(cancellationToken);
            if (room.FirstOrDefault(loadout => loadout.Slot == member.SessionSlot) is { } loadout)
            {
                records.Add(new(
                    UdpCommandConstants.PlayerProfile,
                    burst.BuildLoadoutBody(loadout),
                    roster.TakeRunOrdinal()));
            }
            else
            {
                logger.LogDebug(
                    "UDP {LocalPort}: the room's loadouts name no seat for {RemoteAddress}'s slot {Slot}; "
                    + "announcing the member without a loadout",
                    context.LocalPort,
                    context.Remote,
                    member.SessionSlot);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "UDP {LocalPort}: the room's loadouts could not be read, so {RemoteAddress} is announced "
                + "without its loadout",
                context.LocalPort,
                context.Remote);
        }

        foreach (var record in records)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }

        logger.LogInformation(
            "UDP {LocalPort}: announced {RemoteAddress}'s slot {Slot} to the room in {RecordCount} record(s)",
            context.LocalPort,
            context.Remote,
            member.SessionSlot,
            records.Count);
    }

    /// <summary>
    /// Builds the records that put a member who has just spawned into the room:
    /// the member's entry, the close and its settings header.
    /// </summary>
    /// <remarks>
    /// The entry's index is the room's joiner count, which is the last index the
    /// room's own table has: <c>pcapng2</c> writes <c>17</c> for the join that
    /// makes eighteen players and <c>18</c> for the next. The entry names the map's
    /// spawn, so it is written only while a round is playing.
    /// </remarks>
    /// <param name="member">The member whose first position arrived.</param>
    /// <param name="cancellationToken">Token that cancels the slot read.</param>
    /// <returns>The records in send order, each with the ordinal it takes.</returns>
    public async Task<List<RosterRecord>> BuildSpawnAsync(
        RosterMember member,
        CancellationToken cancellationToken = default)
    {
        short? side = null;
        try
        {
            var slots = await lobbySlots.ReadAsync(cancellationToken);
            side = slots.FirstOrDefault(slot => slot.Slot == member.SessionSlot)?.Team;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The lobby slots could not be read for {RemoteAddress}, so its spawn is "
                + "announced with no side",
                member.RemoteAddress);
        }

        var map = roundState.RoundSettings?.Map ?? matchSettings.Map;
        return
        [
            new(
                UdpCommandConstants.PlayerProfile,
                RoomPlayerEntryBodiesUtils.Build(
                    (byte)roster.JoinerCount,
                    member.SessionSlot,
                    RoomPlayerEntryBodiesUtils.TeamByteOf(side),
                    map),
                roster.TakeRunOrdinal()),
            new(
                UdpCommandConstants.PlayerProfile,
                PlayerProfileRecordUtility.BuildRosterClose(),
                roster.TakeRunOrdinal()),
            new(
                UdpCommandConstants.PlayerProfile,
                SettingsHeaderBodiesUtils.Build((byte)(member.SessionSlot + 1)),
                roster.TakeRunOrdinal()),
        ];
    }
}
