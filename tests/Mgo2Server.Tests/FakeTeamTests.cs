using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the in-memory teams and the players added to them. A fake team is what
/// a moderator tests a list or a roster with, so its roster has to be complete
/// and its snapshot has to carry every slot — a team that listed its leader and
/// no players is the exact bug the command exists to test for.
/// </summary>
[Trait("Category", "Shared")]
public sealed class FakeTeamTests
{
    /// <summary>
    /// The in-memory paths never reach the database, so a factory that is never
    /// asked for a context is enough here.
    /// </summary>
    private static FakeTeamService CreateService() => new(null!);

    [Fact]
    public void A_created_team_holds_its_leader_and_every_player_it_was_given()
    {
        var service = CreateService();

        var team = service.CreateTeam(
            mode: EventConstants.SurvivalSelector,
            lobbyIdentifier: 9,
            eventIdentifier: EventConstants.TransientEventIdentifier,
            teamName: "TESTERS",
            playerPrefix: "Sim",
            playerCount: 4);

        Assert.NotNull(team);
        Assert.Equal(4, team.Members.Count);
        Assert.Equal(team.Members[0].CharacterIdentifier, team.OwnerCharacterIdentifier);

        var snapshot = team.BuildSnapshot();
        Assert.Equal(4, snapshot.OccupiedParticipantCount());
        Assert.Equal(team.Members[0].Name, snapshot.HostName);
        Assert.Equal("Sim 1", snapshot.Participants[0].Name);
        Assert.Equal("Sim 4", snapshot.Participants[3].Name);

        // The team is open, so its roster carries members that have not decided:
        // the client refuses any other state on 0x4918 and the players never show.
        Assert.Equal(EventConstants.ParticipantPendingState, snapshot.Participants[0].State);
    }

    [Fact]
    public void A_team_with_no_name_gets_a_composed_one()
    {
        var service = CreateService();

        var team = service.CreateTeam(4, 9, 1, string.Empty, string.Empty, 1);

        Assert.NotNull(team);
        Assert.StartsWith("FAKE ", team.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filling_an_in_memory_team_adds_to_its_own_roster()
    {
        var service = CreateService();
        var created = service.CreateTeam(4, 9, 1, "TESTERS", string.Empty, 1);

        var result = await service.FillTeamAsync(9, "testers", 3, string.Empty, CancellationToken.None);

        Assert.Equal(FakeTeamFillOutcome.Filled, result.Outcome);
        Assert.True(result.InMemory);
        Assert.Equal(created!.Identifier, result.TeamIdentifier);
        Assert.Equal(new[] { 1, 2, 3 }, result.AddedSlots);
        Assert.Equal(4, created.Members.Count);
        Assert.Equal(4, result.Snapshot!.OccupiedParticipantCount());
    }

    [Fact]
    public async Task Filling_a_full_in_memory_team_reports_it_full()
    {
        var service = CreateService();
        service.CreateTeam(4, 9, 1, "TESTERS", string.Empty, FakeTeamService.MaximumPerTeam);

        var result = await service.FillTeamAsync(9, "TESTERS", 1, string.Empty, CancellationToken.None);

        Assert.Equal(FakeTeamFillOutcome.TeamFull, result.Outcome);
        Assert.Empty(result.AddedSlots);
    }

    [Fact]
    public async Task A_request_with_no_team_is_refused_without_looking_anywhere()
    {
        var service = CreateService();

        var result = await service.FillTeamAsync(9, "  ", 2, string.Empty, CancellationToken.None);

        Assert.Equal(FakeTeamFillOutcome.TeamNotFound, result.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(FakeTeamService.MaximumPerTeam + 1)]
    public async Task A_request_outside_what_a_team_holds_is_refused(int count)
    {
        var service = CreateService();

        var result = await service.FillTeamAsync(9, "TESTERS", count, string.Empty, CancellationToken.None);

        Assert.Equal(FakeTeamFillOutcome.TeamNotFound, result.Outcome);
    }

    [Fact]
    public void A_lobby_lists_only_the_in_memory_teams_of_its_own()
    {
        var service = CreateService();
        service.CreateTeam(EventConstants.SurvivalSelector, 9, 1, "SURVIVAL", string.Empty, 2);
        service.CreateTeam(EventConstants.SurvivalSelector, 9, 1, "ALSO SURVIVAL", string.Empty, 1);
        service.CreateTeam(EventConstants.TournamentSelector, 10, 1, "TOURNAMENT", string.Empty, 1);

        var listed = service.ListTeams(9);

        Assert.Equal(["SURVIVAL", "ALSO SURVIVAL"], listed.Select(team => team.Name));
    }

    [Fact]
    public void A_removed_team_is_forgotten_with_its_players()
    {
        var service = CreateService();
        service.CreateTeam(EventConstants.SurvivalSelector, 9, 1, "TESTERS", "Sim", 2);

        var removed = service.RemoveTeam(9, "  testers  ");

        Assert.NotNull(removed);
        Assert.Equal("TESTERS", removed.Name);
        Assert.Empty(service.ListTeams(9));

        // The players go with the team: an identifier left behind in the fake
        // range would still be a player a roster could name.
        Assert.Empty(service.ListPlayers());
    }

    [Fact]
    public void Removing_a_team_of_another_lobby_or_a_name_nobody_holds_removes_nothing()
    {
        var service = CreateService();
        service.CreateTeam(EventConstants.SurvivalSelector, 9, 1, "TESTERS", string.Empty, 1);

        Assert.Null(service.RemoveTeam(10, "TESTERS"));
        Assert.Null(service.RemoveTeam(9, "NOBODY"));
        Assert.Null(service.RemoveTeam(9, "   "));
        Assert.Single(service.ListTeams(9));
    }

    [Fact]
    public async Task A_roster_or_state_change_leaves_the_serial_the_clients_hold_alone()
    {
        var service = CreateService();
        var team = service.CreateTeam(
            EventConstants.SurvivalSelector, 9, 1, "TESTERS", "Sim", 1);
        var listed = team!.BuildSnapshot().Sequence;

        var filled = await service.FillTeamAsync(9, "TESTERS", 3, "Sim", CancellationToken.None);
        service.SetState(9, "TESTERS", EventConstants.TeamRegisteredState, 0);

        // Every 0x4918 about this team is gated on the u16 in its head matching
        // the serial the client cached from the reply that listed the team, and
        // a mismatch is dropped with no dialog. So neither filling the roster nor
        // moving the team may move the serial: that is what an added player
        // being invisible looks like from the server side.
        Assert.Equal(FakeTeamFillOutcome.Filled, filled.Outcome);
        Assert.Equal(listed, filled.Snapshot!.Sequence);
        Assert.Equal(listed, team.BuildSnapshot().Sequence);
    }
}
