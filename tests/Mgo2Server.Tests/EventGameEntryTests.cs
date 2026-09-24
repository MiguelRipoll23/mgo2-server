using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the game-entry information record. The client reads a fixed four
/// records whatever the count says, so the slot layout here is what decides
/// whether an entry appears at all: a key written one record off is an entry the
/// screen never shows, and a zero key is how a slot is told to be empty.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventGameEntryTests
{
    /// <summary>Where a slot begins, from the header and the slots before it.</summary>
    private static int SlotOffset(int slot) =>
        8 + (slot * EventConstants.GameEntryRecordWireSize);

    [Fact]
    public void Entry_info_is_236_bytes_and_places_its_entry_at_slot_one()
    {
        var writer = new PacketWriter();
        EventGameEntryUtils.WriteGameEntryInfo(
            writer,
            [new EventGameEntry(Key: 4242, Name: "TEAM", TeamIdentifier: 77, LobbyIdentifier: 3)]);

        var payload = writer.Build();
        Assert.Equal(EventConstants.GameEntryInfoWireSize, payload.Length);
        Assert.Equal(236, payload.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));

        // Slot zero is left empty: it is written as a zero record rather than
        // omitted, because the client reads all four slots either way.
        var emptySlot = SlotOffset(0);
        for (var offset = emptySlot; offset < emptySlot + EventConstants.GameEntryRecordWireSize; offset++)
        {
            Assert.Equal(0, payload[offset]);
        }

        // The key is the entry's occupied flag, so it has to be in the slot the
        // entry is meant to appear in.
        var slot = SlotOffset(EventConstants.GameEntryFirstSlot);
        Assert.Equal(4242, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(slot)));

        // Four unread words, then the name.
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(slot + 6)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(slot + 10)));
        Assert.Equal("TEAM", System.Text.Encoding.Latin1.GetString(payload, slot + 14, 4));

        // The team the client asks 0x4986 and 0x491B about, and the lobby whose
        // ordinal it shows as the entry's location.
        Assert.Equal(77, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(slot + 30)));
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(slot + 51)));
    }

    [Fact]
    public void No_entry_is_four_empty_slots_with_a_zero_count()
    {
        var writer = new PacketWriter();
        EventGameEntryUtils.WriteGameEntryInfo(writer, []);

        var payload = writer.Build();
        Assert.Equal(EventConstants.GameEntryInfoWireSize, payload.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));

        // The all-zero reply is the correct "no pending entries" answer, not a
        // placeholder: a zero key is what an empty slot means.
        for (var offset = 8; offset < payload.Length; offset++)
        {
            Assert.Equal(0, payload[offset]);
        }
    }

    [Fact]
    public void An_entry_that_does_not_fit_a_slot_is_refused()
    {
        // Three is the capacity once slot zero is left alone, so a fourth entry
        // has no row to appear in and would be silently dropped by the client.
        Assert.Equal(3, EventGameEntryUtils.Capacity);

        var entries = Enumerable.Range(1, EventGameEntryUtils.Capacity + 1)
            .Select(index => new EventGameEntry(index, string.Empty, index, 1))
            .ToList();

        Assert.Throws<ArgumentException>(
            () => EventGameEntryUtils.WriteGameEntryInfo(new PacketWriter(), entries));
    }

    [Fact]
    public void A_lobby_identifier_that_is_not_a_u16_is_refused()
    {
        // Truncating would name a different lobby, which is worse than refusing
        // to write the entry at all.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EventGameEntryUtils.WriteGameEntryInfo(
                new PacketWriter(),
                [new EventGameEntry(1, string.Empty, 1, 0x10000)]));
    }

    [Fact]
    public void Entries_fill_the_slots_in_order_after_the_first()
    {
        var writer = new PacketWriter();
        EventGameEntryUtils.WriteGameEntryInfo(
            writer,
            [
                new EventGameEntry(11, string.Empty, 1, 3),
                new EventGameEntry(22, string.Empty, 2, 3),
                new EventGameEntry(33, string.Empty, 3, 3),
            ]);

        var payload = writer.Build();
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));
        Assert.Equal(11, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(SlotOffset(1))));
        Assert.Equal(22, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(SlotOffset(2))));
        Assert.Equal(33, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(SlotOffset(3))));
    }
}
