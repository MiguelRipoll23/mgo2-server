namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing ready record: the operations body a client writes when a player
/// changes its briefing state to ready, and the record a round waits for.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the <c>0x1f</c> record and not the payload report.</b> A client writes
/// two different things around a briefing, and the captures separate them:
/// <c>e1 &lt;slot&gt;</c> on the round's payload identifier, with its one-byte
/// companion, is what the game sends after a loading screen — into a briefing and
/// again into a round, long before anyone has readied — and this is the one the
/// player's own state change produces. <c>mgo2-game-joined-when-red-team-then-briefing</c>
/// joins at t+57.656 and readies at t+112.750; its second briefing
/// reports at t+870.222 and readies at t+844.858; the solo capture reports at
/// t+54.918 and readies at t+165.780, with the player's own note ("i changed my
/// state to ready") between the two.
/// </para>
/// <para>
/// <b>The byte behind the opcode is the player's own slot, and the last byte is
/// the state.</b> Every capture's record is three bytes — <c>1f 05 01</c>, where
/// the room's player is slot 5 — and the state <c>01</c> is the ready the host
/// publishes back: the briefing countdown's set grows by <c>1 &lt;&lt; slot</c> at
/// the ready and the whole set is written with the zero when the room is complete
/// (<see cref="BriefingCountdownBodiesUtils"/>). In
/// <c>joined-when-red-team</c> the client that readied last is the one whose bit
/// is added by the zero — <c>0b 00 00 ea 01 …</c> adds bit 5 to
/// <c>0b 16 00 ca 01 …</c> — which is the measurement behind
/// <see cref="Rooms.BriefingStateService.ReadyMask"/>.
/// </para>
/// <para>
/// What the state byte means beyond "ready" is **[U]**: no capture here holds a
/// player taking its ready back, so only <see cref="ReadyState"/> is named and any
/// other value is left to the reader.
/// </para>
/// </remarks>
public static class BriefingReadyBodiesUtils
{
    /// <summary>Opcode of the briefing ready record, in its first body byte.</summary>
    public const byte ReadyOpcode = 0x1f;

    /// <summary>Length of the ready body: the opcode, the slot and the state.</summary>
    public const int RecordLength = 3;

    /// <summary>Offset of the player's own lobby slot.</summary>
    public const int SlotIndex = 1;

    /// <summary>Offset of the briefing state the player is reporting.</summary>
    public const int StateIndex = 2;

    /// <summary>The state byte a player reports when it is ready to play.</summary>
    public const byte ReadyState = 0x01;

    /// <summary>
    /// Whether a body is a briefing ready record: the opcode at the length every
    /// capture sends.
    /// </summary>
    /// <param name="body">Body of an inbound operations record.</param>
    /// <returns><c>true</c> when the body is a player's briefing state.</returns>
    public static bool IsBriefingReady(ReadOnlySpan<byte> body) =>
        body.Length == RecordLength && body[0] == ReadyOpcode;

    /// <summary>The lobby slot the record names, which is the reporting player's own.</summary>
    /// <param name="body">Body of a briefing ready record.</param>
    /// <returns>The slot byte.</returns>
    public static byte SlotOf(ReadOnlySpan<byte> body) => body[SlotIndex];

    /// <summary>The briefing state the record reports.</summary>
    /// <param name="body">Body of a briefing ready record.</param>
    /// <returns>The state byte.</returns>
    public static byte StateOf(ReadOnlySpan<byte> body) => body[StateIndex];

    /// <summary>Whether the record reports the ready state.</summary>
    /// <param name="body">Body of a briefing ready record.</param>
    /// <returns><c>true</c> when the player has readied.</returns>
    public static bool IsReady(ReadOnlySpan<byte> body) => body[StateIndex] == ReadyState;
}
