using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.AccountLobbyServer.Commands;

/// <summary>
/// Builds the fixed-grid payload of the character list reply (0x3049). Every size
/// below is the client's own, taken from the inline parser at 0x00f031ec of
/// MGO2.ELF: a header, eight fifty-two byte entries and the thirty-two byte
/// entitlement trailer the client copies onto session + 0x1e4.
/// </summary>
public static class CharacterListPayloadBuilder
{
    /// <summary>Size of the header: a result word, three counters and the main name.</summary>
    public const int HeaderSize = 23;

    /// <summary>
    /// Number of entries the client parses. Its loop compares the destination
    /// offset against 0x1a4 and *then* advances it by 0x3c, so the comparison runs
    /// on the offset of the entry it is about to fill: the entry at 0x1a4 is still
    /// parsed and the loop only exits on the next pass. Zero to 0x1a4 in steps of
    /// 0x3c is eight entries, which is also the size of the session array the
    /// parser clears (0x1e0 bytes = eight sixty byte records).
    /// </summary>
    public const int SlotCount = 8;

    /// <summary>Size of one entry, which the client unpacks into a sixty byte record.</summary>
    public const int EntrySize = 52;

    /// <summary>Offset of the entitlement trailer (0x1b7).</summary>
    public const int TrailerOffset = HeaderSize + (SlotCount * EntrySize);

    /// <summary>Size of the entitlement trailer.</summary>
    public const int TrailerSize = 32;

    /// <summary>Total size of the reply (0x1d7).</summary>
    public const int PayloadSize = TrailerOffset + TrailerSize;

    /// <summary>Field length of a character name.</summary>
    private const int NameLength = 16;

    /// <summary>
    /// Entitlement byte the client keeps at session + 0x1e5 (trailer index 1).
    /// Bit 0 is GENE, bit 1 MEME and bit 2 SCENE, so 0x07 owns every expansion;
    /// bit 2 is what the expansion-only lobby check at 0x0097ce60 reads.
    /// </summary>
    private const byte ExpansionEntitlements = 0x07;

    /// <summary>
    /// Entitlement byte the client keeps at session + 0x1e7 (trailer index 3).
    /// Its low bits unlock the gated entries of the codec voice packs.
    /// </summary>
    private const byte CodecEntitlements = 0x03;

    /// <summary>
    /// Slot index the client searches the entries for to find the character it
    /// plays as. The main character is served first, so it always sits in slot
    /// zero.
    /// </summary>
    private const byte MainSlotIndex = 0;

    /// <summary>One served character: the entity, its appearance and whether it is the main one.</summary>
    /// <param name="Character">Character to describe.</param>
    /// <param name="Appearance">Appearance of the character, when it has one.</param>
    /// <param name="IsMain">Whether this is the account's main character.</param>
    public sealed record Entry(Character Character, CharacterAppearance? Appearance, bool IsMain);

    /// <summary>Builds the character list reply.</summary>
    /// <param name="characterSlots">Number of slots the account owns.</param>
    /// <param name="entries">Characters to serve, main first; entries past the grid are dropped.</param>
    /// <param name="now">Moment the deletion countdown of every entry is measured against.</param>
    public static byte[] Build(int characterSlots, IReadOnlyList<Entry> entries, DateTimeOffset now)
    {
        var shownCount = Math.Min(entries.Count, SlotCount);
        var writer = new PacketWriter();
        WriteHeader(writer, characterSlots, entries, shownCount);

        for (var slotIndex = 0; slotIndex < shownCount; slotIndex++)
        {
            WriteEntry(writer, entries[slotIndex], slotIndex, now);
        }

        WriteTrailer(writer);
        return writer.Build();
    }

    /// <summary>Writes the result word, the three counters and the main character's name.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="characterSlots">Number of slots the account owns.</param>
    /// <param name="entries">Characters being served.</param>
    /// <param name="shownCount">Number of entries the grid holds.</param>
    private static void WriteHeader(PacketWriter writer, int characterSlots, IReadOnlyList<Entry> entries, int shownCount)
    {
        // Result word: the client parses the rest of the reply only when it is zero.
        writer.WriteUInt32(0);
        writer.WriteUInt8(characterSlots);
        writer.WriteUInt8(shownCount);
        writer.WriteUInt8(MainSlotIndex);
        writer.WriteFixedString(entries.Count == 0 ? string.Empty : DisplayName(entries[0]), NameLength);
    }

