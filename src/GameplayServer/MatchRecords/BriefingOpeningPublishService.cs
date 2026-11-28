using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Opens a stage-end briefing for a peer before that peer reports: the lobby-slot
/// confirmation, the room's two state records and the peer's own settings header
/// and loadout, and the held countdown.
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
///
/// <b>And the opening is not the round.</b> The report that arrives inside the
/// paced opening is the client's map-loaded state, and what it is owed is the
/// briefing's own run and the room's briefing time. The round behind the
/// briefing opens when that time is out, not when this opening ends
/// (<c>MatchStartRunService.EntryRun</c>).
/// </para>
/// <para>
/// <b>What it writes is the room's, read at the stage end.</b> The confirmation
/// and the next-sequence group move the array exchange on; the peer's settings
/// header and loadout name it in the client's player table; and the countdown is
/// the briefing's own held record
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
/// <param name="loadouts">Reader of the players' appearances and skills.</param>
/// <param name="burst">Builder of the loadout body the join uses.</param>
/// <param name="roster">The room's roster, whose exchange numbers the records.</param>
/// <param name="countdowns">The briefing's clock, which writes its held count.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingOpeningPublishService(
    TeamArrayService teamArrays,
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
            // **The client's array exchange is confirmed before anything else.** The
            // reference's first record at a stage end is the confirmation
            // (`rounded-ended-by-reaching-tickets-limit` t+207.448, `81 02`), and its
            // pair at the next sequence follows three seconds later (t+210.237). The
            // pair at the answered sequence is not written: the client has already
            // read and answered it, and a copy in front of the confirmation is a
            // sequence it answers again instead of advancing (TeamArrayService).
            var sequence = roster.PairSequenceOf(context.Remote.ToString());
            var next = await teamArrays.ConfirmAsync(context, sequence, writeAnsweredPair: false);
            roster.NotePairSequence(context.Remote.ToString(), next);

            // **The state pair waits for the pace the reference gives it.** The
            // confirmation is at t+207.448 and the transfers that open the briefing
            // at t+210.237; a client handed the whole stage-end opening in one
            // instant answers none of it (live session of 2026-11-28 02:40, whose
            // last answer was the record before the confirmation). The count and the
            // room's publication are already running beside this wait, so the client
            // has the stage's own records to read while the screens cross.
            await Task.Delay(MatchScreenTimingsUtils.StageOpeningStateGapMilliseconds);

            // **The room crosses the boundary and then states the briefing.** The
            // reference writes field 0 at `1` and then at `2` at t+210.237, the two
            // ten-byte transfers the client leaves its result screen on. Written
            // ahead of the confirmation they reached a client still in its result
            // screen's exchange, which is the map that never faded (live session of
            // 2026-11-28 02:28). Field 51 is not one of these two: the capture's
            // pair is ten bytes each, and the rounds-played value belongs to the
            // round's own open behind the briefing
            // (RoundStateBodiesUtils.RoundOpenTransfers).
            await context.Send(
                UdpCommandConstants.RoomRecord,
                RoundStateBodiesUtils.RoomStateBody(RoomState.Transition),
                roster.TakeRunOrdinal());
            await context.Send(
                UdpCommandConstants.RoomRecord,
                RoundStateBodiesUtils.RoomStateBody(RoomState.Briefing),
                roster.TakeRunOrdinal());

            // **The stage end does not re-announce the peer as a new player.** The
            // reference writes a nine-byte entry and the close here, not a second
            // profile: a full profile is the join's record and carries the name, and
            // the stage-end opening of 2026-11-28 02:40 wrote one and was answered by
            // nothing. The client already has its own profile from the join; the
            // header and loadout below are the stage end's own naming of it.
            var peer = roster.Snapshot().FirstOrDefault(
                member => member.RemoteAddress.ToString() == context.Remote.ToString());

            // The pair the confirmation named is the one the client answers next,
            // and the one the briefing's run names. It follows the state pair, which
            // is where the reference publishes it (t+210.237).
            await teamArrays.PublishNextPairAsync(context, next);

            // The held count is the record that tells the client its briefing screen
            // is open, and the reference writes it in front of the settings, not
            // behind them (t+210.237). The countdown task takes over once the
            // briefing's run is written.
            await countdowns.PublishStageOpeningAsync(context);

            // **The stage end names the peer, and not the whole room again.** The
            // reference's opening writes one entry and one loadout there, not the
            // join's whole settings burst; the live stage end of 2026-11-28 02:28
            // wrote sixteen headers and seventeen loadouts in one pass and the client
            // stopped acknowledging at the tenth loadout, nine records short of the
            // stream's head, so it never reported into the briefing.
            if (peer is not null)
            {
                var records = new List<RosterRecord>(2)
                {
                    new(
                        UdpCommandConstants.RoomRecord,
                        SettingsHeaderBodiesUtils.Build((byte)(peer.SessionSlot + 1)),
                        roster.TakeRunOrdinal()),
                };

                // The loadout is where a client reads an appearance from. A read that
                // fails leaves the header on the wire, which is still a peer the
                // briefing names.
                try
                {
                    var room = await loadouts.ReadAsync();
                    if (room.FirstOrDefault(loadout => loadout.Slot == peer.SessionSlot) is { } loadout)
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
                        + "names its player without the appearance",
                        context.Remote);
                }

                await RosterRunSendUtils.SendAsync(context, records);
            }

            logger.LogInformation(
                "UDP {LocalPort}: opened the stage-end briefing for {RemoteAddress}",
                context.LocalPort,
                context.Remote);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "UDP {LocalPort}: the stage-end briefing to {RemoteAddress} ended on a failed send",
                context.LocalPort,
                context.Remote);
        }
    }
}
