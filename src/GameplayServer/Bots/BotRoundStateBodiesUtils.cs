namespace Mgo2Server.GameplayServer.Bots;

/// <summary>
/// The settings-payload body that puts one player into the round: the
/// <c>0b &lt;key + 1&gt; 60 …</c> record the recorded host writes for every
/// occupied key when its round opens.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the state record a bot has no client to send.</b> A player who is in
/// the room and whose client is in the map has already told the host which
/// settings it plays the round with; a bot has none, so the host writes the
/// record on its behalf. `pcapng1` t+485.74 writes sixteen of them in one frame,
/// one per occupied key from <c>02</c> to <c>17</c>, immediately before it
/// appends its bots' entries and loadouts and six seconds before its run.
/// </para>
/// <para>
/// <b>The three bots in that frame carry the same body.</b> The keys whose roster
/// entries are <c>BOT</c> entries (<c>01</c>, <c>03</c> and <c>07</c>, written at
/// t+0.562) hold these bodies:
/// </para>
/// <code>
/// 0b 02 60 04 00 00 00 80 00…00 03 00…00
/// 0b 04 60 04 00 00 00 80 00…00 03 00…00
/// 0b 08 60 04 00 00 00 80 00…00 03 00…00
/// </code>
/// <para>
/// Thirty-four bytes each, byte for byte the same behind the slot byte, which is
/// the player's key plus one — the same slot byte the loadout records carry. So
/// the body is written here with the bot's own key and nothing else varied.
/// </para>
/// <para>
/// <b>What the other bytes mean is not claimed.</b> <c>0x60</c> and <c>0x04</c>
/// open the same presence map the settings header carries
/// (<c>Rooms.SettingsHeaderBodiesUtils</c>), and the <c>0x80</c> and the
/// <c>03</c> are the recorded body's own; nothing in the capture names them, so
/// they travel as the measurements they are rather than as interpreted fields.
/// A bot is given this record because it is the record the recorded host gives
/// its bots, not because the fields are understood.
/// </para>
/// </remarks>
public static class BotRoundStateBodiesUtils
{
    /// <summary>Length of the body, in bytes.</summary>
    public const int BodyLength = 34;

    /// <summary>Offset of the slot byte: the player's key plus one.</summary>
    public const int SlotIndex = 1;

    /// <summary>Settings payload type the body opens with, <c>0x0b</c>.</summary>
    public const byte PayloadType = 0x0b;

    /// <summary>Second byte of the recorded bodies, <c>0x60</c>.</summary>
    public const byte SecondByte = 0x60;

    /// <summary>Third byte of the recorded bodies, <c>0x04</c>.</summary>
    public const byte ThirdByte = 0x04;

    /// <summary>Offset of the <c>0x80</c> the recorded bodies carry at <c>+7</c>.</summary>
    public const int FlagOffset = 0x07;

    /// <summary>
    /// Offset of the <c>03</c> the recorded bodies carry after fifteen zeros —
    /// <c>+22</c>, which is also the last byte of a twenty-one-byte map.
    /// </summary>
    public const int MarkerOffset = 0x16;

    /// <summary>
    /// One bot's round-start state: the recorded body for its own key.
    /// </summary>
    /// <param name="sessionSlot">
    /// Key the bot holds in the room's player table, written as the key plus one.
    /// </param>
    /// <returns>The thirty-four-byte body.</returns>
    public static byte[] Build(byte sessionSlot)
    {
        var body = new byte[BodyLength];
        body[0] = PayloadType;
        body[SlotIndex] = (byte)(sessionSlot + 1);
        body[2] = SecondByte;
        body[3] = ThirdByte;
        body[FlagOffset] = 0x80;
        body[MarkerOffset] = 0x03;
        return body;
    }
}
