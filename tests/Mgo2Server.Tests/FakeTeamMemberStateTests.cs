using System.Data.Common;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the change that moves the entry-decision byte on the test players of a
/// stored team.
/// <para>
/// The rule it exists to protect is a boundary: a testing device may move a
/// test player's decision, because nobody else can, and may not move a real
/// member's, because a real member is a person deciding whether to play. The
/// name prefix is what decides, and the tests below pin both halves of it.
/// </para>
/// </summary>
[Trait("Category", "Shared")]
public sealed class FakeTeamMemberStateTests
{
    [Fact]
    public async Task A_name_no_team_holds_is_refused_without_touching_the_database()
    {
        // A blank name names no team, so the answer is a refusal rather than a
        // query that would return whichever team's row happened to match it.
        var factory = new CountingDbContextFactory();
        var service = new FakeTeamMemberStateService(factory);

        var result = await service.SetAsync(9, "   ", 2, CancellationToken.None);

        Assert.Equal(FakeTeamMemberStateOutcome.TeamNotFound, result.Outcome);
        Assert.Equal(0, factory.Created);
    }

    [Fact]
    public async Task The_change_compiles_against_the_provider()
    {
        var service = new FakeTeamMemberStateService(new UnreachableDbContextFactory());

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => service.SetAsync(9, "TESTERS", 2, CancellationToken.None));

        // The roster is read with its members and written back, so a connection
        // failure is the proof that both halves are expressible: a query that
        // named a column the provider does not have would fail before the
        // connection was used.
        Assert.IsAssignableFrom<DbException>(exception.InnerException);
    }

    [Theory]
    [InlineData("server-1", true)]
    [InlineData("SERVER-42", true)]
    [InlineData("Player", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("server", false)]
    public void The_name_prefix_is_what_marks_a_player_as_made_by_the_tools(
        string? name,
        bool expected)
    {
        Assert.Equal(expected, TestPlayerNameUtils.IsTestPlayer(name));
    }

    [Fact]
    public void A_test_member_counts_as_ready_whatever_byte_it_carries()
    {
        // This is the reason the byte could be NG without stopping a pairing,
        // and the reason the tools still offer to change it: the client paints
        // it, the queue does not read it.
        var test = Member(1_000_000_000, TestPlayerNameUtils.ComposeName(1), EventConstants.ParticipantPendingState);
        var real = Member(2, "Player", EventConstants.ParticipantPendingState);

        Assert.True(EventTeamRegistrationUtils.IsReady([test]));
        Assert.False(EventTeamRegistrationUtils.IsReady([real]));

        // A real member still has to decide for itself, so a roster of one real
        // undecided member is not ready whatever else is in it.
        Assert.False(EventTeamRegistrationUtils.IsReady([real, test]));
    }

    [Fact]
    public void An_empty_roster_is_not_ready()
    {
        // Occupied is what makes it ready, so a team whose slots are all empty
        // cannot queue however few members it fails to have.
        Assert.False(EventTeamRegistrationUtils.IsReady([]));
        Assert.False(EventTeamRegistrationUtils.IsReady(
            [Member(0, string.Empty, EventConstants.ParticipantReadyState)]));
    }

    private static Mgo2Server.Shared.Persistence.Entities.EventTeamMember Member(
        int characterIdentifier,
        string name,
        int state) =>
        new()
        {
            Slot = 0,
            CharacterIdentifier = characterIdentifier,
            Name = name,
            State = state,
        };

    /// <summary>Fails the way a database that cannot be reached fails.</summary>
    private sealed class UnreachableDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        public Mgo2DatabaseContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                .Options);
    }

    /// <summary>
    /// Counts the contexts it hands out, so a test can prove a path never
    /// reached the database at all.
    /// </summary>
    private sealed class CountingDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        /// <summary>How many contexts were asked for.</summary>
        public int Created { get; private set; }

        public Mgo2DatabaseContext CreateDbContext()
        {
            Created++;
            return new Mgo2DatabaseContext(
                new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                    .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                    .Options);
        }
    }
}
