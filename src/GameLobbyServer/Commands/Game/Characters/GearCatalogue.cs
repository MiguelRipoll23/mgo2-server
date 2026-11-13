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
/// carries as its weapon — so the ids below run from the knives and firearms at
/// <c>0</c> through the launchers, the grenades, the placeables and the pickups,
/// and on into the <c>0xf0</c> rows. The wardrobe reads its own 67 ids back out
/// of the same table and ignores the rest.
/// </para>
/// <para>
/// <b>The reference payload is one account's owned ids, not the whole table.</b>
/// <c>.pcapngs/character-gears.pcapng</c> answers <c>0x4124</c> with 750 bytes
/// and 130 records, and the ids it carries are exactly the items <em>that</em>
/// account held: it has no record for the Desert Eagle (<c>8</c>, <c>9</c>), the
/// THOR (<c>10</c>), the P90 (<c>20</c>), the BIZON (<c>21</c>), the SKORPION
/// (<c>23</c>), the M4 (<c>24</c>), the AK102 (<c>25</c>), the G3A3 (<c>26</c>),
/// the AN94 (<c>27</c>), the VSS (<c>39</c>), the RAILGUN (<c>45</c>), the stun,
/// chaff and smoke grenades (<c>54</c>–<c>56</c>), the sleep claymore and both C4s
/// (<c>65</c>–<c>67</c>), the scope and dot sight (<c>84</c>, <c>85</c>) or the
/// XM8 Long (<c>101</c>) — which is what a short weapon list looks like when that
/// list is copied from one player's table. Twenty-nine of the ids below 128 are
/// absent from it, and a client shown that table offers exactly the rest.
/// </para>
/// <para>
/// <b>Every id below 128 is served, and the tail above it is the measured
/// one.</b> The client's parser refuses a record whose id exceeds 128
/// (<c>0xD3CF00</c>, <c>cmplwi r9,128; bgt</c>), so <c>0</c>–<c>127</c> is the
/// whole space a record can hold and serving all of it offers every weapon,
/// attachment, grenade and placeable the item table names. The ids above 128
/// are the reference's own tail — the wardrobe's faces and the <c>0xf0</c> rows
/// — and are carried as measured even though the parser drops them, because
/// they cost nothing and a build that reads them would need them.
/// </para>
/// <para>
/// <b>Every mask is <c>0xffffffff</c>, which is what "all unlocked" means
/// here.</b> The working server's own records are almost all that value, and
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
    /// count of the reference's 750-byte reply sits at offset 32. A payload with
    /// the count at +0 is read as a count taken from the middle of a record, so
    /// the wardrobe comes up broken.
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

    /// <summary>Highest item id a record can carry: the parser refuses one above it.</summary>
    private const int HighestRecordedIdentifier = 127;

    /// <summary>
    /// The ids above <see cref="HighestRecordedIdentifier"/> the reference
    /// carries, in wire order: its face, pickup and <c>0xf0</c> rows.
    /// </summary>
    private static readonly int[] TailItems =
    [
        128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143,
        160, 161, 162, 176,
        192, 193, 194, 195, 196,
        240, 241, 242, 243, 244,
    ];

    /// <summary>The item ids the catalogue carries, in wire order.</summary>
    /// <remarks>
    /// Every id from zero to <see cref="HighestRecordedIdentifier"/>, which is
    /// the space a record can hold, then the measured tail above it.
    /// </remarks>
    private static readonly int[] Items = BuildItemList();

    /// <summary>The pre-built catalogue payload, which is static.</summary>
    public static readonly byte[] Payload = BuildPayload();

    /// <summary>Builds the id list: the whole recordable space, then the tail.</summary>
    /// <returns>The ids, ascending within each part.</returns>
    private static int[] BuildItemList()
    {
        var items = new List<int>(HighestRecordedIdentifier + 1 + TailItems.Length);
        for (var identifier = 0; identifier <= HighestRecordedIdentifier; identifier++)
        {
            items.Add(identifier);
        }

        items.AddRange(TailItems);
        return [.. items];
    }

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
