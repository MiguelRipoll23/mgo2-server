using System.Buffers.Binary;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the next-match card. It is the wider sibling of the match-found
/// notification: the same recipient-relative identity rule, plus the lobby
/// triple and the full eight character identifiers per block that the shared
/// ladder record renders.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventNextMatchCardTests
{
    private static EventSnapshot CreateTeam(
        int teamIdentifier,
        int leaderCharacterIdentifier,
        int streak = 0)
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
            PrimaryEquipmentType = 7,
        };

        team.Participants[0].CharacterIdentifier = leaderCharacterIdentifier;
        team.Participants[0].Name = $"LEADER{leaderCharacterIdentifier}";
        team.Participants[0].State = EventConstants.ParticipantReadyState;
        team.Participants[0].Experience = 500;
        return team;
    }

    [Fact]
    public void Next_match_card_echoes_the_event_identity_and_carries_both_blocks()
    {
        var ownTeam = CreateTeam(7, 42, streak: 2);
        var opponentTeam = CreateTeam(9, 77, streak: 3);

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteNextMatchCard(writer, 500, ownTeam, opponentTeam, 42);
        var payload = writer.Build();

        Assert.Equal(EventConstants.NextMatchCardWireSize, payload.Length);

        // The card identity must echo the identity the active event opened with,
        // because the client drops a card that does not match it.
        Assert.Equal(500, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(12, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4)));
        Assert.Equal(EventConstants.SurvivalSelector, payload[8]);
        Assert.Equal(7, payload[9]);

        // Survival reads the pair as the two participants' win counts.
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(10)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(14)));

        // The first block is the recipient's own team, the second the opponent:
        // each is its identity, the sixteen-byte name and the eight roster ids.
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(18)));
        Assert.Equal("TEAM7", StringUtility.ReadFixedString(payload, 22, 16));
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(38)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(66)));
        Assert.Equal(77, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(70)));
        Assert.Equal("TEAM9", StringUtility.ReadFixedString(payload, 74, 16));
        Assert.Equal(77, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(90)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(118)));

        // The rotation selector closes the record, matching the host cache.
        Assert.Equal(EventConstants.EventGameRotationIndex, payload[122]);
    }

    [Fact]
    public void Next_match_card_names_the_frozen_roster_rather_than_the_live_team()
    {
        var ownTeam = CreateTeam(7, 42);
        var opponentTeam = CreateTeam(9, 77);

        // The live teams have moved on since the draw: a member has left, and
        // the team has been renamed. The card must still name the roster the
        // bracket was settled with, or it describes a team nobody matched.
        ownTeam.Participants[1].CharacterIdentifier = 43;
        ownTeam.Name = "RENAMED7";

        var ownRoster = new EventRoster(7, "FROZEN7", [42, 43]);
        var opponentRoster = new EventRoster(9, "FROZEN9", [77, 88, 99]);

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteNextMatchCard(
            writer,
            500,
            ownTeam,
            opponentTeam,
            42,
            ownRoster,
            opponentRoster);
        var payload = writer.Build();

        Assert.Equal(EventConstants.NextMatchCardWireSize, payload.Length);

        // Both blocks carry the frozen name and the frozen member list, in the
        // same layout the live snapshot would have filled.
        Assert.Equal("FROZEN7", StringUtility.ReadFixedString(payload, 22, 16));
        Assert.Equal(42, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(38)));
        Assert.Equal(43, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(42)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(46)));

        Assert.Equal("FROZEN9", StringUtility.ReadFixedString(payload, 74, 16));
        Assert.Equal(77, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(90)));
        Assert.Equal(88, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(94)));
        Assert.Equal(99, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(98)));
    }

    [Fact]
    public void Next_match_card_refuses_a_missing_identity()
    {
        Assert.Throws<ArgumentException>(
            () => EventAssignmentUtils.WriteNextMatchCard(
                new PacketWriter(),
                0,
                CreateTeam(7, 42),
                CreateTeam(9, 77),
                42));

        Assert.Throws<InvalidOperationException>(
            () => EventAssignmentUtils.WriteNextMatchCard(
                new PacketWriter(),
                500,
                CreateTeam(7, 42),
                CreateTeam(9, 77),
                recipientCharacterIdentifier: 0));
    }
}