    /// <summary>
    /// Writes one entry: the slot index, the identifier, the name, the face bytes,
    /// the reserved word and the gear bytes.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="entry">Character to write.</param>
    /// <param name="slotIndex">Index of the slot the entry lands in.</param>
    /// <param name="now">Moment the deletion countdown is measured against.</param>
    private static void WriteEntry(PacketWriter writer, Entry entry, int slotIndex, DateTimeOffset now)
    {
        var appearance = entry.Appearance;
        writer.WriteUInt8(slotIndex);
        writer.WriteUInt32((uint)entry.Character.Identifier);
        writer.WriteFixedString(DisplayName(entry), NameLength);

        writer.WriteUInt8(appearance?.Gender ?? 0);
        writer.WriteUInt8(appearance?.Face ?? 0);
        writer.WriteUInt8(appearance?.Upper ?? 0);
        writer.WriteUInt8(appearance?.Lower ?? 0);
        writer.WriteUInt8(appearance?.FacePaint ?? 0);
        writer.WriteUInt8(appearance?.UpperColor ?? 0);
        writer.WriteUInt8(appearance?.LowerColor ?? 0);
        writer.WriteUInt8(appearance?.Voice ?? 0);
        writer.WriteUInt8(appearance?.Pitch ?? 0);
        // Reserved word the client stores at record offset 0x24 and never reads back.
        writer.WritePadding(4);

        writer.WriteUInt8(appearance?.Head ?? 0);
        writer.WriteUInt8(appearance?.Chest ?? 0);
        writer.WriteUInt8(appearance?.Hands ?? 0);
        writer.WriteUInt8(appearance?.Waist ?? 0);
        writer.WriteUInt8(appearance?.Feet ?? 0);
        writer.WriteUInt8(appearance?.Accessory1 ?? 0);
        writer.WriteUInt8(appearance?.Accessory2 ?? 0);
        writer.WriteUInt8(appearance?.HeadColor ?? 0);
        writer.WriteUInt8(appearance?.ChestColor ?? 0);
        writer.WriteUInt8(appearance?.HandsColor ?? 0);
        writer.WriteUInt8(appearance?.WaistColor ?? 0);
        writer.WriteUInt8(appearance?.FeetColor ?? 0);
        writer.WriteUInt8(appearance?.Accessory1Color ?? 0);
        writer.WriteUInt8(appearance?.Accessory2Color ?? 0);

        // Trailing word the client stores at record offset 0x38 and reads as the
        // seconds left before this character may be deleted. It draws its own wait
        // screen from the value and pre-checks the deletion against it, so serving a
        // zero here would promise a character can be deleted that the delete command
        // then refuses — the wait would surface as a bare failure with no countdown.
        writer.WriteUInt32((uint)CharacterService.SecondsUntilDeletable(entry.Character.CreatedAt, now));
    }

    /// <summary>
    /// Pads the grid to the trailer offset and writes the entitlement trailer. The
    /// trailer maps byte for byte onto session + 0x1e4, so the entitlements sit at
    /// indices 1 and 3 and every reserved byte stays zero.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    private static void WriteTrailer(PacketWriter writer)
    {
        writer.WritePadding(TrailerOffset - writer.Size);
        writer.WriteUInt8(0);
        writer.WriteUInt8(ExpansionEntitlements);
        writer.WriteUInt8(0);
        writer.WriteUInt8(CodecEntitlements);
        writer.WritePadding(TrailerSize - 4);
    }

    /// <summary>Name shown for an entry, starred when it is the main character.</summary>
    /// <param name="entry">Entry to name.</param>
    private static string DisplayName(Entry entry) =>
        entry.IsMain ? $"*{entry.Character.Name}" : entry.Character.Name;
}
