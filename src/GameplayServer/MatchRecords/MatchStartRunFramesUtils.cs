using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The records the recorded host writes when a round opens: the marker, the
/// thirteen records behind it, and the presence record's answer inside them.
/// </summary>
/// <remarks>
/// <para>
/// The pairing is measured and the bytes are named individually in
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2 and
/// <c>docs/protocol/DATA_PHASE_DELIVERY.md</c>: the marker is the reporting
/// client's own record identifier with the answer bit set (<c>0x5ade</c> for
/// <c>0x1ade</c>, <c>0x52de</c> for <c>0x12de</c>), and the payload behind it
/// is <see cref="MatchStartBodiesUtils.MatchStartPayload"/> — the opcode
/// <c>e0</c> and the byte the client reported, echoed back. Nothing here can be
/// written before a report arrives, because there is no byte to echo.
/// </para>
/// <para>
/// <b>The order below is measured, and it settles the open point this file used
/// to carry.</b> The run is a compressed frame, so its records are only legible
/// after decompression; decompressed, the run is <b>thirteen records</b> and the
/// marker is a record of its own beside them — the marker's own datagram in
/// <c>mgo2-game1.pcapng</c> (t+46.415, counter 24) and the run behind it in one
/// frame (counter 32793), and both inside one frame in
/// <c>mgo2-game-started-on-briefing.pcapng</c> (t+198.371, counter 655). The two
/// captures write the same thirteen, in the same order:
/// </para>
/// <code>
/// 0x5001 len 0            the roster head, twice
/// 0x5001 len 0
/// &lt;ops&gt;   len 90           the state block
/// &lt;ops&gt;   len 2            0f 00
/// 0x9001  len 26          the team array      (index)
/// 0x1001  len 26          the occupancy array (index)
/// 0x1001  len 20          the close pair
/// 0x1001  len 20
/// 0x1001  len 7           07 00 00 00 00 00 03 — the roster close
/// &lt;ops&gt;   len 3            the round clock
/// &lt;payload&gt; len 6          the answer to the report
/// 0x0a61  len 1           the empty rules list, under each class
/// 0x0a61  len 1
/// </code>
/// <para>
/// Two things in that shape were missing here before it was measured: the two
/// empty roster heads the run opens on, and the roster close that ends its
/// <c>0x1001</c> tail (<c>PlayerProfileRecordUtility.BuildRosterClose()</c>, the
/// same seven-byte record a join's roster run closes on). The close pair is
/// therefore a close <em>triple</em>, and the run is not the twelve records this
/// file used to write.
/// </para>
/// <para>
/// The companion's answer is not in either capture's run — both write it beside
/// the report, within half a second of it — so its place here is the one
/// <c>DATA_PHASE_DELIVERY.md</c> measures and keeps: behind the two arrays, so
/// the client's <c>82 NN</c> answer names the sequence the run published. The
/// state block and the clock are placed where the captures place them, which is
/// not where this file used to: the block comes before the arrays, the clock
/// after both closes, and the payload behind the clock rather than in front of
/// everything.
/// </para>
/// </remarks>
public static class MatchStartRunFramesUtils
{
    /// <summary>
    /// Records the run writes and the host repeats behind it.
    /// </summary>
    /// <remarks>
    /// The captures write the run once and repeat it; a client that missed the
    /// first frame is answered by the ones behind it rather than waiting for the
    /// next round.
    /// </remarks>
    public const int RepeatCount = 4;

    /// <summary>
    /// Operations records the run writes — its state block, the short record behind
    /// it and the round's clock — and so the values it takes from the operations
    /// stream. The capture's run takes <c>00 01 02</c> and its beat's clocks go on
    /// from <c>03</c>, which is what makes the run the opener of that stream rather
    /// than a record in the middle of one.
    /// </summary>
    public const int OperationsRecordCount = 3;

    /// <summary>
    /// Values the run's own <c>0x1001</c>-family records take from the roster
    /// exchange's counter, in send order: the two lobby-slot arrays, the room's
    /// own stage (the record and its two entries) and the close triple behind
    /// them.
    /// </summary>
    /// <remarks>
    /// Eight and not five, because the room's stage rides between the arrays and
    /// the closes: `mgo2-started-briefing-then-solo-game`'s run numbers its family
    /// <c>0x16</c>…<c>0x1d</c> — the arrays at <c>0x16</c> and <c>0x17</c>, the
    /// twenty-two-byte record at <c>0x18</c>, the two fifty-four-byte ones at
    /// <c>0x19</c> and <c>0x1a</c>, the close pair at <c>0x1b</c> and <c>0x1c</c>
    /// and the roster close at <c>0x1d</c> — and the ordinal bytes are what put
    /// the records in that order, not the order this file happens to send them in.
    /// </remarks>
    public const int RosterOrdinalCount = 8;

