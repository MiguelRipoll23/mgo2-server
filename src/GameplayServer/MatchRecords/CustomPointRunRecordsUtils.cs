using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The run's objective-point records: one kind-<c>0x03</c> message per target the
/// round's rule places, in send order.
/// </summary>
/// <remarks>
/// <b>The point's index is its position in the list</b> — the record's own
/// <c>+4</c> byte and the low nibble of its payload's task byte, which is what
/// makes Capture's pair <c>0x00</c>/<c>0x01</c> and Race's <c>0x60</c>/<c>0x61</c>
/// (<see cref="CustomPointRecordBodiesUtils.Build"/>). A rule with no objectives
/// yields no record at all, and the run's tail closes on the array pair alone.
/// That absence is the gate: Team Deathmatch is written none, which is why KEROTAN
/// and GA-KO no longer appear in it (<see cref="CustomPointUtils"/>).
/// </remarks>
public static class CustomPointRunRecordsUtils
{
    /// <summary>
    /// Builds the run's point records, each under the ordinal the roster exchange's
    /// counter gave it.
    /// </summary>
    /// <param name="points">
    /// The round's points and task; the points start at
    /// <see cref="MatchStartRunFramesUtils.CustomPointOrdinalOffset"/> in the run.
    /// </param>
    /// <param name="runOrdinals">The run's own values from the roster exchange's counter.</param>
    /// <returns>One message per point, in the order they are sent.</returns>
    public static IEnumerable<MatchRecordFramesUtils.RecordMessage> Build(
        CustomPointSet points,
        IReadOnlyList<byte> runOrdinals) =>
        points.Points.Select((point, index) => new MatchRecordFramesUtils.RecordMessage(
            UdpCommandConstants.RoomRecord,
            CustomPointRecordBodiesUtils.Build(index, points.Task, point),
            runOrdinals[MatchStartRunFramesUtils.CustomPointOrdinalOffset + index]));
}
