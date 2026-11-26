using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Opens a stage-end briefing for a peer before that peer reports: the lobby-slot
/// confirmation, every player's settings header and loadout, and the held
/// countdown.
/// </summary>
/// <remarks>
/// <para>
/// <b>The result screens are the stage end's, and the briefing behind them is the
/// host's to write.</b> The reference host does not wait for a report there:
/// `rounded-ended-by-reaching-tickets-limit.pcapng` writes <c>81 02</c>,
/// the players' settings payloads and the held <c>0b 3c</c> countdown at
/// t+207.4–210.2, and its client reports at t+211.0. A host that waits for the
/// report has no record to send, so the client stays on the result screen and its
/// session goes quiet — the black screen and the missing player this service
/// closes.
/// </para>
/// <para>
/// <b>What it writes is the room's, read at the stage end.</b> The confirmation
/// and the next-sequence group move the array exchange on; the settings headers
/// and loadouts put every player of the room back in the client's player table;
/// and the countdown is the briefing's own held record
/// (<see cref="BriefingCountdownService.PublishStageOpeningAsync"/>). The
/// publication that keeps the rules pair and the count running through the
/// briefing is started beside this one
/// (<see cref="BriefingRoomPublishService"/>).
/// </para>
/// <para>
/// <b>It does not fault.</b> A failed send says what the peer's session already
/// says, so the failure is logged and the publication ends.
/// </para>
/// </remarks>
/// <param name="teamArrays">The lobby-slot exchange, which confirms the peer's sequence.</param>
/// <param name="lobbySlots">Reader of the room's players, by slot and side.</param>
/// <param name="loadouts">Reader of the players' appearances and skills.</param>
/// <param name="burst">Builder of the loadout body the join uses.</param>
/// <param name="roster">The room's roster, whose exchange numbers the records.</param>
/// <param name="countdowns">The briefing's clock, which writes its held count.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingOpeningPublishService(
    TeamArrayService teamArrays,
    LobbySlotService lobbySlots,
    LobbyLoadoutService loadouts,
    PlayerSettingsBurstService burst,
    RoomRosterService roster,
    BriefingCountdownService countdowns,
    ILogger<BriefingOpeningPublishService> logger)
{
    /// <summary>
    /// Writes the opening of the stage-end briefing to one peer.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        try
        {
            // The confirmation moves the client's array exchange on, and the pair it
            // republishes is the room's membership as the briefing sees it.
            var sequence = roster.PairSequenceOf(context.Remote.ToString());
            await teamArrays.ConfirmAsync(context, sequence);

            // The pair the confirmation republishes is the one the briefing's run
            // names. Noting it here keeps the run from naming the sequence the
            // client has just answered while its echo of the next one is in flight.
            roster.NotePairSequence(
                context.Remote.ToString(),
                TeamArrayFramesUtils.NextSequence(sequence));

            var slots = await lobbySlots.ReadAsync();
            var records = new List<RosterRecord>(slots.Count);

            // Every player of the room is announced in the player table the stage
            // end rebuilt, in the slot byte the loadout records carry.
            foreach (var slot in slots)
            {
                records.Add(new(
                    UdpCommandConstants.RoomRecord,
                    SettingsHeaderBodiesUtils.Build((byte)(slot.Slot + 1)),
                    roster.TakeRunOrdinal()));
            }

            // The loadout is where a client reads an appearance from. A read that
            // fails leaves the settings headers on the wire, which is still a room
            // whose players are named.
            try
            {
                foreach (var loadout in await loadouts.ReadAsync())
                {
                    records.Add(new(
                        UdpCommandConstants.RoomRecord,
                        burst.BuildLoadoutBody(loadout),
                        roster.TakeRunOrdinal()));
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "The room's loadouts could not be read, so the stage-end briefing to {RemoteAddress} "
                    + "names its players without their appearances",
                    context.Remote);
            }

            await RosterRunSendUtils.SendAsync(context, records);

            // The held count is the record that tells the client its briefing screen
            // is open; the countdown task takes over once the briefing's run is written.
            await countdowns.PublishStageOpeningAsync(context);

            logger.LogInformation(
                "UDP {LocalPort}: opened the stage-end briefing for {RemoteAddress} with {PlayerCount} "
                + "player(s) and {RecordCount} roster record(s)",
                context.LocalPort,
                context.Remote,
                slots.Count,
                records.Count);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the stage-end briefing to {RemoteAddress} ended on a failed send",
                context.LocalPort,
                context.Remote);
        }
    }
}
