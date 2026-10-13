using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The records the recorded host writes when a round opens: the marker, the
/// payload that echoes the client's own ready byte, and the eleven records
/// behind them.
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
/// <b>The order below is read, not measured, and it is the one open point in
/// this file.</b> A capture holds the run as a compressed frame, so the records
/// inside it are only legible after decompression, and the two documents that
/// describe the run disagree on its size — "the 13-record match-start run" in one
/// and "the twelve records behind them" in the other, which would make fourteen.
/// What each record <em>is</em> is named; where the ones whose position is not
/// pinned sit is a reading. It is recorded here rather than padded to a count,
/// because a record invented to reach a number is worse than a run one short.
/// </para>
/// <para>
/// Two positions <em>are</em> pinned and are kept: the companion's answer rides
/// behind the two arrays (<c>DATA_PHASE_DELIVERY.md</c> — "the one position a
/// capture measures"), and the array pair is published before the companion, so
/// the client's <c>82 NN</c> answer names the sequence the run published.
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
    /// The run's records, in send order, built for the identifier set the round
    /// plays under.
    /// </summary>
    /// <param name="generation">Identifier set of the round being opened.</param>
    /// <param name="reported">Byte the reporting client carried.</param>
    /// <param name="teamArraySequence">
    /// Sequence the lobby-slot array pair opens on. The client answers it and
    /// <see cref="Rooms.TeamArrayService"/> confirms it and moves the pair on.
    /// </param>
    /// <param name="lobbySlots">
    /// The room's own players, by the slot each one holds in its 24-entry player
    /// table, and the side each is on. The pair names them, so it is the room's
    /// roster that is written and not a recorded table: a room whose players are
    /// not in the array is a room its own clients read as empty.
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
        IReadOnlyList<Rooms.LobbyTeamSlot> lobbySlots,
        int timeLimitUnits)
    {
        var (teams, occupancy) = Rooms.TeamArrayFramesUtils.BuildPair(teamArraySequence, lobbySlots);

        return
        [
        // The marker, and the byte the report carried echoed back under it.
        new(generation.MatchStartMarker, []),
        new(generation.MatchStartPayload, MatchStartBodiesUtils.MatchStartPayload(reported)),

        // The lobby-slot pair, written behind the payload record. Its two index
        // bytes are the pair's own and consecutive, which is what the replay
        // parser tells a pair by; both captures of a run write 7 and 8.
        new(
            Rooms.RoomRosterService.HostEntryType,
            teams,
            Rooms.TeamArrayFramesUtils.RunTeamArrayIndex),
        new(
            UdpCommandConstants.PlayerProfile,
            occupancy,
            Rooms.TeamArrayFramesUtils.RunOccupancyArrayIndex),

        // The companion's answer, in the one position a capture measures.
        new(
            generation.ReadyState,
            MatchStartBodiesUtils.ReadyStateAnswer(),
            MatchStartBodiesUtils.ReadyStateAnswerClass),

        // The operations stream: the state block, the short record and the clock.
        // Both records that carry a round's length carry this round's own.
        new(
            generation.Operations,
            MatchStateBodiesUtils.MatchStartStateBlock(
                MatchRecordBodiesUtils.OperationsStateOpcode,
                timeLimitUnits)),
        new(generation.Operations, MatchRecordBodiesUtils.OperationsShort()),
        new(generation.Operations, MatchRecordBodiesUtils.RoundTimeLimitUnits(timeLimitUnits)),

        // The rules pair, as the beat writes it: the empty list under each class.
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster()),
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), 0x01),

        // The close pair: two twenty-byte records whose remaining bytes are the
        // recorded ones, because nothing names them.
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.MatchStartCloseFirst()),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.MatchStartCloseSecond(), 0x01),
        ];
    }
}
