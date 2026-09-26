using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the row of the Tournament/Survival browse list. The client reads the
/// whole list out of one stream and sizes its array from the payload, so a row
/// that is one byte out desyncs every row after it rather than failing loudly.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventScheduleListTests
{
    private static EventSchedule Create() =>
        new()
        {
            Identifier = 7,
            Name = "Survival Night 3",
            LobbySubtype = EventConstants.TournamentRegistrationSelector,
            Enabled = true,
            PublishStart = 1_700_000_000,
            PublishEnd = 1_700_003_600,
            TeamCapacity = 8,
        };

    [Fact]
    public void An_event_row_is_98_bytes_and_places_its_fields()
    {
        var writer = new PacketWriter();
        EventScheduleListUtils.WriteItem(writer, Create());

        var payload = writer.Build();
        Assert.Equal(EventConstants.EventScheduleListItemWireSize, payload.Length);
        Assert.Equal(7, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(0, payload[4]);
        Assert.Equal("Survival Night 3", StringUtility.ReadFixedString(payload, 5, 64));

        // Eight capacity/remainder halves, then the window and the two bytes
        // whose meaning is not established.
        Assert.Equal(8, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(69)));
        Assert.Equal(48, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(71)));
        Assert.Equal(48, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(81)));
        Assert.Equal(8, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(83)));
        Assert.Equal(1_700_000_000, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(85)));
        Assert.Equal(1_700_003_600, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(89)));
    }

    [Fact]
    public void An_event_name_longer_than_the_field_is_truncated_not_shifted()
    {
        var schedule = Create();
        schedule.Name = new string('A', 90);

        var writer = new PacketWriter();
        EventScheduleListUtils.WriteItem(writer, schedule);

        Assert.Equal(EventConstants.EventScheduleListItemWireSize, writer.Build().Length);
    }
}
