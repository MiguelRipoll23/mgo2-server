using System.Data.Common;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the projection of an in-memory team into the row that lets it be
/// paired.
/// <para>
/// This is the one moment a testing device writes, and everything after it
/// reads the team as an ordinary row: the host lease, the assignment packets,
/// the outcome inference and the reward ledger. So the projection has to be
/// right in the ways those readers assume — the identifier the client already
/// cached, the queued state the queue pairs on, and a roster that is ready
/// rather than pending, because a team queued with an undecided roster is
/// released again on the next reconcile and never plays.
/// </para>
/// </summary>
[Trait("Category", "Shared")]
public sealed class FakeTeamPairingTests
{
    private static FakeTeamService CreateFakeTeams() => new(null!);

    [Fact]
    public void The_row_keeps_the_identifier_the_client_already_cached()
    {
        var teams = CreateFakeTeams();
        var created = teams.CreateTeam(
            EventConstants.SurvivalSelector, 9, 1, "TESTERS", "Sim", 3);

        var row = FakeTeamPairingService.BuildRow(created!, 9);

        // A match packet naming a different number is dropped against the
        // client's own copy of the team, so the row has to carry the same one
        // the team was listed under.
        Assert.Equal(created!.Identifier, row.Identifier);
        Assert.Equal(created.OwnerCharacterIdentifier, row.OwnerCharacterIdentifier);
        Assert.True(row.Identifier >= FakeTeamService.FirstFakeIdentifier);
    }

    [Fact]
    public void The_row_is_queued_as_a_survival_team_of_its_own_lobby()
    {
        var teams = CreateFakeTeams();
        var created = teams.CreateTeam(
            EventConstants.SurvivalSelector, 9, 1, "TESTERS", string.Empty, 1);

        var row = FakeTeamPairingService.BuildRow(created!, 9);

        // 9 is the state the queue treats as waiting for an opponent, and the
        // match type is what the queue keys its per-lobby queues by.
        Assert.Equal(EventConstants.TeamRegisteredState, row.State);
        Assert.Equal(EventConstants.SurvivalSelector, row.MatchType);
        Assert.Equal(9, row.LobbyIdentifier);
    }

    [Fact]
    public void The_roster_is_written_ready_and_in_slot_order()
    {
        var teams = CreateFakeTeams();
        var created = teams.CreateTeam(
            EventConstants.SurvivalSelector, 9, 1, "TESTERS", "Sim", 4);

        var row = FakeTeamPairingService.BuildRow(created!, 9);

        Assert.Equal(4, row.Members.Count);
        Assert.Equal([0, 1, 2, 3], row.Members.Select(member => member.Slot));
        Assert.Equal(
            created!.Members.Select(member => member.CharacterIdentifier),
            row.Members.Select(member => member.CharacterIdentifier));

        // A pending member would fail the queue's readiness rule on the very
        // next reconcile, which releases the team instead of pairing it.
        Assert.All(row.Members, member =>
            Assert.Equal(EventConstants.ParticipantReadyState, member.State));
    }

    [Fact]
    public async Task A_tournament_lobby_refuses_to_write_a_team_out()
    {
        // A Tournament entrant is seeded into a bracket and frozen into a
        // roster. A memory-only team has neither, so writing it out would put a
        // team the bracket cannot read into real tournament state.
        var service = new FakeTeamPairingService(null!, CreateFakeTeams());

        var result = await service.MaterialiseAsync(
            9,
            "TESTERS",
            EventConstants.TournamentSelector,
            CancellationToken.None);

        Assert.Equal(FakeTeamPairingOutcome.NotSurvival, result.Outcome);
    }

    [Fact]
    public async Task A_name_no_team_holds_is_refused()
    {
        var service = new FakeTeamPairingService(null!, CreateFakeTeams());

        var result = await service.MaterialiseAsync(
            9,
            "NOBODY",
            EventConstants.SurvivalSelector,
            CancellationToken.None);

        Assert.Equal(FakeTeamPairingOutcome.TeamNotFound, result.Outcome);
    }

    [Fact]
    public async Task The_write_compiles_against_the_provider()
    {
        var teams = CreateFakeTeams();
        teams.CreateTeam(EventConstants.SurvivalSelector, 9, 1, "TESTERS", "Sim", 2);
        var service = new FakeTeamPairingService(new UnreachableDbContextFactory(), teams);

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => service.MaterialiseAsync(
                9,
                "TESTERS",
                EventConstants.SurvivalSelector,
                CancellationToken.None));

        // The database is not there, so the insert cannot be sent. It is built
        // before the connection is used, so a connection failure is the proof
        // that the row and its roster map onto real columns — the explicit
        // identifier included, which is the part a generated key would break.
        Assert.IsAssignableFrom<DbException>(exception.InnerException);
    }

    /// <summary>Fails the way a database that cannot be reached fails.</summary>
    private sealed class UnreachableDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        public Mgo2DatabaseContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                .Options);
    }
}
