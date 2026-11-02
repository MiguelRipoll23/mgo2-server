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
    /// The values are the recording's: two captures that share no session write one
    /// point byte for byte and a third another, so the field is the room's stage
    /// rather than the session. The client hands the payload to its stage object's
    /// kind-<c>0x03</c> node, and the port names that module <c>sv_custom_point</c> —
    /// one point, one action (<c>docs/analysis/roster-record-kinds.md</c> §4 and
    /// §7). What the point states is still **[U]**, so it is the recording's.
    /// </remarks>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateRecord() =>
        MatchRecordBodiesUtils.RosterShapedRecord(
            RoomStateRecordLength,
            RoomStateKind,
            MatchRecordBodiesUtils.FirstRecordIndex,
            MatchRecordBodiesUtils.FirstRecordIndex,
            Payload(x: 900, y: 900, z: -706));

    /// <summary>The second record of the room's stage, at its own point.</summary>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateRecordSecond() =>
        MatchRecordBodiesUtils.RosterShapedRecord(
            RoomStateRecordLength,
            RoomStateKind,
            MatchRecordBodiesUtils.SecondRecordIndex,
            MatchRecordBodiesUtils.SecondRecordIndex,
            Payload(x: -706, y: 893, z: 775));

    /// <summary>
    /// The room-state payload: the point, the same point again as the action's
    /// second side, and the sixteen-bit tail.
    /// </summary>
    /// <param name="x">Coordinate along the axis the points separate on.</param>
    /// <param name="y">Height of the point.</param>
    /// <param name="z">The remaining horizontal coordinate.</param>
    /// <returns>The payload bytes behind the record's tag.</returns>
    private static byte[] Payload(short x, short y, short z) =>
    [
        .. Point(x, y, z),
        .. Point(x, y, z),
        RoomStateTailLow,
        RoomStateTailHigh,
    ];

    /// <summary>One point of the room's stage: x, y and z as little-endian sixteens.</summary>
    /// <param name="x">Coordinate along the axis the points separate on.</param>
    /// <param name="y">Height of the point.</param>
    /// <param name="z">The remaining horizontal coordinate.</param>
    /// <returns>The six bytes of the point.</returns>
    private static byte[] Point(short x, short y, short z) =>
    [
        (byte)x, (byte)(x >> 8),
        (byte)y, (byte)(y >> 8),
        (byte)z, (byte)(z >> 8),
    ];
}
