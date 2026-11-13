using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// The item catalogue every character may use: the read payload and the reply
/// to an outfit commit share it, because the client applies both to one table.
/// </summary>
/// <remarks>
/// <para>
/// <b>The catalogue is the item table, not the wardrobe.</b> The records it
/// carries are keyed by the protocol's own item id — the byte a damage record
/// carries as its weapon — so the 130 ids it holds run from the knives and
/// firearms at <c>0</c>–<c>53</c> through the pickups, the faces and the
/// <c>0xf0</c> rows. That is why the loadout screen offers every weapon and
/// the wardrobe offers every garment from one payload: the wardrobe reads its
/// own 67 ids back out of the same table and ignores the rest.
/// </para>
/// <para>
/// <b>The 130 ids and the payload's 750 bytes are measured.</b>
/// <c>.pcapngs/character-gears.pcapng</c> (the working server, 2026-11-13)
/// answers <c>0x4124</c> with 750 bytes: thirty-two zeroes, a big-endian count
/// of 130, 130 five-byte <c>{ u8 id, u32 mask }</c> records, the sixteen
/// <c>0xff</c> highlight pairs, and thirty-two bytes the parser never reads.
/// The earlier payload here carried only the 67 wardrobe ids, which is the
/// short weapon list this replaces: the client appends an item to a list only
/// when the catalogue has a record for it, and it had none for the weapons.
/// </para>
/// <para>
/// <b>Every mask is <c>0xffffffff</c>, which is what "all unlocked" means
/// here.</b> The working server's own records are almost all that value (its
/// exceptions, such as item <c>33</c> and <c>12</c>, are a half-filled account's
/// rather than a limit of the format), and
/// <c>docs/GEAR.md</c> settles what a mask indexes: a per-item slot, read only
/// for slots the item has a colour record for, so the bits above an item's
/// count are unreadable and harmless. Widening every mask therefore offers
/// every colour and every attachment the item has, and no more.
/// </para>
/// </remarks>
public static class GearCatalogue
{
    /// <summary>
    /// Bytes of zeroes the payload opens with, before the count.
    /// <para>
    /// The client's parser reaches the count thirty-two bytes into the payload,
    /// not at its first byte, which the measurement above shows directly: the
    /// count <c>00 00 00 82</c> sits at offset 32 of a 750-byte reply. A payload
    /// with the count at +0 is read as a count taken from the middle of a record,
    /// so the wardrobe comes up broken.
    /// </para>
    /// </summary>
    private const int ReservedBytes = 32;

    /// <summary>Mask written for every item: every colour and attachment it has.</summary>
    private const uint AllMask = 0xffffffff;

    /// <summary>Number of colour-highlight slots closing the records.</summary>
    private const int HighlightSlots = 16;

    /// <summary>
    /// Bytes the reference payload carries past the highlight pairs and the
    /// parser never reads — it stops at the count's records and the sixteen
    /// pairs. Zero is written rather than the reference's leftover bytes.
    /// </summary>
    private const int UnreadPadding = 32;

    /// <summary>
    /// The item ids the working server's catalogue carries, in wire order.
    /// </summary>
    /// <remarks>
    /// Measured, not derived: this is the id sequence of
    /// <c>.pcapngs/character-gears.pcapng</c>'s <c>0x4124</c>, 130 records.
    /// Ids above 128 are dropped by the client's own bound, and the reference
    /// carries them anyway; <c>134</c> appears twice, which the client writes
    /// twice into the same record and is left as measured.
    /// </remarks>
    private static readonly int[] Items =
    [
        0, 1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18, 19, 22,
        28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 40, 41, 42, 43, 44,
        46, 47, 48, 49, 50, 51, 52, 53, 57, 58, 59, 60, 61, 62, 63, 64,
        68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83,
        86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100,
        102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119,
        128, 129, 130, 131, 132, 133, 134, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143,
        160, 161, 162, 176, 192, 193, 194, 195, 196, 240, 241, 242, 243, 244,
    ];

    /// <summary>The pre-built catalogue payload, which is static.</summary>
    public static readonly byte[] Payload = BuildPayload();

    /// <summary>Builds the catalogue payload.</summary>
    public static byte[] BuildPayload()
    {
        var writer = new PacketWriter();

        writer.WritePadding(ReservedBytes);
        writer.WriteUInt32((uint)Items.Length);
        foreach (var identifier in Items)
        {
            writer.WriteUInt8((byte)identifier);
            writer.WriteUInt32(AllMask);
        }

        // The sixteen {item, bit} highlight pairs: with everything unlocked there
        // is nothing to highlight, so they keep the filler the client skips.
        for (var slot = 0; slot < HighlightSlots; slot++)
        {
            writer.WriteUInt8(0xff);
            writer.WriteUInt8(0xff);
        }

        writer.WritePadding(UnreadPadding);
        return writer.Build();
    }
}
