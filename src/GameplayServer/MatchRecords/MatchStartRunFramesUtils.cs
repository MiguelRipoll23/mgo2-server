using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The frame that opens the round and the four times the recorded host writes it
/// again <em>inside the beat that follows</em>, as measured from the solo window
/// of <c>mgo2-game1.pcapng</c>. Split out of <see cref="MatchRecordFramesUtils"/>
/// so neither file exceeds the repository's size limit; the bodies are built by
/// <see cref="MatchRecordBodiesUtils"/>.
/// </summary>
public static class MatchStartRunFramesUtils
{
    /// <summary>
    /// Beat frames between one run-shaped frame and the next: 56, 1.85 s at the
    /// measured 33 ms beat. Measured from the solo window's own frame counters —
    /// the run at 32793 and its four repeats at 32849, 32905, 32961 and 33017,
    /// each 56 frames after the last.
    /// </summary>
    /// <remarks>
    /// It is a beat-frame interval and not a delay, because the repeats ride in
    /// the beat rather than in front of it: the recorded host keeps the 33 ms
    /// frame moving and writes the run-shaped frame <em>instead of</em> the beat
    /// at those four indices. The writer uses it as a frame index, which is what
    /// keeps the seven seconds after the run alive.
    /// </remarks>
    public const int MatchStartRepeatPeriodFrames = 56;

    /// <summary>
    /// The frame that opens the round, written once the client has reported
    /// itself ready: two empty
    /// <see cref="UdpCommandConstants.RosterHead"/> records, the ninety-byte
    /// operations state block and the two-byte operations record under it, then
    /// the team and occupancy arrays at the consecutive index bytes 7 and 8 —
    /// the pair the replay parser joins — the close, the payload record and the
    /// first rules-roster pair.
    /// </summary>
    /// <param name="generation">Identifier set of the round being opened.</param>
    /// <param name="reported">Byte the client's readiness report carried.</param>
    /// <returns>The records of the run, in the captured order.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> MatchStartRun(
        MatchRecordGenerationUtils generation, byte reported) =>
    [
        new(UdpCommandConstants.RosterHead, [], 10),
        new(UdpCommandConstants.RosterHead, [], 11),
        new(generation.RoundClock, MatchStateBodiesUtils.MatchStartStateBlock(
            MatchRecordBodiesUtils.OperationsStateOpcode,
            MatchRecordBodiesUtils.RoundClockUnits), 0),
        new(generation.RoundClock, MatchRecordBodiesUtils.OperationsShort(), 1),
        new(UdpCommandConstants.JoinRequest, MatchRecordBodiesUtils.TeamArray(), MatchRecordFramesUtils.TeamArrayIndex),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.OccupancyArray(), MatchRecordFramesUtils.TeamArrayIndex + 1),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.MatchStartCloseFirst(), 9),
        new(UdpCommandConstants.PlayerProfile, MatchRecordBodiesUtils.MatchStartCloseSecond(), 10),
        new(UdpCommandConstants.PlayerProfile, [.. PlayerProfileRecordUtility.BuildRosterClose()], 11),
        new(generation.RoundClock, MatchRecordBodiesUtils.RoundClock(), 2),
        new(generation.MatchStartPayload, MatchRecordBodiesUtils.MatchStartPayload(reported), 0),
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), 0),
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), 1),
    ];

    /// <summary>
    /// The match-start run's repeat: the same records without the two opening
    /// <see cref="UdpCommandConstants.RosterHead"/> records and with the two
    /// operations records — the state block and the short form — moved behind
    /// the five roster records.
    /// </summary>
    /// <param name="generation">Identifier set of the round being repeated.</param>
    /// <param name="reported">Byte the client's readiness report carried.</param>
    /// <returns>The records of the repeat, in the captured order.</returns>
    public static List<MatchRecordFramesUtils.RecordMessage> MatchStartRunRepeat(
        MatchRecordGenerationUtils generation, byte reported)
    {
        var run = MatchStartRun(generation, reported);
        var repeat = new List<MatchRecordFramesUtils.RecordMessage>(run.Count - 2);
        repeat.AddRange(run.Where(record => record.Type != UdpCommandConstants.RosterHead));
        var operations = repeat.FindAll(record => record.Type == generation.RoundClock);
        repeat.RemoveAll(record => record.Type == generation.RoundClock);
        // The five roster records keep their place; the operations pair follows
        // them, before the payload and rules records.
        var payloadIndex = repeat.FindIndex(record => record.Type == generation.MatchStartPayload);
        repeat.InsertRange(payloadIndex, operations);
        return repeat;
    }
}
