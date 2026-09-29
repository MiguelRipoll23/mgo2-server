using Mgo2Server.GameLobbyServer.Commands.Game.Chat;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the two ways a team can exist: a row, and a simulated one held for a
/// test. The halves are told apart by identifier, and a member may be added to
/// either — a simulated team's own roster, or a real row the roster is padded
/// with — so a drift in the routing would put a test's player in the table or
/// make a real team invisible to the matchmaker.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventTeamMemoryTests
{
    [Fact]
    public void A_simulated_team_keeps_its_roster_and_test_range_identifiers()
    {
        var memory = new EventTeamMemoryService();
        var characters = new CharacterMemoryService();

        var team = memory.CreateTeam(
            "server-team",
            EventConstants.SurvivalSelector,
            lobbyIdentifier: 12,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            [characters.Create(), characters.Create()]);

        // A real row owns an identifier a database sequence reaches, so every
        // simulated one has to come from the test range or a lookup could not
        // route on it. It is positive, because the client resolves a roster entry
        // by the identifier it was given and a negative one means nothing to it.
        Assert.True(TestIdentifierUtils.IsTest(team.Identifier));
        Assert.True(memory.Holds(team.Identifier));
        Assert.Equal(2, team.Members.Count);
        Assert.All(team.Members, member => Assert.True(TestIdentifierUtils.IsTest(member.CharacterIdentifier)));
        Assert.All(team.Members, member => Assert.Equal(EventConstants.ParticipantReadyState, member.State));
        Assert.All(team.Members, member => Assert.True(TestPlayerNameUtils.IsTestPlayer(member.Name)));
        Assert.Equal(team.Members.First().CharacterIdentifier, team.OwnerCharacterIdentifier);
    }

    [Fact]
    public void A_member_added_to_a_real_row_is_held_beside_it()
    {
        var memory = new EventTeamMemoryService();
        var character = new CharacterMemoryService().Create();

        var member = memory.AddMember(
            teamIdentifier: 42,
            slot: 3,
            character,
            state: EventConstants.ParticipantPendingState);

        // The row is not replaced: only the simulated slot is held here, and it
        // is returned so the team service can merge it into the row's roster.
        Assert.False(memory.Holds(42));
        Assert.Null(memory.FindTeam(42));
        Assert.Equal(42, member.TeamIdentifier);
        Assert.Equal(character.Identifier, member.CharacterIdentifier);
        Assert.Equal(character.Name, member.Name);
        Assert.Equal([member], memory.MembersFor(42));
    }

    [Fact]
    public void A_simulated_member_moves_from_pending_to_ready()
    {
        var memory = new EventTeamMemoryService();
        var member = memory.AddMember(
            teamIdentifier: 42,
            slot: 2,
            new CharacterMemoryService().Create(),
            state: EventConstants.ParticipantPendingState);

        var slot = memory.SetMemberState(
            42,
            member.CharacterIdentifier,
            EventConstants.ParticipantReadyState);

        Assert.Equal(2, slot);
        Assert.Equal(EventConstants.ParticipantReadyState, member.State);
    }

    [Fact]
    public async Task A_test_character_is_decided_without_reading_the_database()
    {
        var memory = new EventTeamMemoryService();
        var member = memory.AddMember(
            teamIdentifier: 42,
            slot: 1,
            new CharacterMemoryService().Create(),
            state: EventConstants.ParticipantPendingState);

        // The factory is deliberately unusable: a simulated member must be
        // answered from memory before any context is asked for.
        var memberService = new EventTeamMemberService(null!, memory);

        var slot = await memberService.SetDecisionAsync(42, member.CharacterIdentifier, decision: 1);

        Assert.Equal(1, slot);
        Assert.Equal(EventConstants.ParticipantReadyState, member.State);
    }

    [Fact]
    public async Task A_simulated_team_is_left_without_reading_the_database()
    {
        var memory = new EventTeamMemoryService();
        var memberService = new EventTeamMemberService(null!, memory);
        var team = memory.CreateTeam(
            "server-team",
            EventConstants.SurvivalSelector,
            12,
            EventConstants.TransientEventIdentifier,
            [new CharacterMemoryService().Create()]);

        var outcome = await memberService.LeaveAsync(team.Identifier, team.OwnerCharacterIdentifier);

        Assert.Equal(EventLeaveOutcome.TeamDisbanded, outcome);
        Assert.False(memory.Holds(team.Identifier));
    }

    [Fact]
    public void FindJoinable_lists_only_this_lobby_joinable_teams()
    {
        var memory = new EventTeamMemoryService();
        var characters = new CharacterMemoryService();
        var joinable = memory.CreateTeam("Joinable", EventConstants.SurvivalSelector, 12, 1, [characters.Create()]);
        var queued = memory.CreateTeam("Queued", EventConstants.SurvivalSelector, 12, 1, [characters.Create()]);
        memory.CreateTeam("Elsewhere", EventConstants.SurvivalSelector, 13, 1, [characters.Create()]);
        memory.SetTeamState(queued.Identifier, EventConstants.TeamRegisteredState);

        var found = memory.FindJoinable(12);

        Assert.Equal([joinable], found);
    }

    [Fact]
    public void Reset_forgets_every_simulated_team_and_member()
    {
        var memory = new EventTeamMemoryService();
        var team = memory.CreateTeam(
            "server-team",
            EventConstants.SurvivalSelector,
            12,
            1,
            [new CharacterMemoryService().Create()]);
        memory.AddMember(42, 1, new CharacterMemoryService().Create(), EventConstants.ParticipantPendingState);

        memory.Reset();

        Assert.False(memory.Holds(team.Identifier));
        Assert.Empty(memory.MembersFor(42));
        Assert.Null(memory.FindOwnedInLobby(team.OwnerCharacterIdentifier, 12));
    }

    [Theory]
    [InlineData("/test", true)]
    [InlineData("  /test  ", true)]
    [InlineData("/TEST", true)]
    [InlineData("/testing", false)]
    [InlineData("test", false)]
    [InlineData("", false)]
    public void Only_the_test_command_is_recognised(string text, bool expected)
    {
        Assert.Equal(expected, SurvivalTestService.IsCommand(text));
    }
}
