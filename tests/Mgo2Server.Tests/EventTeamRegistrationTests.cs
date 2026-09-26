using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the queue state a Survival team is marked with. A team that has
/// decided to play must leave the joinable list, and one that leaves the queue
/// must come back to it — the list is served from this state by another process,
/// so a team left marked as waiting is invisible to everyone else.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventTeamRegistrationTests
{
    [Fact]
    public void Queued_state_is_not_the_joinable_one()
    {
        Assert.Equal(EventConstants.TeamRegisteredState, EventTeamRegistrationUtils.QueuedState);
        Assert.NotEqual(EventConstants.TeamJoinableState, EventTeamRegistrationUtils.QueuedState);
    }

    [Fact]
    public void Releasing_a_waiting_team_makes_it_joinable_again()
    {
        Assert.Equal(
            EventConstants.TeamJoinableState,
            EventTeamRegistrationUtils.ReleasedState(EventConstants.TeamRegisteredState));
    }

    [Theory]
    [InlineData(EventConstants.TeamJoinableState)]
    [InlineData(0)]
    [InlineData(7)]
    public void Releasing_a_team_that_never_queued_writes_nothing(int currentState)
    {
        Assert.Null(EventTeamRegistrationUtils.ReleasedState(currentState));
    }

    [Fact]
    public void A_waiting_ready_team_of_the_same_lobby_can_be_paired()
    {
        Assert.True(EventTeamRegistrationUtils.IsPairableCandidate(
            EventConstants.TeamRegisteredState,
            isReady: true,
            lobbyIdentifier: 12,
            expectedLobbyIdentifier: 12));
    }

    [Fact]
    public void A_team_that_was_released_is_not_paired()
    {
        Assert.False(EventTeamRegistrationUtils.IsPairableCandidate(
            EventConstants.TeamJoinableState,
            isReady: true,
            lobbyIdentifier: 12,
            expectedLobbyIdentifier: 12));
    }

    [Fact]
    public void A_team_that_is_not_ready_is_not_paired()
    {
        Assert.False(EventTeamRegistrationUtils.IsPairableCandidate(
            EventConstants.TeamRegisteredState,
            isReady: false,
            lobbyIdentifier: 12,
            expectedLobbyIdentifier: 12));
    }

    [Fact]
    public void A_team_of_another_lobby_is_not_paired()
    {
        Assert.False(EventTeamRegistrationUtils.IsPairableCandidate(
            EventConstants.TeamRegisteredState,
            isReady: true,
            lobbyIdentifier: 13,
            expectedLobbyIdentifier: 12));
    }
}
