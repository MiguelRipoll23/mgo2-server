using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One entry of the game-entry information screen: the room a character has been
/// assigned to, or the Tournament place it holds while no room exists yet.
/// </summary>
/// <param name="Key">Key of the entry, which the client returns when removing it.</param>
/// <param name="Name">Name shown for the entry.</param>
/// <param name="TeamIdentifier">Team the entry plays for, or zero before a team is submitted.</param>
/// <param name="LobbyIdentifier">Lobby the entry belongs to.</param>
public readonly record struct EventGameEntry(
    int Key,
    string Name,
    int TeamIdentifier,
    int LobbyIdentifier);

/// <summary>
/// Codec of the game-entry information reply. The record carries four slots, and
/// the client reads all four whatever the count word says, so the unused slots
/// are written as zeroes rather than omitted: a zero key is how the client is
/// told a slot is empty.
/// <para>
/// Only three of the fifty-seven bytes have a reader in the client — the key, the
/// team identifier and the lobby identifier. The rest are parsed and never read,
/// so they are written as zeroes instead of being filled with values whose
/// meaning this server would be guessing at.
/// </para>
/// </summary>
public static class EventGameEntryUtils
{
    /// <summary>Entries the record can hold, given the slot entries start at.</summary>
    public static int Capacity => EventConstants.GameEntrySlotCount - EventConstants.GameEntryFirstSlot;

    /// <summary>Writes the reply: a success result, the count, and four records.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="entries">Entries to place, in slot order.</param>
    public static void WriteGameEntryInfo(PacketWriter writer, IReadOnlyList<EventGameEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > Capacity)
        {
            // The client's loop is a fixed four, so a fifth entry would have no
            // row and would silently not be shown.
            throw new ArgumentException(
                $"The game-entry information record holds at most {Capacity} entries.",
                nameof(entries));
        }

        var start = writer.Size;
        writer.WriteInt32(0);
        writer.WriteInt32(entries.Count);

        var slot = 0;
        for (; slot < EventConstants.GameEntryFirstSlot; slot++)
        {
            writer.WritePadding(EventConstants.GameEntryRecordWireSize);
        }

        foreach (var entry in entries)
        {
            WriteEntry(writer, entry);
            slot++;
        }

        for (; slot < EventConstants.GameEntrySlotCount; slot++)
        {
            writer.WritePadding(EventConstants.GameEntryRecordWireSize);
        }

        AssertSize(writer, start, EventConstants.GameEntryInfoWireSize, "game entry information");
    }

    private static void WriteEntry(PacketWriter writer, EventGameEntry entry)
    {
        var start = writer.Size;

        // The key is the record's own identity and its occupied flag, so a zero
        // key would present the entry as an empty slot. It is the character
        // identifier because that is what the client hands back when the entry is
        // removed, and the removal path is what reads it.
        writer.WriteInt32(entry.Key);

        // Four unread words and a byte whose meanings are not established.
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);

        writer.WriteFixedString(entry.Name ?? string.Empty, 16);

        // The team the client asks 0x4986 and 0x491B about.
        writer.WriteInt32(entry.TeamIdentifier);

        // The second name field. Nothing reads either of them, so the name is
        // written once, where a reader would meet it first.
        writer.WriteFixedString(string.Empty, 16);
        writer.WriteUInt8(0);

        // The lobby whose ordinal the client shows as this entry's location. It is
        // a u16 on the wire, and a lobby identifier that does not fit is a caller
        // bug rather than something to truncate into a different lobby.
        if (entry.LobbyIdentifier is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entry),
                $"A game-entry lobby identifier is a u16, not {entry.LobbyIdentifier}.");
        }

        writer.WriteUInt16(entry.LobbyIdentifier);

        // The trailing unread word.
        writer.WriteInt32(0);

        AssertSize(writer, start, EventConstants.GameEntryRecordWireSize, "game entry");
    }

    private static void AssertSize(PacketWriter writer, int start, int expected, string name)
    {
        var written = writer.Size - start;
        if (written != expected)
        {
            throw new InvalidOperationException(
                $"The {name} record is {written} bytes, expected {expected}.");
        }
    }
}
