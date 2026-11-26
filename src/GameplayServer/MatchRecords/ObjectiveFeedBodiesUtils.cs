using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The operations stream's objective feed: <c>04 &lt;kind&gt; &lt;lobby slot&gt;</c>,
/// the record a player writes when a rule's objective moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>The body is the replay parser's own.</b> <c>rpdt.js</c> reads the same
/// three bytes as <c>[0x04, kind, lobby slot]</c> and names the kinds it has
/// measured: <c>0x00</c> a target was taken, <c>0x01</c> it was let go,
/// <c>0x02</c> a Capture target was put in a goal, <c>0x0c</c> the bomb was
/// taken, <c>0x0d</c> it was let go, <c>0x0e</c> it was planted, and
/// <c>0x12</c> a Race goal was scored. The kind is mode-dependent and the
/// slot is the carrier's lobby slot.
/// </para>
/// <para>
/// <b>A delivery is the checkpoint event.</b> The checkpoint rules are read
/// from the same put a Capture goal is, and the carrier is the player the
/// counter belongs to; the RECORD does not name a checkpoint number, so the
/// score's own dedup window is what keeps one delivery from counting twice.
/// </para>
/// </remarks>
public static class ObjectiveFeedBodiesUtils
{
    /// <summary>Opcode of the objective feed on the operations stream.</summary>
    public const byte Opcode = (byte)OperationsOpcode.ObjectiveFeed;

    /// <summary>Length of an objective-feed body: the opcode, the kind and the slot.</summary>
    public const int BodyLength = 3;

    /// <summary>A target was taken.</summary>
    public const byte TargetTakenKind = 0x00;

    /// <summary>A target was let go.</summary>
    public const byte TargetDroppedKind = 0x01;

    /// <summary>A target was put in a Capture goal, which is the checkpoint delivery.</summary>
    public const byte TargetPutKind = 0x02;

    /// <summary>The bomb was taken.</summary>
    public const byte BombTakenKind = 0x0C;

    /// <summary>The bomb was let go.</summary>
    public const byte BombDroppedKind = 0x0D;

    /// <summary>The bomb was planted.</summary>
    public const byte BombPlantedKind = 0x0E;

    /// <summary>A Race goal was scored.</summary>
    public const byte RaceGoalKind = 0x12;

    /// <summary>
    /// Whether a body is the objective feed: the opcode, the kind and the slot,
    /// and exactly three bytes.
    /// </summary>
    /// <param name="body">Body of an inbound operations record.</param>
    /// <returns><c>true</c> when the body is an objective feed.</returns>
    public static bool IsObjectiveFeed(ReadOnlySpan<byte> body) =>
        body.Length == BodyLength && body[0] == Opcode;

    /// <summary>The rule's objective event.</summary>
    /// <param name="body">The objective-feed body.</param>
    /// <returns>The kind byte.</returns>
    public static byte KindOf(ReadOnlySpan<byte> body) => body[1];

    /// <summary>The lobby slot of the player the event names.</summary>
    /// <param name="body">The objective-feed body.</param>
    /// <returns>The carrier's slot.</returns>
    public static byte SlotOf(ReadOnlySpan<byte> body) => body[2];

    /// <summary>
    /// Whether the feed is a delivery a checkpoint count reads: a Capture put or
    /// a Race goal.
    /// </summary>
    /// <param name="body">Body of an inbound operations record.</param>
    /// <returns><c>true</c> when the carrier delivered an objective.</returns>
    public static bool IsDelivery(ReadOnlySpan<byte> body) =>
        IsObjectiveFeed(body) && body[1] is TargetPutKind or RaceGoalKind;
}
