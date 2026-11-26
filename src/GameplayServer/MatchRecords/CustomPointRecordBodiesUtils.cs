using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The kind-<c>0x03</c> body: one objective point of the round's stage, as the
/// room tells its peers where the target stands.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the stage's marker and not the session's.</b> The body the captures
/// hold was read as "the room's own stage" until the effect it names was found:
/// Blood Bath's two records carry <c>(900, 900, −706)</c> and
/// <c>(−706, 893, 775)</c>, which are the stage's
/// <c>PRP_RUGBY_TGT_01 (9000, 0.83, −7062.5)</c> and
/// <c>PRP_RUGBY_TGT_02 (−7062.5, −6.26, 7750)</c> divided by ten in x and z with
/// the height lifted 900 — Capture's two targets, on the round that played
/// Capture. Three more captures match their own rules' props the same way
/// (<see cref="MapCustomPoint"/>, <c>docs/analysis/custom-points.md</c> §2).
/// </para>
/// <para>
/// <b>The room writes one record per target its rule places, and none for a rule
/// that places none.</b> The two records are not a pair the run always carries:
/// Bomb and Sneaking Capture write one each, Race two, and Team Deathmatch — which
/// has no target — is written none at all. Sending the recorded pair in a mode
/// with no objectives is what put KEROTAN and GA-KO in Team Deathmatch: the client
/// spawns a target for every kind-<c>0x03</c> body it is given, wherever the body
/// says (<see cref="CustomPointUtils"/>).
/// </para>
/// <para>
/// <b>The payload's first byte is the task and the point's index in it.</b>
/// <c>(task &lt;&lt; 4) | index</c>: Capture's pair is <c>0x00</c>/<c>0x01</c>,
/// Bomb's single site <c>0x40</c>, Sneaking Capture's <c>0x50</c>, and Race's pair
/// <c>0x60</c>/<c>0x61</c>. The point follows it twice — the same three signed
/// sixteens written twice, which every captured body does — and the record closes
/// on <c>fd 00</c>, the tail every captured body carries too.
/// </para>
/// </remarks>
public static class CustomPointRecordBodiesUtils
{
    /// <summary>Length of the body on the wire: the seven-byte family header and fifteen bytes.</summary>
    public const int RecordLength = 22;

    /// <summary>Kind that indexes the client's stage object: a custom point and its action.</summary>
    private const byte CustomPointKind = (byte)RoomRecordKind.CustomPoint;

    /// <summary>Low byte of the sixteen-bit tail every captured body closes on.</summary>
    private const byte TailLow = 0xfd;

    /// <summary>High byte of that tail.</summary>
    private const byte TailHigh = 0x00;

    /// <summary>Low nibble of the payload's first byte: the point's own index.</summary>
    private const int IndexMask = 0x0f;

    /// <summary>
    /// Builds one target's body: its index and the task it belongs to as the
    /// payload's first byte, the point written twice and the tail.
    /// </summary>
    /// <param name="index">The point's index within its task, zero or one.</param>
    /// <param name="task">The task byte's high nibble (<see cref="CustomPointUtils.TaskOf"/>).</param>
    /// <param name="point">The stage's point, in the wire's own form.</param>
    /// <returns>The record body.</returns>
    public static byte[] Build(int index, byte task, MapCustomPoint point)
    {
        var tag = (byte)((task << 4) | (index & IndexMask));
        return MatchRecordBodiesUtils.RosterShapedRecord(
            RecordLength,
            CustomPointKind,
            (byte)index,
            tag,
            [
                .. PointBytes(point),
                .. PointBytes(point),
                TailLow,
                TailHigh,
            ]);
    }

    /// <summary>One point as three signed sixteens, little-endian.</summary>
    /// <param name="point">The point to write.</param>
    /// <returns>Its six bytes.</returns>
    private static byte[] PointBytes(MapCustomPoint point) =>
    [
        (byte)(point.X & 0xff),
        (byte)((point.X >> 8) & 0xff),
        (byte)(point.Y & 0xff),
        (byte)((point.Y >> 8) & 0xff),
        (byte)(point.Z & 0xff),
        (byte)((point.Z >> 8) & 0xff),
    ];
}
