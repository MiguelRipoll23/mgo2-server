using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the active-game snapshot and the joinable-team row. Both are parsed by
/// fixed offsets rather than by length, so the size and the field placement are
/// asserted together.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventSnapshotTests
{
    private static EventSnapshot CreateSnapshot()
    {
        var snapshot = new EventSnapshot
        {
            Result = 0,
            SnapshotIdentifier = 7,
            Sequence = 3,
            State = EventConstants.TeamJoinableState,
            Name = "TEAM",
            Comment = "COMMENT",
            FlagBits = 1,
            LobbyIdentifier = 12,
            MatchType = EventConstants.SurvivalSelector,
            HostIdentifier = 99,
            EventIdentifier = EventConstants.TransientEventIdentifier,
        };

        snapshot.Participants[0].CharacterIdentifier = 42;
        snapshot.Participants[0].Name = "LEADER";
        snapshot.Participants[0].State = EventConstants.ParticipantReadyState;
        snapshot.Participants[0].Experience = 500;
        snapshot.ParticipantStates[0] = EventConstants.ParticipantReadyState;
        snapshot.HostName = "LEADER";
        return snapshot;
    }

    [Fact]
    public void Compact_snapshot_is_441_bytes_and_places_its_fields()
    {
        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, CreateSnapshot());
        var compact = writer.Build();

        Assert.Equal(EventConstants.CompactSnapshotWireSize, compact.Length);
        Assert.Equal(7, BinaryPrimitives.ReadInt32BigEndian(compact.AsSpan(4)));
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(compact.AsSpan(8)));
        Assert.Equal("TEAM", StringUtility.ReadFixedString(compact, 11, 16));
        Assert.Equal(1, compact[155]);

        // Participant array begins at 156 with a 25-byte stride.
        var participant = EventConstants.CompactParticipantOffset;
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(compact.AsSpan(participant)));
        Assert.Equal("LEADER", StringUtility.ReadFixedString(compact, participant + 4, 16));
        Assert.Equal(EventConstants.ParticipantReadyState, compact[participant + 20]);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(compact.AsSpan(participant + 21)));

        // Lobby, match type and host follow the roster.
        var lobbyOffset = EventConstants.CompactParticipantOffset + EventConstants.SnapshotParticipantCount * EventConstants.SnapshotParticipantWireSize;
        Assert.Equal(12, BinaryPrimitives.ReadInt32BigEndian(compact.AsSpan(lobbyOffset)));
        Assert.Equal(EventConstants.SurvivalSelector, compact[lobbyOffset + 4]);
        Assert.Equal(99, BinaryPrimitives.ReadInt32BigEndian(compact.AsSpan(lobbyOffset + 10)));
    }

    [Fact]
    public void Full_snapshot_is_645_bytes()
    {
        var writer = new PacketWriter();
        EventSnapshotUtils.WriteFull(writer, CreateSnapshot());

        Assert.Equal(EventConstants.SnapshotWireSize, writer.Build().Length);
    }

    [Fact]
    public void Compact_snapshot_substitutes_the_response_identifier()
    {
        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, CreateSnapshot(), responseIdentifier: 1234);
        var compact = writer.Build();

        Assert.Equal(1234, BinaryPrimitives.ReadInt32BigEndian(compact.AsSpan(4)));
    }

    [Fact]
    public void Team_list_row_is_71_bytes()
    {
        var writer = new PacketWriter();
        EventTeamListUtils.WriteItem(writer, CreateSnapshot());
        var row = writer.Build();

        Assert.Equal(EventConstants.TeamListItemWireSize, row.Length);
        Assert.Equal(7, BinaryPrimitives.ReadInt32BigEndian(row.AsSpan(0)));
        Assert.Equal("TEAM", StringUtility.ReadFixedString(row, 4, 16));
        // Host name, then the zero word, then max players, occupied count and state.
        Assert.Equal(EventConstants.TeamRosterSize, row[42]);
        Assert.Equal(1, row[43]);
        Assert.Equal(EventConstants.TeamJoinableState, row[44]);
    }
}
