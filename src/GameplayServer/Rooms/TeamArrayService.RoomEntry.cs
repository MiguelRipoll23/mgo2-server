using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The room's answer to a client's request to be placed in it, split out of
/// <c>TeamArrayService.cs</c> so neither file exceeds the repository's size limit.
/// </summary>
/// <remarks>
/// The service is the same object: this file is its <c>83 NN</c> answer and the
/// other is the echoed pair's confirmation and the pair's republishing. The entry
/// is answered in the pass that received it, under the sequence the request names,
/// and what it carries is the phase's — see the remarks on the method.
/// </remarks>
public sealed partial class TeamArrayService
{
    /// <summary>
    /// Answers a client that has asked to be placed in the room: the room's own
    /// state, under the sequence the request names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The request is answered with the room, not with one record.</b> Every
    /// capture answers it with the lobby-slot array pair under the sequence the
    /// request carried — the peer's own slot and the room's sides, which the
    /// client cannot read off a channel it is not in yet — and what follows the
    /// pair is the round's: a peer answered during a *briefing* gets the pair
    /// alone (`mgo2-game-started-on-briefing` t+93.13, `mgo2-game2` t+78.99), and
    /// one answered during a round gets the pair, its own entry and the roster's
    /// close (game1 t+201.04, `started-on-solo-game` t+101.41,
    /// `started-briefing-then-solo` t+180.49, measured in
    /// `docs/analysis/join-stall-after-mode2.md`).
    /// </para>
    /// <para>
    /// <b>A briefing's answer is the pair, the countdown and the player's settings
    /// header.</b> `mgo2-game-started-on-briefing` t+93.127 and `mgo2-game2`
    /// t+78.987 both write <c>85 NN / 86 NN</c>, then <c>0b 3c 00 00 00 00 00</c>
    /// (<see cref="BriefingCountdownService.AnswerRecord"/>), then a 24-byte
    /// <c>0b SS 02 …</c> for the requesting player
    /// (<see cref="SettingsHeaderBodiesUtils"/>) — and no entry, which is what the
    /// pair-and-countdown answer used to lack: the client was told which room it
    /// was in and what the room was holding, and nothing of the briefing it was
    /// sitting in.
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
    /// <b>And it is a round's record, written only while one is playing.</b> The
    /// entry carries the map's spawn, and a client handed one while it is still
    /// being briefed leaves the briefing for the map: `myserver26`'s join took it
    /// at t+67.84, its first position record followed 0.9 s later and its round
    /// never opened, which is the round start the player saw in place of the
    /// briefing screen (`myserver25` is the same join one second apart). The two
    /// answers the captures give a *briefing* carry the pair and no entry
    /// (`mgo2-game-started-on-briefing` t+93.13, `mgo2-game2` t+78.99), and the
    /// three given during a round carry it with the close behind it (game1
    /// t+201.04, `started-on-solo-game` t+101.41, `started-briefing-then-solo`
    /// t+180.49). So the answer before a round is the pair alone, and the entry
    /// and the close are written for a round the client is in.
    /// </para>
    /// <para>
    /// <b>What the captures carry beyond this group is not written.</b> In a round
    /// the answer carries the requesting player's own entity block — state,
    /// position and aim, which only a host that simulates the player has — so that
    /// is recorded as the gap it is rather than written on a resemblance. See the
    /// analysis file's "what is not written" section.
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

        // The pair opens the answer, under the sequence the request named, and
        // everything after it is the phase's: the captures' own order, measured in
        // the five answers named above.
        var (teams, occupancy) = TeamArrayFramesUtils.BuildPair(sequence, slots);
        var pairOrdinals = roster.TakeRunOrdinals(2);
        var run = new List<RosterRecord>(4)
        {
            new(RoomRosterService.HostEntryType, teams, pairOrdinals[0]),
            new(UdpCommandConstants.PlayerProfile, occupancy, pairOrdinals[1]),
        };

        // The requesting peer's own player, when the room has one for it. A request
        // that arrives before the profile is answered with the room's other
        // records: the entry and the header both name a player of the room, and
        // the room does not hold this one yet. Both endpoints are looked up, for
        // the reason the roster's own reaper does: a peer is filed under the
        // endpoint its profile arrived from, which is the observed source, while
        // the session answers at the one the peer advertised. The two are one for
        // a peer out on the internet.
        var member = roster.Find(context.Remote.ToString())
            ?? roster.Find(context.Session.DialBack.ToString());

        // A briefing's answer is the countdown and the player's own settings
        // header: the entry and the close that follow them in a round are the
        // round's records, and a peer still being briefed that is handed the entry
        // leaves the briefing for a round that has not opened (the remarks above).
        if (roundState.IsOpen)
        {
            if (member is not null)
            {
                var side = slots.Find(slot => slot.Slot == member.SessionSlot);
                run.Add(
                    new(UdpCommandConstants.PlayerProfile,
                        RoomPlayerEntryBodiesUtils.Build(
                            0,
                            member.SessionSlot,
                            RoomPlayerEntryBodiesUtils.TeamByteOf(side?.Team),
                            matchSettings.Map),
                        roster.TakeRunOrdinal()));
            }
            else
            {
                logger.LogDebug(
                    "UDP {LocalPort}: a room request from {RemoteAddress} has no roster entry behind it yet; "
                    + "answering with the room's own state only",
                    context.LocalPort,
                    context.Remote);
            }

            run.Add(
                new(UdpCommandConstants.PlayerProfile,
                    PlayerProfileRecordUtility.BuildRosterClose(),
                    roster.TakeRunOrdinal()));
        }
        else
        {
            // The countdown numbers itself on the stream its tick writes, so the
            // record the answer carries is one the client could have received a
            // tick later rather than a second series beside the countdown. A room
            // whose briefing is not armed has no count to answer with.
            if (countdowns.AnswerRecord() is { } countdown)
            {
                run.Add(countdown);
            }

            if (member is not null)
            {
                run.Add(
                    new(UdpCommandConstants.PlayerProfile,
                        SettingsHeaderBodiesUtils.Build((byte)(member.SessionSlot + 1)),
                        roster.TakeRunOrdinal()));
            }
        }

        logger.LogInformation(
            "UDP {LocalPort}: answering {RemoteAddress}'s request to be placed in the room: {RecordCount} "
            + "records at sequence {Sequence:x2}, {SlotCount} player(s) on the lobby-slot arrays, {Phase}",
            context.LocalPort,
            context.Remote,
            run.Count,
            sequence,
            slots.Count,
            roundState.IsOpen
                ? "with the player's own entry"
                : "with the briefing's countdown and the player's settings header");

        await RosterRunSendUtils.SendAsync(context, run, compressed: true);
    }
}
