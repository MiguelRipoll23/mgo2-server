using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the active-event records. Two of them carry a variable number of
/// per-item state bytes and never repeat their count on the wire, so the size
/// assertions here are the only thing standing between a wrong count and a
/// client that reads the following record out of this one's tail.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventActiveEventTests
{
    private static byte[] CreateParticipantStates() =>
        [1, 2, 0, 0, 0, 0, 0, 0];

    [Fact]
    public void Active_event_state_is_229_bytes_and_places_its_fields()
    {
        var environment = EventHostEnvironment.CreateDefault();
        var writer = new PacketWriter();
        EventActiveEventUtils.WriteActiveEventState(
            writer,
            activeStateIdentifier: 500,
            sequence: 7,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            globalState: EventConstants.ActiveEventAssignedState,
            CreateParticipantStates(),
            environment,
            baseTimeSeconds: 1_700_000_000,
            flagByte: 1,
            detailByte: 2);

        var payload = writer.Build();
        Assert.Equal(EventConstants.ActiveEventStateWireSize, payload.Length);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));
        Assert.Equal(EventConstants.ActiveEventAssignedState, payload[10]);
        Assert.Equal(1, payload[11]);
        Assert.Equal(2, payload[12]);
        Assert.Equal(0, payload[18]);

        // The environment follows the participant states, then the base time and
        // the two bytes whose meaning is not established.
        var blockWriter = new PacketWriter();
        EventHostEnvironmentUtils.Write(blockWriter, environment);
        var block = blockWriter.Build();
        Assert.Equal(block, payload.AsSpan(19, block.Length).ToArray());
        Assert.Equal(1_700_000_000, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(223)));
        Assert.Equal(1, payload[227]);
        Assert.Equal(2, payload[228]);
    }

    [Fact]
    public void Active_event_state_refuses_an_incomplete_roster()
    {
        Assert.Throws<ArgumentException>(
            () => EventActiveEventUtils.WriteActiveEventState(
                new PacketWriter(),
                500,
                7,
                EventConstants.TransientEventIdentifier,
                EventConstants.ActiveEventAssignedState,
                [1, 2],
                EventHostEnvironment.CreateDefault(),
                0,
                0,
                0));
    }

    [Fact]
    public void Active_event_state_refuses_a_zero_active_state_identifier()
    {
        Assert.Throws<ArgumentException>(
            () => EventActiveEventUtils.WriteActiveEventState(
                new PacketWriter(),
                0,
                7,
                EventConstants.TransientEventIdentifier,
                EventConstants.ActiveEventAssignedState,
                CreateParticipantStates(),
                EventHostEnvironment.CreateDefault(),
                0,
                0,
                0));
    }

    [Fact]
    public void Event_detail_length_follows_its_item_states()
    {
        var itemStates = new byte[] { 1, 0, 1 };
        var detailFields = new ushort[EventConstants.EventDetailU16Count];
        detailFields[0] = 4;
        detailFields[2] = (ushort)itemStates.Length;

        var writer = new PacketWriter();
        EventActiveEventUtils.WriteEventDetail(
            writer,
            activeStateIdentifier: 500,
            sequence: 7,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            globalState: 3,
            uiContext: 4,
            detailFields,
            detailValue: 9,
            itemStates,
            flagBits: 5,
            detailByte: 6,
            trailingValue: 10,
            new int[EventConstants.EventDetailValueCount],
            new byte[EventConstants.EventDetailMetadataCount],
            trailingU16: 11);

        var payload = writer.Build();
        Assert.Equal(EventConstants.EventDetailBaseWireSize + itemStates.Length, payload.Length);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));
        Assert.Equal(3, payload[10]);
        Assert.Equal(4, payload[11]);

        // Nine u16 fields from offset 12, the third of which the client reads as
        // the item-state count.
        Assert.Equal(4, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(12)));
        Assert.Equal(itemStates.Length, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(16)));
        Assert.Equal(9, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(30)));

        // The item states sit immediately after that value, so a count that
        // disagrees with them shifts every later field.
        Assert.Equal(itemStates, payload.AsSpan(34, itemStates.Length).ToArray());
        Assert.Equal(5, payload[37]);
        Assert.Equal(6, payload[38]);
        Assert.Equal(10, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(39)));
    }

    [Fact]
    public void Event_detail_refuses_a_count_that_disagrees_with_its_states()
    {
        var detailFields = new ushort[EventConstants.EventDetailU16Count];
        detailFields[2] = 5;

        Assert.Throws<ArgumentException>(
            () => EventActiveEventUtils.WriteEventDetail(
                new PacketWriter(),
                500,
                7,
                EventConstants.TransientEventIdentifier,
                3,
                4,
                detailFields,
                0,
                new byte[] { 1, 2 },
                0,
                0,
                0,
                new int[EventConstants.EventDetailValueCount],
                new byte[EventConstants.EventDetailMetadataCount],
                0));
    }

    [Fact]
    public void Row_records_differ_in_their_numbering_and_their_prefix()
    {
        var bitset = new int[EventConstants.EventRowWordCount];
        bitset[0] = 0x0f;
        var itemStates = new byte[] { 1 };

        var resyncWriter = new PacketWriter();
        EventActiveEventUtils.WriteRowResync(
            resyncWriter,
            activeStateIdentifier: 500,
            sequence: 7,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            globalState: 3,
            uiContext: 4,
            detailField: 2,
            oneBasedRow: 1,
            bitset,
            itemStates);

        var resync = resyncWriter.Build();
        Assert.Equal(EventConstants.EventRowResyncBaseWireSize + itemStates.Length, resync.Length);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(resync));
        // Six prefix bytes, then the event identifier, the two context bytes,
        // the detail field and the row number before the bitset.
        Assert.Equal(2, BinaryPrimitives.ReadUInt16BigEndian(resync.AsSpan(12)));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16BigEndian(resync.AsSpan(14)));
        Assert.Equal(0x0f, BinaryPrimitives.ReadInt32BigEndian(resync.AsSpan(16)));
        Assert.Equal(itemStates[0], resync[48]);

        var updateWriter = new PacketWriter();
        EventActiveEventUtils.WriteRowUpdate(
            updateWriter,
            EventConstants.TransientEventIdentifier,
            zeroBasedRow: 0,
            bitset,
            itemStates);

        var update = updateWriter.Build();
        Assert.Equal(EventConstants.EventRowUpdateBaseWireSize + itemStates.Length, update.Length);
        Assert.Equal(EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadInt32BigEndian(update));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(update.AsSpan(4)));

        // A row update carries no active-state prefix, which is why its first
        // four bytes are the event identifier rather than an active state.
        Assert.Equal(0x0f, BinaryPrimitives.ReadInt32BigEndian(update.AsSpan(6)));
    }

    [Fact]
    public void Row_resync_refuses_a_zero_based_row()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EventActiveEventUtils.WriteRowResync(
                new PacketWriter(),
                500,
                7,
                EventConstants.TransientEventIdentifier,
                3,
                4,
                2,
                oneBasedRow: 0,
                new int[EventConstants.EventRowWordCount],
                []));
    }

    [Fact]
    public void Event_list_item_is_47_bytes_and_places_its_fields()
    {
        var writer = new PacketWriter();
        EventActiveEventUtils.WriteEventListItem(
            writer,
            index: 3,
            entityIdentifier: 42,
            primaryName: "TEAM",
            rowState: 1,
            discardedByte: 9,
            leaderName: "LEADER",
            opaqueByte: 8,
            memberCount: 4,
            statusFlags: 3,
            averageExperience: 1234);

        var payload = writer.Build();
        Assert.Equal(EventConstants.EventListItemWireSize, payload.Length);
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(payload));
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(2)));
        Assert.Equal("TEAM", StringUtility.ReadFixedString(payload, 6, 16));
        Assert.Equal(1, payload[22]);
        Assert.Equal(9, payload[23]);
        Assert.Equal("LEADER", StringUtility.ReadFixedString(payload, 24, 16));
        Assert.Equal(8, payload[40]);
        Assert.Equal(4, payload[41]);
        Assert.Equal(3, payload[42]);
        Assert.Equal(1234, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(43)));
    }

    [Fact]
    public void Remaining_records_keep_their_recovered_sizes()
    {
        var boundary = new PacketWriter();
        EventActiveEventUtils.WriteEventListBoundary(boundary, EventConstants.TransientEventIdentifier);
        Assert.Equal(EventConstants.EventListBoundaryWireSize, boundary.Build().Length);

        var result = new PacketWriter();
        EventRecordUtils.WriteEventResult(
            result,
            activeStateIdentifier: 500,
            sequence: 7,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            new int[EventConstants.EventRowWordCount],
            signedResult: 1,
            tailA: 2,
            tailB: 3,
            tailC: 4);
        Assert.Equal(EventConstants.EventResultWireSize, result.Build().Length);

        var eventState = new PacketWriter();
        EventRecordUtils.WriteEventState(
            eventState,
            activeStateIdentifier: 500,
            sequence: 7,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            globalState: 3,
            CreateParticipantStates());
        Assert.Equal(EventConstants.EventStateWireSize, eventState.Build().Length);

        var rosterState = new PacketWriter();
        EventRecordUtils.WriteRosterState(
            rosterState,
            activeStateIdentifier: 500,
            sequence: 7,
            globalState: 3,
            CreateParticipantStates());
        Assert.Equal(EventConstants.RosterStateWireSize, rosterState.Build().Length);

        var counterPair = new PacketWriter();
        EventRecordUtils.WriteCounterPair(counterPair, EventConstants.TransientEventIdentifier, 1, 2);
        Assert.Equal(EventConstants.CounterPairWireSize, counterPair.Build().Length);

        var prefixed = new PacketWriter();
        EventRecordUtils.WritePrefixedValue(prefixed, 500, 7, 42);
        Assert.Equal(EventConstants.PrefixedValueWireSize, prefixed.Build().Length);

        var advance = new PacketWriter();
        EventRecordUtils.WriteAdvance(advance, 500, 7, 42, 1, 2);
        Assert.Equal(EventConstants.AdvanceWireSize, advance.Build().Length);

        var sequence = new PacketWriter();
        EventRecordUtils.WriteSequenceUpdate(sequence, 500, 7, 8);
        Assert.Equal(EventConstants.SequenceUpdateWireSize, sequence.Build().Length);
    }

    [Fact]
    public void Assigned_game_detail_is_401_bytes_and_carries_the_environment()
    {
        var environment = EventHostEnvironment.CreateDefault();
        var writer = new PacketWriter();
        EventAssignmentUtils.WriteAssignedGameDetail(
            writer,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            lobbyIdentifier: 12,
            matchType: EventConstants.SurvivalSelector,
            activationTimeSeconds: 1_700_000_000,
            gameName: "ROOM",
            environment);

        var payload = writer.Build();
        Assert.Equal(EventConstants.AssignedGameDetailWireSize, payload.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));

        // The roster size is written as the participant count rather than as a
        // magic number, because the client sizes a table from it.
        Assert.Equal(0, payload[8]);
        Assert.Equal(EventConstants.SnapshotParticipantCount, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(9)));

        Assert.Equal(1_700_000_000, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(27)));
        Assert.Equal(12, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(31)));
        Assert.Equal(EventConstants.SurvivalSelector, payload[35]);
        Assert.Equal("ROOM", StringUtility.ReadFixedString(payload, 37, 64));

        var blockWriter = new PacketWriter();
        EventHostEnvironmentUtils.Write(blockWriter, environment);
        var block = blockWriter.Build();
        Assert.Equal(block, payload.AsSpan(135, block.Length).ToArray());
        Assert.Equal(EventConstants.AssignedGameDetailWireSize, 135 + block.Length + 62);
    }
}
