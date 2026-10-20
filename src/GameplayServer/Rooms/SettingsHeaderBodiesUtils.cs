namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The twenty-four-byte settings header the recorded host writes for a player in
/// the answer to a request to be placed in the room.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is.</b> The settings payload type (<c>0x0b</c>, the byte the burst's
/// rotation and loadout records open with), the player's slot and the player's own
/// state behind them: `mgo2-game-started-on-briefing` t+93.127,
/// `mgo2-game2` t+78.987 and `mgo2-started-on-solo-game` t+101.405 write
/// <c>0b SS 02 00 …</c> as the last record of the answer.
/// </para>
/// <para>
/// <b><c>SS</c> is the player's key plus one</b>, the slot byte the burst's loadout
/// records carry (<see cref="LoadoutBodiesUtils"/>) and not the raw table key the
/// entry writes: `started-on-solo-game`'s request is answered for the player in
/// table key <c>03</c>, whose entry reads <c>07 12 00 04 … 03 …</c> and whose
/// header opens <c>0b 04</c>.
/// </para>
/// <para>
/// <b>The bytes behind the slot are the player's own and only partly measured.</b>
/// The third byte is <c>0x02</c> in every copy written through a briefing — the
/// two answers above and the seven per-player copies
/// `mgo2-game-started-on-briefing` writes at t+130.494, 156.885, 198.371, 215.499
/// and 235.930 — and this builder writes the briefing's value. The copies whose
/// tail is <c>… 01 00 02</c> carry <c>0x00</c> there instead, and they are a family
/// of their own (`mgo2-game-joined-when-red-team-then-briefing` t+307.917,
/// `mgo2-joined-game-in-progress-last-round` t+169.567), not a phase. The bytes
/// between are zero in every copy; the last byte moves per player and per copy
/// (<c>0x00</c> and <c>0x01</c> for the same slot in the same briefing), so it is a
/// per-player value this host has no source for and is written zero. **[V]** the
/// type, the length, the slot byte and the third byte; **[U]** the trailing byte,
/// and the tail family above.
/// </para>
/// <para>
/// <b>It is written in a briefing's answer and not in a round's.</b> A round's
/// answer carries the player's entry and the roster close, and the reference writes
/// the header beside them in one of its three (`started-on-solo-game` t+101.405)
/// and not the other two; a briefing's answer carries it in both captures that hold
/// one. See the analysis file's "what is not written" section for the rest.
/// </para>
/// </remarks>
public static class SettingsHeaderBodiesUtils
{
    /// <summary>Body length of the settings header, as measured.</summary>
    public const int PayloadLength = 24;

    /// <summary>Type byte the body opens with: the settings payload type.</summary>
    public const byte SettingsPayloadType = PlayerSettingsBurstService.SettingsPayloadType;

    /// <summary>Offset of the player's slot byte.</summary>
    public const int SlotIndex = 1;

    /// <summary>
    /// Offset of the byte the briefing's copies carry <c>0x02</c> at.
    /// </summary>
    public const int PhaseIndex = 2;

    /// <summary>The value <see cref="PhaseIndex"/> carries through a briefing.</summary>
    public const byte BriefingPhase = 0x02;

    /// <summary>
    /// A player's settings header: the settings payload type, the player's slot,
    /// the briefing's own byte and a zero-filled tail.
    /// </summary>
    /// <param name="slot">
    /// The player's key plus one, the slot byte the burst's loadout records carry
    /// (the room's table is zero-based, <see cref="RoomRosterService.SessionSlotCount"/>).
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] Build(byte slot)
    {
        var body = new byte[PayloadLength];
        body[0] = SettingsPayloadType;
        body[SlotIndex] = slot;
        body[PhaseIndex] = BriefingPhase;
        return body;
    }
}