    /// <summary>
    /// The run's records, in send order, built for the identifier set the round
    /// plays under.
    /// </summary>
    /// <param name="generation">Identifier set of the round being opened.</param>
    /// <param name="reported">Byte the reporting client carried.</param>
    /// <param name="teamArraySequence">
    /// Sequence the lobby-slot array pair opens on. The client answers it and
    /// <see cref="Rooms.TeamArrayService"/> confirms it and moves the pair on.
    /// </param>
    /// <param name="runOrdinals">
    /// The values the run's own <c>0x1001</c> identifiers take, in send order and
    /// <see cref="RosterOrdinalCount"/> of them, from the roster exchange's counter
    /// (<see cref="Rooms.RoomRosterService.TakeRunOrdinals"/>) rather than pinned:
    /// the two lobby-slot arrays, the room's own stage and the close triple. The
    /// replay parser tells a pair by its two records being consecutive, and they
    /// are consecutive because they are the values one counter handed out — the
    /// recorded run carries 7 and 8 for the arrays and 9, 10 and 11 for the
    /// closes, which is where a join's own records leave the counter. Eight values
    /// and not two, because the closes and the room's stage are under the same
    /// identifiers and take the values behind the pair.
    /// </param>
    /// <param name="operationsOrdinals">
    /// The three values the run's operations records take, in send order
    /// (<see cref="OperationsRecordCount"/>): the state block, the short record
    /// behind it and the round's clock. They come from the round's own operations
    /// stream (<see cref="MatchRecordSequenceService"/>), which the run opens —
    /// the capture's run takes <c>00 01 02</c> and the beat's clocks behind it go
    /// on from <c>03</c>.
    /// </param>
    /// <param name="lobbySlots">
    /// The room's own players, by the slot each one holds in its 24-entry player
    /// table, and the side each is on. The pair names them, so it is the room's
    /// roster that is written and not a recorded table: a room whose players are
    /// not in the array is a room its own clients read as empty.
    /// </param>
    /// <param name="reportingSet">
    /// The room's reporting players as the presence answer's bit set, one bit per
    /// player in the order it reported (<see cref="Rooms.PlayerReadyService.PresenceMask"/>).
    /// It is the room's own set and not a constant: a room of one writes the bit
    /// of its only reporter, and a room of four the run of bits the briefing
    /// capture writes as its players arrive.
    /// </param>
    /// <param name="timeLimitUnits">
    /// Units of the round clock, in the stream's own 125 ms ticks: the round's own
    /// rule time × <see cref="MatchRecordBodiesUtils.ClockUnitsPerMinute"/>, which
    /// the room's settings state (<see cref="Match.MatchRotationEntry.TimeLimitUnits"/>).
    /// The clock is the only place on this channel a round's length is said, so a
    /// host that wrote its own default here would run a round neither the client
    /// nor the host could agree on.
    /// </param>
    /// <returns>The records of the run.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> BuildRun(
        MatchRecordGenerationUtils generation,
        byte reported,
        byte teamArraySequence,
        IReadOnlyList<byte> runOrdinals,
        IReadOnlyList<byte> operationsOrdinals,
        IReadOnlyList<Rooms.LobbyTeamSlot> lobbySlots,
        byte reportingSet,
        int timeLimitUnits)
    {
        var (teams, occupancy) = Rooms.TeamArrayFramesUtils.BuildPair(teamArraySequence, lobbySlots);

        return
        [
        // The marker the round is opened with. The answer to the report is the
        // payload record below, and the two are the same identifier in two
        // directions.
        new(generation.MatchStartMarker, []),

        // The two empty roster heads the run opens on — the same 0x5001 record a
        // join's roster run opens on, written twice as the capture writes it.
        new(UdpCommandConstants.RosterHead, []),
        new(UdpCommandConstants.RosterHead, []),

        // The operations stream's state block and the short record behind it,
        // ahead of the arrays: the order both captures write them in.
        new(
            generation.Operations,
            MatchStateBodiesUtils.MatchStartStateBlock(
                MatchRecordBodiesUtils.OperationsStateOpcode,
                timeLimitUnits),
            operationsOrdinals[0]),
        new(generation.Operations, MatchRecordBodiesUtils.OperationsShort(), operationsOrdinals[1]),

        // The lobby-slot pair. Its two index bytes are the next two values of the
        // roster exchange's counter, which is what the replay parser tells a pair
        // by: consecutive, because one counter handed both out.
        new(Rooms.RoomRosterService.HostEntryType, teams, runOrdinals[0]),
        new(UdpCommandConstants.PlayerProfile, occupancy, runOrdinals[1]),

        // The presence record's answer, behind the arrays as
        // DATA_PHASE_DELIVERY.md measures it: the room's reporting players as a
        // bit set, whose first reporter is bit one. Neither capture carries it
        // inside its run — both write it beside the report — and
        // mgo2-game-started-on-briefing writes it in this position as well.
        new(
            generation.PlayerPresence,
            MatchStartBodiesUtils.PlayerPresenceAnswer(reportingSet),
            MatchStartBodiesUtils.PlayerPresenceAnswerClass),

        // The room's own stage, between the arrays and the closes: the 22-byte
        // record and the two 54-byte entries every capture writes there. Their
        // ordinal bytes are the run's own counter, which is what puts them
        // between the arrays and the close pair on the wire.
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.RoomStateRecord(), runOrdinals[2]),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.RoomStateEntryFirst(), runOrdinals[3]),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.RoomStateEntrySecond(), runOrdinals[4]),

        // The close triple: two twenty-byte records whose remaining bytes are the
        // recorded ones, because nothing names them, and the seven-byte roster
        // close that ends a 0x1001 tail.
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.MatchStartCloseFirst(), runOrdinals[5]),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.MatchStartCloseSecond(), runOrdinals[6]),
        new(UdpCommandConstants.PlayerProfile, PlayerProfileRecordUtility.BuildRosterClose(), runOrdinals[7]),

        // The round's clock, behind the closes rather than in front of them. Both
        // records that carry a round's length carry this round's own.
        new(
            generation.Operations,
            MatchRecordBodiesUtils.RoundTimeLimitUnits(timeLimitUnits),
            operationsOrdinals[2]),

        // The answer to the report, the byte it carried echoed back under it.
        new(generation.MatchStartPayload, MatchStartBodiesUtils.MatchStartPayload(reported)),

        // The rules pair, as the beat writes it: the empty list under each class.
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster()),
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), 0x01),
        ];
    }
}
