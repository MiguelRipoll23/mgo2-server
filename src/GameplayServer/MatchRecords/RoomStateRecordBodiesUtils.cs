using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's own stage: the pair of twenty-two-byte records a briefing and a
/// round's opening run carry, as the port client's host writes them. Split out
/// of <see cref="MatchRecordBodiesUtils"/> so neither file exceeds the
/// repository's size limit.
/// </summary>
public static class RoomStateRecordBodiesUtils
{
    /// <summary>Kind that indexes the client's stage object: a custom point and its action.</summary>
    private const byte RoomStateKind = (byte)RoomRecordKind.CustomPoint;

    /// <summary>
    /// The first record's point, as its own six little-endian sixteens:
    /// <c>(900, 900, -706)</c>, written twice in the body.
    /// </summary>
    private static readonly byte[] FirstRecordedPoint = [0x84, 0x03, 0x84, 0x03, 0x3e, 0xfd];

    /// <summary>The second record's point: <c>(-706, 893, 775)</c>.</summary>
    private static readonly byte[] SecondRecordedPoint = [0x3e, 0xfd, 0x7d, 0x03, 0x07, 0x03];

    /// <summary>Low byte of the sixteen-bit tail both records close on.</summary>
    private const byte RoomStateTailLow = 0xfd;

    /// <summary>High byte of that tail.</summary>
    private const byte RoomStateTailHigh = 0x00;

    /// <summary>
    /// Length of either room-state record that rides beside the close pair: a
    /// twenty-two-byte roster-shaped record, the room's own stage.
    /// </summary>
    /// <remarks>
    /// <b>The stage is a pair of these and nothing else.</b> The port client's
    /// briefing writes two of them — `briefing-then-solo-game` t+9.780 at ordinals
    /// <c>11</c> and <c>12</c> — where an older reading of `mgo2-game1` took a
    /// twenty-two-byte record with two fifty-four-byte ones behind it. Both of that
    /// capture's records are this length, in its briefing and again in the round
    /// that follows it (t+147.650, ordinals <c>29</c> and <c>30</c>), and no capture
    /// of a working host holds a roster-shaped fifty-four-byte record at all.
    /// </remarks>
    public const int RoomStateRecordLength = 22;

    /// <summary>
    /// The room's own stage as the first of its two room-state records states it:
    /// the record the captures write behind the close pair, in the round's opening
    /// run and again through a briefing.
    /// </summary>
    /// <remarks>
    /// The values are the recording's, and they are the stage's rather than the
    /// session's: <c>briefing-then-solo-game</c> writes the same pair at t+9.780,
    /// t+11.579, t+13.379 and again in the round's opening run at t+147.650, byte
    /// for byte, and <c>mgo2-game2</c> writes other points on the stage it plays
    /// (<c>docs/analysis/briefing-room-state.md</c> §1). The client hands the payload
    /// to its stage object's kind-<c>0x03</c> node, whose module is
    /// <c>.\online\supervisor\cp\sv_custom_point.cc</c>: its own strings log
    /// <c>"action %d"</c>, <c>"over action %d"</c> and <c>"over custom point %d"</c> and
    /// it bounds-checks the two collections separately, so the payload is **one custom
    /// point of the room's stage and one action on it**, the point written twice
    /// (<c>docs/analysis/roster-record-kinds.md</c> §4 and §7). What the point and the
    /// action state is still **[U]**, so it is the recording's.
    /// </remarks>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateRecord() =>
        Build(MatchRecordBodiesUtils.FirstRecordIndex, FirstRecordedPoint);

    /// <summary>The second record of the room's stage, at its own point.</summary>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateRecordSecond() =>
        Build(MatchRecordBodiesUtils.SecondRecordIndex, SecondRecordedPoint);

    /// <summary>
    /// Builds one record of the pair: its index, that index as the payload's tag,
    /// the stage's point written twice and the sixteen-bit tail.
    /// </summary>
    /// <param name="index">The record's index within its pair.</param>
    /// <param name="point">The point the recording states, six bytes.</param>
    /// <returns>The record body.</returns>
    private static byte[] Build(byte index, ReadOnlySpan<byte> point) =>
        MatchRecordBodiesUtils.RosterShapedRecord(
            RoomStateRecordLength,
            RoomStateKind,
            index,
            index,
            [
                .. point,
                .. point,
                RoomStateTailLow,
                RoomStateTailHigh,
            ]);
}
