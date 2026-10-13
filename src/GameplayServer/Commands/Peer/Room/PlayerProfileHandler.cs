using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Handles a player-profile record. The joiner sends its own profile once the
/// session is keyed and then re-sends it, byte for byte, until the host answers;
/// this handler answers with the whole room roster and announces the new player
/// to peers already in the room.
/// </summary>
/// <remarks>
/// <para>
/// The type is shared. A joining client also sends one-byte <c>0x1001</c>
/// messages, which are not profiles and are not answered with a roster.
/// </para>
/// <para>
/// <b>The roster is the answer to the profile, written in the pass it arrives
/// in.</b> There is no copy to wait for. The client sends its profile once the
/// session is keyed and re-sends the same body, byte for byte, 1.93 s later only
/// when nothing has answered it, so answering in the pass that received it is
/// what keeps the re-send off the wire. A copy that arrives anyway is answered
/// the same way — a peer still waiting for its roster is not a peer whose retry
/// is to be ignored — and the member is filed either way, so a re-send lands in
/// the slot the client already has.
/// </para>
/// <para>
/// <b>What the captures show.</b> The recorded host sends one roster run per
/// join, and in game1 that run follows the <em>re-sent</em> copy: the joiner's
/// two copies are 1.934 s apart and the run is 2.476 s after the first, 0.542 s
/// after the second. game2's round sends three copies and no roster run this
/// reader can resolve, so game1's single round is the whole of the evidence.
/// What the capture fixes is the records; it cannot say whether a client that
/// receives them 2.4 s earlier — which is what answering in the first pass does
/// — is any worse off, and the one run whose roster was acknowledged under the
/// two did go quiet afterwards.
/// </para>
/// </remarks>
/// <param name="roster">Roster of the room this host is playing.</param>
/// <param name="burstScheduler">Scheduler of the one-shot settings burst after roster exchange.</param>
/// <param name="matchRecords">Records the recorded host keeps writing after the burst.</param>
/// <param name="matchStart">Writer of the run the room's next round opens with.</param>
/// <param name="teamArrays">The lobby-slot array handshake, which answers under two of these identifiers.</param>
/// <param name="score">
/// The host's own score, which a scoreboard delta arriving on this identifier
/// is merged into. The identifier carries the scoreboard as well as the roster,
/// and the two are told apart by their length (<see cref="MatchScoreboardFramesUtils"/>).
/// </param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(
    RoomRosterService roster,
    PlayerSettingsBurstSchedulerService burstScheduler,
    MatchRecordService matchRecords,
    MatchStartRunService matchStart,
    TeamArrayService teamArrays,
    MatchScoreService score,
    ILogger<PlayerProfileHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        // The lobby-slot array handshake answers the pair under these two
        // identifiers and no other, and a two-byte body naming the pair's
        // sequence byte is that answer and not a profile. It is read here
        // because the registry binds one handler to an identifier and this is
        // the one already bound to both.
        if (TeamArrayFramesUtils.IsArrayEcho(context.Message.Body))
        {
            await teamArrays.ConfirmAsync(context, TeamArrayFramesUtils.SequenceOf(context.Message.Body));
            return;
        }

        // The same identifier carries the game's own scoreboard, and its body is
        // the only other shape on it: the settings discriminator, a player's slot
        // and the values that changed. A peer that writes one is telling the room
        // a score this host has not counted itself, so it is merged and passed on
        // rather than read as a profile the peer never sent.
        if (MatchScoreboardFramesUtils.TryParse(context.Message.Body, out var delta))
        {
            MergeScoreboard(context, delta);
            var relay = context.Relay ?? context.Broadcast;
            await relay(context.Message.Type, context.Message.Body, context.Message.Flags);
            return;
        }

        var profile = PlayerProfileRecordParseUtils.Parse(context.Message.Body);
        if (profile is not { Name.Length: > 0 })
        {
            logger.LogDebug(
                "UDP {LocalPort}: {MessageType:x4} of {BodyLength} bytes from {RemoteAddress} carries no profile; not answered",
                context.LocalPort,
                context.Message.Type,
                context.Message.Body.Length,
                context.Remote);
            return;
        }

        var member = roster.Register(
            context.Remote,
            profile,
            context.Session.PeerIdentifier,
            context.Session.PeerAddressData);
        logger.LogDebug(
            "UDP {LocalPort}: registered roster entry for {RemoteAddress}: character {CharacterId}, "
            + "slot {RosterIndex}, current joiner count {JoinerCount}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
            member.RosterIndex,
            roster.JoinerCount);

        if (context.Session.PeerAddressData.Length != PlayerProfileRecordUtility.AddressDataLength)
        {
            logger.LogWarning(
                "UDP {LocalPort}: profile from {RemoteAddress} has no valid pair of handshake endpoints; "
                + "its roster address fields cannot be echoed faithfully",
                context.LocalPort,
                context.Remote);
        }

        logger.LogInformation(
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterId} name {Name}, roster slot {RosterIndex}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
            profile.Name,
            member.RosterIndex);

        var run = roster.BuildRosterRun();
        logger.LogDebug(
            "UDP {LocalPort}: built roster response for {RemoteAddress}: {RecordCount} records, "
            + "{JoinerCount} joiners, {FrameCount} outbound roster frames including repeat and trailer",
            context.LocalPort,
            context.Remote,
            run.Count,
            roster.JoinerCount,
            3);
        foreach (var record in run.Where(record => record.Type == RoomRosterService.HostEntryType))
        {
            logger.LogInformation(
                "UDP {LocalPort}: sending this host's own roster entry to {RemoteAddress} under "
                + "0x{MsgType:x4}, the tag a joiner opens the exchange with; the rest of the roster "
                + "travels as 0x{RosterType:x4}",
                context.LocalPort,
                context.Remote,
                record.Type,
                UdpCommandConstants.PlayerProfile);
        }

        var repeat = roster.BuildRosterRunRepeat();
        var trailer = RoomRosterService.BuildRosterTrailer();

        // In this pass: nothing is paused off the dispatch, so the run, its repeat
        // and its trailer go out inside the client's 1.93 s re-send timer rather
        // than after it.
        await SendRosterAsync(context, run, repeat, trailer);
    }

    /// <summary>
    /// Merges a scoreboard delta a peer wrote into the host's own score: the bits
    /// this file names, and nothing for the ones it does not.
    /// </summary>
    /// <param name="context">Context the delta arrived through.</param>
    /// <param name="delta">Slot and values the delta carried.</param>
    private void MergeScoreboard(PeerContext context, MatchScoreboardFramesUtils.ScoreboardDelta delta)
    {
        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} reports a scoreboard delta for slot {Slot} with "
            + "{ValueCount} value(s)",
            context.LocalPort,
            context.Remote,
            delta.Slot,
            delta.Values.Count);

        foreach (var value in delta.Values)
        {
            switch (value.Bit)
            {
                case MatchScoreboardFramesUtils.KillsBit:
                    score.NoteKills(delta.Slot, value.Value);
                    break;
                case MatchScoreboardFramesUtils.DeathsBit:
                    score.NoteDeaths(delta.Slot, value.Value);
                    break;
                case MatchScoreboardFramesUtils.ScoreBit:
                    score.NoteScore(delta.Slot, value.Value);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Writes the roster run, its repeat and its trailing record, and announces
    /// the run to the room's other peers.
    /// </summary>
    /// <param name="context">Context of the profile this roster answers.</param>
    /// <param name="run">Roster records to write, in order.</param>
    /// <param name="repeat">The same records without the opening answer.</param>
    /// <param name="trailer">Record that closes the exchange.</param>
    private async Task SendRosterAsync(
        PeerContext context,
        IReadOnlyList<RosterRecord> run,
        IReadOnlyList<RosterRecord> repeat,
        RosterRecord trailer)
    {
        // Both runs go out compressed, as the recorded host sends them. The same
        // two runs sent in the clear were the experiment that separated our LZSS
        // stream from the records in it - the peer answered those and answered
        // nothing against our marked frames - and the difference was the encoder,
        // not the marker: it wrote no end-of-stream marker, which is the peer
        // reader's only clean exit, so both frames were dropped on arrival.
        logger.LogDebug(
            "UDP {LocalPort}: sending roster response ({RecordCount} records, compressed)",
            context.LocalPort,
            run.Count);
        await RosterRunSendUtils.SendAsync(context, run, compressed: true);
        logger.LogDebug(
            "UDP {LocalPort}: sending roster repeat ({RecordCount} records, compressed)",
            context.LocalPort,
            repeat.Count);
        await RosterRunSendUtils.SendAsync(context, repeat, compressed: true);

        logger.LogDebug(
            "UDP {LocalPort}: sending roster trailer 0x{MessageType:x4} to {RemoteAddress}",
            context.LocalPort,
            trailer.Type,
            context.Remote);
        await context.Send(trailer.Type, trailer.Body, trailer.Ordinal);

        // The recorded host does not stop at the roster. 2.52 s later it writes
        // the one burst of the round - the room's rotation, the joiner's loadout,
        // the two 54-byte second loadouts and a state blob - and then keeps the
        // channel moving with the match records under it, which is what a client
        // that has been answered the roster waits on. Both are scheduled here, in
        // the pass that answered the roster, and neither is awaited: the pause is
        // longer than the dispatch loop should be held.
        logger.LogDebug(
            "UDP {LocalPort}: scheduling settings burst and match records for {RemoteAddress}",
            context.LocalPort,
            context.Remote);
        burstScheduler.Schedule(context);
        matchRecords.Start(context);

        // The peer that has just been answered the roster is in the room's
        // briefing, and the room's next round will open for it too. Its watcher
        // is armed here, in the pass that answered the roster, so the round's
        // opening run reaches this peer whether it readies first or last.
        matchStart.Start(context);

        logger.LogDebug(
            "UDP {LocalPort}: announcing {RecordCount} roster records from {RemoteAddress} to other peers",
            context.LocalPort,
            context.Remote,
            run.Count);
        foreach (var record in run)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }
    }
}