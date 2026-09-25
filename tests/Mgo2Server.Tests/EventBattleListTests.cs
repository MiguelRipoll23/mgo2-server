using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the Survival battle-list stream. The start marker repeats the event
/// settings and rewards, so its size is asserted together with the row size.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventBattleListTests
{
    private static EventSnapshot CreateTeam()
    {
        var team = new EventSnapshot
        {
            SnapshotIdentifier = 21,
            State = EventConstants.TeamJoinableState,
            Name = "ALPHA",
            LobbyIdentifier = 12,
            MatchType = EventConstants.SurvivalSelector,
            HostIdentifier = 5,
            ConsecutiveWins = 2,
            PaidReward = 150,
        };
        team.Participants[0].CharacterIdentifier = 5;
        team.Participants[0].Name = "LEADER";
        team.Participants[0].Experience = 400;
        team.Participants[1].CharacterIdentifier = 6;
        team.Participants[1].Name = "MATE";
        team.Participants[1].Experience = 600;
        return team;
    }

    [Fact]
    public void Start_marker_is_270_bytes()
    {
        var writer = new PacketWriter();
        EventBattleListUtils.WriteStart(
            writer,
            EventConstants.TransientEventIdentifier,
            EventHostEnvironment.CreateDefault(),
            [100, 200, 300],
            participationReward: 50);

        var start = writer.Build();

        Assert.Equal(EventConstants.BattleListStartWireSize, start.Length);
        Assert.Equal(EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadInt32BigEndian(start));
    }

    [Fact]
    public void Team_row_is_53_bytes_and_places_its_fields()
    {
        var team = CreateTeam();
        var writer = new PacketWriter();
        EventBattleListUtils.WriteItem(writer, rowIndex: 3, team, averageExperience: 500);

        var row = writer.Build();

        Assert.Equal(EventConstants.BattleListItemWireSize, row.Length);
        Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(row));
        Assert.Equal(21, BinaryPrimitives.ReadInt32BigEndian(row.AsSpan(2)));
        Assert.Equal("ALPHA", StringUtility.ReadFixedString(row, 6, 16));
        Assert.Equal("LEADER", StringUtility.ReadFixedString(row, 25, 16));
        Assert.Equal(2, row[42]);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(row.AsSpan(44)));
        Assert.Equal(2, row[48]);
        Assert.Equal(150, BinaryPrimitives.ReadInt32BigEndian(row.AsSpan(49)));
    }

    [Fact]
    public void Average_experience_ignores_empty_slots()
    {
        var team = CreateTeam();

        Assert.Equal(500, EventBattleListUtils.AverageParticipantExperience(team));
    }

    [Fact]
    public void Average_experience_of_an_empty_team_is_zero()
    {
        Assert.Equal(0, EventBattleListUtils.AverageParticipantExperience(new EventSnapshot()));
    }
}
