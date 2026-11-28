namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The eleven-byte record the working host writes at every death: one side's
/// remaining tickets.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on `.pcapngs/game-with-bots-workingserver.pcapng`.</b> Every death
/// in that capture is followed by a <c>0x1001</c> body
/// <c>0b 00 00 00 &lt;side&gt; 00 00 00 00 &lt;left&gt; 00</c> whose count steps
/// down by one per death of that side, from its twenty-ticket limit. Two
/// families of record carry it: <c>0b 00 00 00 08 …</c> for raw team <c>0</c>
/// (<c>19</c> at t+40.85, <c>18</c> at t+45.61, <c>17</c> at t+52.01, <c>16</c> at
/// t+53.75, <c>15</c> at t+83.21, <c>14</c> at t+89.01) and
/// <c>0b 00 00 00 40 …</c> for raw team <c>1</c> (<c>19</c> at t+55.26, <c>18</c>
/// at t+80.75, <c>17</c> at t+87.88). The room's own body states the limit,
/// twenty, as the pair at <c>0x5a</c>/<c>0x5e</c>.
/// </para>
/// <para>
/// <b>It is the number the ticket panel reads.</b> A ticket is spent when a player
/// respawns, and the host is the side that knows a death happened; a room that
/// writes no record here shows every side its starting limit for the whole
/// round, which is the room of 2026-11-11: forty for both sides, with seven kills
/// in the feed and neither count moved.
/// </para>    /// <para>
    /// <b>What the eleven bytes most plausibly are, and what is certain.</b> The first
    /// byte is the settings family's discriminator, not a patch opcode: under <c>0x1001</c>
    /// the working capture writes it on bodies of seven, eleven, twenty-four, twenty-five,
    /// twenty-six, twenty-seven and thirty-three bytes (<c>pcapng1</c>: 1 113 of them),
    /// while the player-profile record's own version is <c>0x07</c> and opens the other
    /// eighty-nine. Byte one is the address inside that family — <c>0x00</c> for the room's
    /// own, a lobby slot on the records beside it.
    /// <para>
    /// Behind that address the binary has a field-transfer layer over the operations state
    /// block: <c>P2P_GetOperationStateBlock</c> (<c>0x27e2b0</c>), <c>P2P_ReadOperationStateField</c>
    /// (<c>0x27e480</c>), <c>P2P_WriteOperationStateField</c> (<c>0x27e578</c>). A write copies the
    /// field and then sets its code to <c>3</c> in a two-bit-per-field map,
    /// <c>block[descriptor[3]][index &gt;&gt; 2] |= 3 &lt;&lt; ((index &amp; 3) &lt;&lt; 1)</c>, and raises the block's
    /// dirty word; a read accepts a field only where the block's ten-byte-entry table
    /// declares that offset at that width, and silently drops anything else. **That the
    /// eleven bytes are this map is an inference, not yet proven.** It fits — <c>0x08</c> in
    /// the fourth byte after the address is code <c>3</c> at field 13 and <c>0x40</c> is code
    /// <c>3</c> at field 15, one per side, with the count as the sixteen-bit value behind
    /// them, and it explains why the scoreboard record beside it carries a twenty-one-byte
    /// run of the same shape — but no function in the image has yet been found that packs
    /// these bytes, and until one is, the field indices are the reading that fits rather
    /// than the reading the binary states.
    /// </para>
/// </remarks>
public static class TeamTicketBodiesUtils
{
    /// <summary>Length of the body: the type, the room's slot, and eight words.</summary>
    public const int BodyLength = 11;

    /// <summary>Leading byte, the replay parser's settings discriminator.</summary>
    public const byte PayloadType = 0x0b;

    /// <summary>
    /// Slot byte the record carries: <c>0x00</c>, the room's own, which is the same
    /// slot the settings payload opens on.
    /// </summary>
    public const byte RoomSlot = 0x00;

    /// <summary>
    /// Offset of the mask byte that carries the side's transfer code: mask byte three
    /// of eight, which covers fields twelve to fifteen at two bits each.
    /// </summary>
    public const int SideOffset = 4;

    /// <summary>Offset of the remaining count, a little-endian sixteen-bit word.</summary>
    public const int RemainingOffset = 9;

    /// <summary>
    /// Code <c>3</c> at field 13, the client's first ticket panel (blue): the
    /// mask's low pair of the third byte's second nibble.
    /// </summary>
    public const byte FirstSide = 0x08;

    /// <summary>
    /// Code <c>3</c> at field 15, the client's second ticket panel (red): the top
    /// pair of the same mask byte.
    /// </summary>
    public const byte SecondSide = 0x40;

    /// <summary>
    /// The record one side's remaining tickets travels as.
    /// </summary>
    /// <param name="team">Raw team value the room's rows hold, <c>0</c> or <c>1</c>.</param>
    /// <param name="remaining">Tickets the side has left.</param>
    /// <returns>The eleven-byte body, ready to send under <c>0x1001</c>.</returns>
    public static byte[] Build(short team, int remaining)
    {
        var body = new byte[BodyLength];
        body[0] = PayloadType;
        body[1] = RoomSlot;

        // **Measured, and the room's raw id does not cross here.**
        // `game-with-bots-workingserver` writes `0x08` for raw team 0 and `0x40`
        // for raw team 1 (this class's remarks), so each side keeps its place in
        // the mask. A raw id written to the other field emptied the panel it was
        // not playing on, which is the live report of 2026-11-28.
        body[SideOffset] = team == 0 ? FirstSide : SecondSide;

        var left = (ushort)Math.Clamp(remaining, 0, ushort.MaxValue);
        body[RemainingOffset] = (byte)(left & 0xff);
        body[RemainingOffset + 1] = (byte)(left >> 8);
        return body;
    }
}
