using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the assignment packets. Each is parsed by the client at fixed offsets
/// rather than by length, so the size and the placement of every field are
/// asserted together — a record that is short is not a truncated display, it is
/// the next record being read from the previous one's bytes.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventAssignmentTests
{
    private static EventSnapshot CreateTeam(
        int teamIdentifier,
        int leaderCharacterIdentifier,
        int streak = 0,
        int reward = 0)
    {
        var team = new EventSnapshot
        {
            SnapshotIdentifier = teamIdentifier,
            Sequence = 3,
            State = EventConstants.TeamJoinableState,
            Name = $"TEAM{teamIdentifier}",
            LobbyIdentifier = 12,
            MatchType = EventConstants.SurvivalSelector,
            EventIdentifier = EventConstants.TransientEventIdentifier,
            ConsecutiveWins = streak,
            PaidReward = reward,
        };

        team.Participants[0].CharacterIdentifier = leaderCharacterIdentifier;
        team.Participants[0].Name = $"LEADER{leaderCharacterIdentifier}";
        team.Participants[0].State = EventConstants.ParticipantReadyState;
        team.Participants[0].Experience = 500;
        return team;
    }

    [Fact]
    public void Match_found_is_53_bytes_and_is_recipient_relative()
    {
        var ownTeam = CreateTeam(7, 42, streak: 2);
        var opponentTeam = CreateTeam(9, 77, streak: 3);

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteMatchFound(writer, 500, 4, ownTeam, opponentTeam, 42);
        var payload = writer.Build();

        Assert.Equal(EventConstants.MatchFoundWireSize, payload.Length);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(EventConstants.TransientEventIdentifier, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));
        Assert.Equal(EventConstants.ActiveEventAssignedState, payload[10]);

        // The first identity is the recipient's own character, not the team's
        // leader: the client compares it with the local character to decide which
        // cached name is the opponent.
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(11)));
        Assert.Equal("TEAM7", StringUtility.ReadFixedString(payload, 15, 16));
        Assert.Equal(2, payload[31]);
        Assert.Equal(77, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(32)));
        Assert.Equal("TEAM9", StringUtility.ReadFixedString(payload, 36, 16));
        Assert.Equal(3, payload[52]);
    }

    [Fact]
    public void Match_found_refuses_a_recipient_outside_its_team()
    {
        var ownTeam = CreateTeam(7, 42);
        var opponentTeam = CreateTeam(9, 77);

        Assert.Throws<InvalidOperationException>(
            () => EventAssignmentUtils.WriteMatchFound(
                new PacketWriter(),
                500,
                4,
                ownTeam,
                opponentTeam,
                recipientCharacterIdentifier: 0));
    }

    [Fact]
    public void State_update_carries_the_global_and_participant_states()
    {
        var team = CreateTeam(7, 42);
        team.State = 9;
        team.Participants[1].CharacterIdentifier = 43;
        team.Participants[1].State = EventConstants.ParticipantPendingState;

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteStateUpdate(writer, 500, 4, team);
        var payload = writer.Build();

        Assert.Equal(EventConstants.StateUpdateWireSize, payload.Length);
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4)));
        Assert.Equal(9, payload[6]);

        // Eight slot bytes follow, with an empty slot written as a neutral zero
        // rather than whatever state it happened to hold.
        Assert.Equal(EventConstants.ParticipantReadyState, payload[7]);
        Assert.Equal(EventConstants.ParticipantPendingState, payload[8]);
        Assert.Equal(0, payload[9]);
        Assert.Equal(0, payload[14]);
    }

    [Fact]
    public void Event_game_initialize_carries_both_rosters()
    {
        var ownTeam = CreateTeam(7, 42, streak: 2);
        var opponentTeam = CreateTeam(9, 77, streak: 3);

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteEventGameInitialize(writer, ownTeam, opponentTeam);
        var payload = writer.Build();

        Assert.Equal(EventConstants.EventGameInitializeWireSize, payload.Length);
        Assert.Equal(12, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(EventConstants.SurvivalSelector, payload[4]);
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(6)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(10)));

        // Fourteen header bytes, then thirty-two bytes of identifiers per team.
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(14)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(18)));
        Assert.Equal(77, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(46)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(50)));
    }

    [Fact]
    public void Event_game_host_initialize_carries_the_environment()
    {
        var ownTeam = CreateTeam(7, 42);
        var opponentTeam = CreateTeam(9, 77);
        var environment = EventHostEnvironment.CreateDefault();

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteEventGameHostInitialize(
            writer,
            hostCharacterIdentifier: 99,
            ownTeam,
            opponentTeam,
            environment);
        var payload = writer.Build();

        Assert.Equal(EventConstants.EventGameHostInitializeWireSize, payload.Length);
        Assert.Equal(99, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(12, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));
        Assert.Equal(EventConstants.SurvivalSelector, payload[8]);
        Assert.Equal(0, payload[18]);

        // The environment block starts at 19, byte for byte the standalone
        // encoding, and the optional trailing tuple closes the record.
        var blockWriter = new PacketWriter();
        EventHostEnvironmentUtils.Write(blockWriter, environment);
        var block = blockWriter.Build();
        Assert.Equal(EventConstants.HostEnvironmentWireSize, block.Length);
        Assert.Equal(block, payload.AsSpan(19, block.Length).ToArray());
        Assert.Equal(0, payload[223]);
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(224)));
        Assert.Equal(0, payload[226]);
    }

    [Fact]
    public void Event_game_host_initialize_requires_a_host_identity()
    {
        Assert.Throws<ArgumentException>(
            () => EventAssignmentUtils.WriteEventGameHostInitialize(
                new PacketWriter(),
                0,
                CreateTeam(7, 42),
                CreateTeam(9, 77),
                EventHostEnvironment.CreateDefault()));
    }

    [Fact]
    public void Confirmation_response_carries_the_team_reward_and_streak()
    {
        var team = CreateTeam(7, 42, streak: 5, reward: 1200);

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteConfirmationResponse(writer, 42, team);
        var payload = writer.Build();

        Assert.Equal(EventConstants.ConfirmationResponseWireSize, payload.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));
        Assert.Equal(1200, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(8)));
        Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(12)));

        // The last three cache values have no established Survival producer, so
        // they stay neutral rather than carrying invented meanings.
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(16)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(20)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(24)));
    }

    [Fact]
    public void Member_information_writes_one_record_per_roster_slot()
    {
        var team = CreateTeam(7, 42);
        team.Participants[1].CharacterIdentifier = 43;
        team.Participants[1].Name = "MEMBER";
        team.Participants[1].Experience = 250;

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteAssignedMemberInformation(writer, team);
        var payload = writer.Build();

        Assert.Equal(EventConstants.AssignedMemberInformationWireSize, payload.Length);
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload));

        // Twenty-four bytes per slot: identifier, experience, sixteen-byte name.
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(8)));
        Assert.Equal("LEADER42", StringUtility.ReadFixedString(payload, 12, 16));
        Assert.Equal(43, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(28)));
        Assert.Equal(250, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(32)));
        Assert.Equal("MEMBER", StringUtility.ReadFixedString(payload, 36, 16));

        // An empty slot carries no experience and no name, so a stale value
        // cannot be shown against it.
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(52)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(56)));
        Assert.Equal(string.Empty, StringUtility.ReadFixedString(payload, 60, 16));
    }
}
