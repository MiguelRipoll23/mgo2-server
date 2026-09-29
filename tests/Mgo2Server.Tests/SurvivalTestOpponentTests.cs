using Microsoft.Extensions.Logging.Abstractions;
using Mgo2Server.GameLobbyServer.Commands.Game.Chat;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the rule the self-test's second half turns on: only a team a run armed
/// and the queue has frozen is served, and only once. A run that served a team
/// twice would put a second simulated opponent into the field, and one that
/// served a team whose player had not frozen it yet would pair an opponent with
/// a team the client is still showing as joinable.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class SurvivalTestOpponentTests
{
    [Fact]
    public void An_armed_team_is_served_once()
    {
        var service = Create();

        service.ExpectOpponent(42);

        Assert.True(service.TakeExpectedOpponent(42));
        Assert.False(service.TakeExpectedOpponent(42));
    }

    [Fact]
    public void A_team_no_run_armed_is_never_served()
    {
        Assert.False(Create().TakeExpectedOpponent(42));
    }

    [Fact]
    public void Reset_forgets_the_teams_a_previous_run_armed()
    {
        var service = Create();
        service.ExpectOpponent(42);

        service.Reset();

        Assert.False(service.TakeExpectedOpponent(42));
    }

    [Theory]
    [InlineData(EventConstants.TeamRegisteredState, true)]
    [InlineData(EventConstants.TeamJoinableState, false)]
    [InlineData(0, false)]
    public void Only_a_team_waiting_in_the_field_is_frozen(int state, bool expected)
    {
        var team = new EventTeam { Name = "TEAM", State = state };

        Assert.Equal(expected, SurvivalTestOpponentService.IsFrozen(team));
    }

    [Fact]
    public void A_team_that_does_not_exist_is_not_frozen()
    {
        Assert.False(SurvivalTestOpponentService.IsFrozen(null));
    }

    /// <summary>
    /// A service whose dependencies are deliberately unusable: the two rules
    /// above are answered without a team, a queue or a connection.
    /// </summary>
    private static SurvivalTestOpponentService Create() =>
        new(
            new CharacterMemoryService(),
            teamService: null!,
            matchmakingService: null!,
            assignmentService: null!,
            sessionHelper: null!,
            NullLogger<SurvivalTestOpponentService>.Instance);
}
