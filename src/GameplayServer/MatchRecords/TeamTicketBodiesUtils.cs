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
/// down by one per death of that side, from its twenty-ticket allotment. Two
/// families of record carry it: <c>0b 00 00 00 08 …</c> for raw team <c>0</c>
/// (<c>19</c> at t+40.85, <c>18</c> at t+45.61, <c>17</c> at t+52.01, <c>16</c> at
/// t+53.75, <c>15</c> at t+83.21, <c>14</c> at t+89.01) and
/// <c>0b 00 00 00 40 …</c> for raw team <c>1</c> (<c>19</c> at t+55.26, <c>18</c>
/// at t+80.75, <c>17</c> at t+87.88). The room's own body states the allotment,
/// twenty, as the pair at <c>0x5a</c>/<c>0x5e</c>.
/// </para>
/// <para>
/// <b>It is the number the ticket panel reads.</b> A ticket is spent when a player
/// respawns, and the host is the side that knows a death happened; a room that
/// writes no record here shows every side its starting allotment for the whole
/// round, which is the room of 2026-11-11: forty for both sides, with seven kills
/// in the feed and neither count moved.
/// </para>
/// <para>
/// The two side markers are the capture's own and are recorded rather than
/// derived: <c>0x08</c> on the team the room stores as <c>0</c> and <c>0x40</c>
/// on the one it stores as <c>1</c>. Nothing else on the channel names a side with
/// a word this small, and the count is what the panel is measured to move.
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

    /// <summary>Offset of the side's marker byte.</summary>
    public const int SideOffset = 4;

    /// <summary>Offset of the remaining count, a little-endian sixteen-bit word.</summary>
    public const int RemainingOffset = 9;

    /// <summary>Marker the capture writes for raw team <c>0</c>.</summary>
    public const byte FirstSide = 0x08;

    /// <summary>Marker the capture writes for raw team <c>1</c>.</summary>
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
        body[SideOffset] = team == 0 ? FirstSide : SecondSide;

        var left = (ushort)Math.Clamp(remaining, 0, ushort.MaxValue);
        body[RemainingOffset] = (byte)(left & 0xff);
        body[RemainingOffset + 1] = (byte)(left >> 8);
        return body;
    }
}
