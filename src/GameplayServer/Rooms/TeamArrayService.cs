using Mgo2Server.GameplayServer.Match;
using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Runs the lobby-slot array handshake: it answers the sequence a client echoed
/// with the pair at that same sequence, then confirms it and publishes the pair
/// under the next one.
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
/// The room's roster exchange, whose counter numbers the whole exchange: the
/// confirmation, the group and the pair take one consecutive range of it, so the
/// run reads in order on the wire and a pair republished a hundred times over a
/// briefing carries a hundred values and never repeats one the peer has already
/// seen.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed class TeamArrayService(
    LobbySlotService lobbySlots,
    RoomRosterService roster,
    MatchSettingsService matchSettings,
    ILogger<TeamArrayService> logger)
{
    /// <summary>
    /// Confirmation runs already written, by the peer that answered. A client
    /// sends its two answers for one sequence together, so this handler runs twice
    /// for the same sequence and the second pass must repeat the first's records
    /// rather than number them again: every capture repeats them with the same
    /// ordinals (`mgo2-game1` and `mgo2-started-briefing-then-solo-game` both
    /// re-send the confirmation and group unchanged), and the live freeze that
    /// numbered the group twice is what this holds back.
    /// </summary>
    private readonly Dictionary<string, ConfirmationRun> confirmationRuns = [];

    /// <summary>Guards <see cref="confirmationRuns"/>, this service being a singleton.</summary>
    private readonly object confirmationGate = new();

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
                    RoomPlayerEntryBodiesUtils.TeamByteOf(side?.Team),
                    matchSettings.Map));
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
    /// Answers the sequence a client echoed and then confirms it, publishing the
    /// pair under the next one.
    /// </summary>
    /// <remarks>
    /// <b>The pair at the echoed sequence comes first, and it is what the
    /// captures answer an echo with.</b> A client's <c>82 NN</c>/<c>84 NN</c> is
    /// met by <c>85 NN</c>/<c>86 NN</c> — the pair at the very sequence it named —
    /// and only later by the confirmation: `mgo2-game-joined-when-red-team-then-briefing`
    /// echoes <c>82 02</c> at t+57.983 and is answered with the pair at <c>02</c> at
    /// t+62.666 and again at t+65.138, the confirmation <c>81 02</c> and the pair at
    /// <c>03</c> following at t+72.560. Until this change the echo was met with the
    /// confirmation alone, which is an answer to a sequence the client had not been
    /// given since it asked. `docs/analysis/udp-protocol-patterns.md` has the
    /// measured loop and the stall that is still unexplained.
    /// </remarks>
    /// <param name="context">Context of the answer being confirmed.</param>
    /// <param name="answered">Sequence the client answered.</param>
    public async Task ConfirmAsync(PeerContext context, byte answered)
    {
        // The echo names the pair the client wants, and the pair is written under
        // that same sequence before anything advances it.
        var (answeredTeams, answeredOccupancy) =
            TeamArrayFramesUtils.BuildPair(answered, await lobbySlots.ReadAsync());
        var answeredOrdinals = roster.TakeRunOrdinals(2);
        await RosterRunSendUtils.SendAsync(
            context,
            [
                new(RoomRosterService.HostEntryType, answeredTeams, answeredOrdinals[0]),
                new(UdpCommandConstants.PlayerProfile, answeredOccupancy, answeredOrdinals[1]),
            ],
            compressed: true);

        var next = TeamArrayFramesUtils.NextSequence(answered);
        var run = TakeConfirmationRun(context, answered, next);

        logger.LogDebug(
            "UDP {LocalPort}: confirming the lobby-slot arrays at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            answered,
            context.Remote);

        await context.Send(
            UdpCommandConstants.PlayerProfile,
            run.Confirmation.Body,
            run.Confirmation.Ordinal);

        await RosterRunSendUtils.SendAsync(context, run.Group, compressed: true);

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

    /// <summary>
    /// Returns the confirmation and the next-sequence group for a sequence,
    /// writing them once and repeating the same records for a second answer of
    /// the same sequence.
    /// </summary>
    /// <remarks>
    /// <b>The two records are one run and are written once.</b> The confirmation,
    /// the announcement and the two companions take one consecutive range of the
    /// roster counter and the confirmation takes its head, so the run reads in
    /// order on the wire and the client can advance it
    /// (<see cref="TeamArrayFramesUtils.BuildConfirmation"/>). A client sends its
    /// two answers for a sequence in one datagram, so this runs twice for the same
    /// sequence; the second pass repeats the first's records and ordinals, the way
    /// every capture repeats them, rather than number a second group the client
    /// has no place for.
    /// </remarks>
    /// <param name="context">Context of the answer being confirmed.</param>
    /// <param name="answered">Sequence the client answered.</param>
    /// <param name="next">Sequence the pair is republished under.</param>
    /// <returns>The confirmation and the group, in the order they are written.</returns>
    private ConfirmationRun TakeConfirmationRun(PeerContext context, byte answered, byte next)
    {
        var key = context.Remote.ToString();
        lock (confirmationGate)
        {
            if (confirmationRuns.TryGetValue(key, out var cached) && cached.Sequence == answered)
            {
                return cached;
            }

            var (announcement, firstCompanion, secondCompanion) =
                TeamArrayFramesUtils.BuildNextSequenceRecords(next);

            var ordinals = roster.TakeRunOrdinals(4);
            var run = new ConfirmationRun(
                answered,
                new RosterRecord(
                    UdpCommandConstants.PlayerProfile,
                    TeamArrayFramesUtils.BuildConfirmation(answered),
                    ordinals[0]),
                [
                    new(RoomRosterService.HostEntryType, announcement, ordinals[1]),
                    new(UdpCommandConstants.PlayerProfile, firstCompanion, ordinals[2]),
                    new(UdpCommandConstants.PlayerProfile, secondCompanion, ordinals[3]),
                ]);

            confirmationRuns[key] = run;
            return run;
        }
    }

    /// <summary>
    /// Forgets the confirmation run held for a peer that is starting over.
    /// </summary>
    /// <remarks>
    /// <b>The held run belongs to one session.</b> Its ordinals are a position in
    /// the roster exchange's counter, and a peer that has re-handshaken expects
    /// the exchange to go on from where the run it is served leaves it — replaying
    /// a cached run would hand back ordinals the peer's own sequence has already
    /// passed, which is a pair it can only drop. The answer is the next run this
    /// service takes, not the last one it took.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the peer was filed under.</param>
    public void Forget(string remoteAddress)
    {
        lock (confirmationGate)
        {
            if (confirmationRuns.Remove(remoteAddress))
            {
                logger.LogDebug(
                    "UDP: dropped the held lobby-slot confirmation for {RemoteAddress}; it is joining again",
                    remoteAddress);
            }
        }
    }

    /// <summary>
    /// A confirmation and the next-sequence group written for one answered
    /// sequence, held so a second answer of the same sequence repeats them.
    /// </summary>
    /// <param name="Sequence">Sequence the records confirm.</param>
    /// <param name="Confirmation">The confirmation record.</param>
    /// <param name="Group">The announcement and its two companions, in order.</param>
    private sealed record ConfirmationRun(
        byte Sequence,
        RosterRecord Confirmation,
        RosterRecord[] Group);
}
