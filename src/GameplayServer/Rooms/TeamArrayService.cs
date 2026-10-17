using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Runs the lobby-slot array handshake: it confirms the sequence a client
/// answered and publishes the pair again under the next one.
/// </summary>
/// <remarks>
/// <para>
/// The loop is the client's, and the confirmation is the only thing that moves
/// it. A host that writes the pair and never confirms gets one answer and then
/// silence — the client only advances once the sequence it answered has been
/// confirmed — and a host that confirms and does not republish leaves the client
/// with nothing to answer next. The next sequence is named beside the
/// confirmation in the announcement group (<see cref="TeamArrayFramesUtils"/>),
/// and without it the client answers nothing after the confirmation. All three
/// are written here, in one pass, as the captures show them.
/// </para>
/// <para>
/// <b>The pair is per peer.</b> Each client answers under its own sequence and
/// is confirmed under it, so the pair is written back to the peer that answered
/// and not to the room. The captures hold one peer's side of the exchange and so
/// cannot show a room, but the sequence the answer names is the peer's own, and a
/// pair fanned out to the room would carry one peer's sequence to all of them.
/// </para>
/// <para>
/// The pair is the seventh and eighth records of the round's opening run
/// (<see cref="MatchStartRunFramesUtils"/>), which is where its first sequence
/// comes from, so the two stay in step: the run publishes <c>85 NN</c> and
/// <c>86 NN</c>, the client answers <c>82 NN</c> and <c>84 NN</c>, and this
/// confirms <c>81 NN</c> and publishes <c>85 NN+1</c>.
/// </para>
/// <para>
/// <b>What the pair says is the room's and is read again here.</b> The republish
/// names the room's own players and sides, and it is read per confirmation rather
/// than carried over from the run: a client that answers a pair twice over a
/// briefing in which someone changed sides is answered with the room as it now
/// is. The read is one query against the room's rows
/// (<see cref="LobbySlotService"/>) behind a client that has just answered a
/// record, so it adds no wait the client can see.
/// </para>
/// </remarks>
/// <param name="lobbySlots">Reader of the room's own players and their sides.</param>
/// <param name="roster">
/// The room's roster exchange, whose counter numbers the republished pair: the
/// two values it takes are the next two the identifier's records take, so a pair
/// republished a hundred times over a briefing carries a hundred values and
/// never repeats one the peer has already seen.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed class TeamArrayService(
    LobbySlotService lobbySlots,
    RoomRosterService roster,
    ILogger<TeamArrayService> logger)
{
    /// <summary>
    /// Answers a client that has asked to be placed in the room: the room's own
    /// state, under the sequence the request names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The request is answered with the room, not with one record.</b> Every
    /// capture answers it with four records in one frame — the lobby-slot array
    /// pair under the sequence the request carried, the requesting peer's own
    /// entry, and the roster's close (`mgo2-game1` t+201.0, `mgo2-game2` t+79.0,
    /// `mgo2-game-started-on-briefing` t+93.1, `mgo2-started-on-solo-game`
    /// t+101.4, and `mgo2-started-briefing-then-solo-game` t+180.5, measured in
    /// `docs/analysis/join-stall-after-mode2.md`) — and the group is what this
    /// writes: the peer's own slot, the room's sides and the close are the three
    /// things the client cannot read off a channel it is not in yet.
    /// </para>
    /// <para>
    /// <b>The order is the captures', and so is what the group does not
    /// carry.</b> The pair opens the answer and the close ends it, which is the
    /// reverse of the roster run's own order. Two records this host used to write
    /// here are not written: the first copy of the peer's own roster entry
    /// belongs to the join and is answered there
    /// (<see cref="RosterAnswerService"/>), and the empty <c>0x5001</c> head is
    /// the run's. No captured answer carries either.
    /// </para>
    /// <para>
    /// <b>The entry is the room's own and not the roster's.</b>
    /// <see cref="RoomPlayerEntryBodiesUtils"/> carries the 25-byte record's four
    /// measured fields and the recorded tail behind them; its remarks hold the
    /// measurement and the one field left unresolved.
    /// </para>
    /// <para>
    /// <b>What the captures carry beyond this group is not written.</b> A 24-byte
    /// loadout header (<c>0b SS 02</c> and zeros) rides in some answers and not
    /// others, and in a round the answer carries the requesting player's own
    /// entity block; neither is derivable from anything this host holds, so both
    /// are recorded as the gap they are rather than written on a resemblance. See
    /// the analysis file's "what is not written" section.
    /// </para>
    /// <para>
    /// <b>It is written in the pass that received the request</b>, which is
    /// where the captures write it: the answers land 0.3 to 3.2 s after the
    /// request they answer and a re-sent request is answered the same way, so
    /// nothing here waits on a clock.
    /// </para>
    /// </remarks>
    /// <param name="context">Context of the request being answered.</param>
    /// <param name="sequence">Sequence byte the client's request named.</param>
    public async Task EnterRoomAsync(PeerContext context, byte sequence)
    {
        var slots = await lobbySlots.ReadAsync();
        var bodies = new List<byte[]>();

        // The pair opens the answer, under the sequence the request named, and
        // the entry and the close follow it: the captures' own order, measured in
        // the four answers named above.
        var (teams, occupancy) = TeamArrayFramesUtils.BuildPair(sequence, slots);
        bodies.Add(teams);
        bodies.Add(occupancy);

        // The requesting peer's own entry, when the room has a player for it. A
        // request that arrives before the profile is answered with the room's
        // other records: the entry names a player of the room, and the room does
        // not hold this one yet. Both endpoints are looked up, for the reason the
        // roster's own reaper does: a peer is filed under the endpoint its
        // profile arrived from, which is the observed source, while the session
        // answers at the one the peer advertised. The two are one for a peer out
        // on the internet.
        var member = roster.Find(context.Remote.ToString())
            ?? roster.Find(context.Session.DialBack.ToString());
        if (member is not null)
        {
            var side = slots.Find(slot => slot.Slot == member.SessionSlot);
            bodies.Add(
                RoomPlayerEntryBodiesUtils.Build(
                    0,
                    member.SessionSlot,
                    RoomPlayerEntryBodiesUtils.TeamByteOf(side?.Team)));
        }
        else
        {
            logger.LogDebug(
                "UDP {LocalPort}: a room request from {RemoteAddress} has no roster entry behind it yet; "
                + "answering with the room's own state only",
                context.LocalPort,
                context.Remote);
        }

        bodies.Add(PlayerProfileRecordUtility.BuildRosterClose());

        // The pair travels under the tag the joiner opened the exchange with and
        // the rest under the roster's own, as both the run and the answer do.
        var ordinals = roster.TakeRunOrdinals(bodies.Count);
        var run = new List<RosterRecord>(bodies.Count)
        {
            new(RoomRosterService.HostEntryType, bodies[0], ordinals[0]),
        };
        for (var index = 1; index < bodies.Count; index++)
        {
            run.Add(new(UdpCommandConstants.PlayerProfile, bodies[index], ordinals[index]));
        }

        logger.LogInformation(
            "UDP {LocalPort}: answering {RemoteAddress}'s request to be placed in the room: {RecordCount} "
            + "records at sequence {Sequence:x2}, {SlotCount} player(s) on the lobby-slot arrays",
            context.LocalPort,
            context.Remote,
            run.Count,
            sequence,
            slots.Count);

        await RosterRunSendUtils.SendAsync(context, run, compressed: true);
    }

    /// <summary>
    /// Confirms the sequence a client answered and publishes the pair under the
    /// next one.
    /// </summary>
    /// <param name="context">Context of the answer being confirmed.</param>
    /// <param name="answered">Sequence the client answered.</param>
    public async Task ConfirmAsync(PeerContext context, byte answered)
    {
        var confirmation = TeamArrayFramesUtils.BuildConfirmation(answered);
        logger.LogDebug(
            "UDP {LocalPort}: confirming the lobby-slot arrays at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            answered,
            context.Remote);

        await context.Send(
            UdpCommandConstants.PlayerProfile,
            confirmation,
            TeamArrayFramesUtils.ConfirmationClass);

        var next = TeamArrayFramesUtils.NextSequence(answered);

        // The sequence the pair is republished under is announced beside the
        // confirmation rather than left to be inferred from the pair: every
        // reference capture writes this group the moment it confirms, and the
        // client only answers NN+1 once it has seen it
        // (TeamArrayFramesUtils.BuildNextSequenceRecords).
        var (announcement, firstCompanion, secondCompanion) =
            TeamArrayFramesUtils.BuildNextSequenceRecords(next);

        var announcementOrdinals = roster.TakeRunOrdinals(3);
        var announcementRun = new List<RosterRecord>(3)
        {
            new(RoomRosterService.HostEntryType, announcement, announcementOrdinals[0]),
            new(UdpCommandConstants.PlayerProfile, firstCompanion, announcementOrdinals[1]),
            new(UdpCommandConstants.PlayerProfile, secondCompanion, announcementOrdinals[2]),
        };

        await RosterRunSendUtils.SendAsync(context, announcementRun, compressed: true);

        var (teams, occupancy) = TeamArrayFramesUtils.BuildPair(next, await lobbySlots.ReadAsync());

        // The pair's two index bytes are the next two values of the roster
        // exchange's counter, not a restarted 0 and 1: the counter is what tells
        // the replay parser the two records are one pair, and a pair that
        // restarted would carry numbers records before it have already taken
        // (docs/GAMEPLAY_SERVER.md §7).
        var ordinals = roster.TakeRunOrdinals(2);
        var pair = new List<RosterRecord>
        {
            new(RoomRosterService.HostEntryType, teams, ordinals[0]),
            new(UdpCommandConstants.PlayerProfile, occupancy, ordinals[1]),
        };

        logger.LogDebug(
            "UDP {LocalPort}: publishing the lobby-slot arrays again at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            next,
            context.Remote);

        await RosterRunSendUtils.SendAsync(context, pair, compressed: true);
    }
}
