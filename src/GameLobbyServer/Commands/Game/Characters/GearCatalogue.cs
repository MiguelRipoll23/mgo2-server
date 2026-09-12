using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// The gear catalogue every character may use: the read payload and the reply
/// to an outfit commit share it, because the client zeroes its gear table
/// before applying entries.
/// </summary>
public static class GearCatalogue
{
    /// <summary>Colour mask of the 21-slot camouflage family.</summary>
    private const uint CamouflageMask = 0x1fffff;

    /// <summary>Colour mask of the 10-slot solid-colour family.</summary>
    private const uint SolidMask = 0x3ff;

    /// <summary>Colour mask of the 8-slot family.</summary>
    private const uint EightSlotMask = 0xff;

    /// <summary>Colour mask of the 6-slot goggle family.</summary>
    private const uint LensSixMask = 0x3f;

    /// <summary>Colour mask of the 5-slot lens and scarf families.</summary>
    private const uint FiveSlotMask = 0x1f;

    /// <summary>Colour mask of the single-colour items.</summary>
    private const uint SingleMask = 0x1;

    /// <summary>Colour mask of the widest item in the game.</summary>
    private const uint WidestMask = 0xffffff;

    /// <summary>Colour mask of the entries that carry no colour records.</summary>
    private const uint NoColourMask = 0x0;

    /// <summary>Number of colour-highlight slots closing the payload.</summary>
    private const int HighlightSlots = 16;

    private static readonly int[] CamouflageItems =
    [
        11, 22, 29, 30, 31, 32, 34, 35, 36, 37,
        57, 58, 59, 60, 61, 62,
        69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
        87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97,
    ];

    private static readonly int[] SolidItems = [12, 13, 33, 38, 104, 108];
    private static readonly int[] EightSlotItems = [105, 109, 111];
    private static readonly int[] LensSixItems = [103];
    private static readonly int[] LensFiveItems = [106, 107];
    private static readonly int[] ScarfFiveItems = [113];
    private static readonly int[] WidestItems = [110];
    private static readonly int[] SingleItems = [46, 47, 48, 49, 50, 51, 112, 114, 115, 116];
    private static readonly int[] NoColourItems = [28, 68, 86, 102];

    /// <summary>The pre-built catalogue payload, which is static.</summary>
    public static readonly byte[] Payload = BuildPayload();

    /// <summary>Builds the catalogue payload.</summary>
    public static byte[] BuildPayload()
    {
        var writer = new PacketWriter();
        var entries = BuildEntries();

        writer.WriteUInt32((uint)entries.Count);
        foreach (var (identifier, mask) in entries)
        {
            writer.WriteUInt8(identifier);
            writer.WriteUInt32(mask);
        }

        // The 32-byte tail holds sixteen {item, bit} highlight pairs; with
        // everything unlocked there is nothing to highlight, so the slots keep
        // the filler the client provably skips.
        for (var slot = 0; slot < HighlightSlots; slot++)
        {
            writer.WriteUInt8(0xff);
            writer.WriteUInt8(0xff);
        }

        return writer.Build();
    }

    private static List<(int Identifier, uint Mask)> BuildEntries()
    {
        var entries = new List<(int, uint)>();

        Add(entries, CamouflageItems, CamouflageMask);
        Add(entries, SolidItems, SolidMask);
        Add(entries, EightSlotItems, EightSlotMask);
        Add(entries, LensSixItems, LensSixMask);
        Add(entries, LensFiveItems, FiveSlotMask);
        Add(entries, ScarfFiveItems, FiveSlotMask);
        Add(entries, WidestItems, WidestMask);
        Add(entries, SingleItems, SingleMask);
        Add(entries, NoColourItems, NoColourMask);

        entries.Sort((first, second) => first.Item1.CompareTo(second.Item1));
        return entries;
    }

    private static void Add(List<(int, uint)> entries, int[] identifiers, uint mask)
    {
        foreach (var identifier in identifiers)
        {
            entries.Add((identifier, mask));
        }
    }
}
