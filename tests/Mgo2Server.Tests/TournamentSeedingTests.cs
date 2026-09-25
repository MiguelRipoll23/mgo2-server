using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the seed order. It is taken from the places the teams were given
/// rather than from a draw or from submission order, so it has to be exactly
/// reproducible: a bracket reseeded differently on a second read would pair
/// teams that never qualified against each other.
/// </summary>
[Trait("Category", "Shared")]
public sealed class TournamentSeedingTests
{
    [Fact]
    public void A_field_of_one_never_closes_because_it_has_nobody_to_play()
    {
        // Seeding it would crown an entrant that never played a fixture.
        Assert.False(TournamentSeedingUtils.EntriesClosed(1, 8, [true]));
        Assert.False(TournamentSeedingUtils.EntriesClosed(0, 8, []));
    }

    [Fact]
    public void A_full_field_closes_whatever_the_teams_have_decided()
    {
        // Nothing else could be admitted, so waiting would only delay the draw.
        Assert.True(TournamentSeedingUtils.EntriesClosed(4, 4, [false, false, false, false]));
    }

    [Fact]
    public void An_unready_team_keeps_the_field_open()
    {
        Assert.False(TournamentSeedingUtils.EntriesClosed(3, 8, [true, false, true]));
        Assert.True(TournamentSeedingUtils.EntriesClosed(3, 8, [true, true, true]));
    }

    [Fact]
    public void A_readiness_list_that_does_not_cover_the_field_is_not_consent()
    {
        // A team whose readiness was never read has not agreed to start, so the
        // field stays open rather than being closed on an incomplete read.
        Assert.False(TournamentSeedingUtils.EntriesClosed(3, 8, [true, true]));
    }

    [Fact]
    public void Seeds_follow_the_place_order_not_the_submission_order()
    {
        var seeds = TournamentSeedingUtils.OrderTeamSeeds(
        [
            new TournamentEntrant(SlotIndex: 3, TeamIdentifier: 90),
            new TournamentEntrant(SlotIndex: 0, TeamIdentifier: 10),
            new TournamentEntrant(SlotIndex: 1, TeamIdentifier: 20),
        ]);

        Assert.Equal([10, 20, 90], seeds);
    }

    [Fact]
    public void A_team_registered_twice_takes_one_place()
    {
        // A duplicate seed would pair a team with itself in the first round.
        var seeds = TournamentSeedingUtils.OrderTeamSeeds(
        [
            new TournamentEntrant(SlotIndex: 0, TeamIdentifier: 10),
            new TournamentEntrant(SlotIndex: 1, TeamIdentifier: 10),
            new TournamentEntrant(SlotIndex: 2, TeamIdentifier: 20),
        ]);

        Assert.Equal([10, 20], seeds);
    }

    [Fact]
    public void Places_without_a_team_are_not_seeded()
    {
        // A reservation is a place without a team; it has nothing to seed yet.
        var seeds = TournamentSeedingUtils.OrderTeamSeeds(
        [
            new TournamentEntrant(SlotIndex: 0, TeamIdentifier: 0),
            new TournamentEntrant(SlotIndex: 1, TeamIdentifier: 20),
        ]);

        Assert.Equal([20], seeds);
    }

    [Fact]
    public void An_empty_field_cannot_be_seeded()
    {
        Assert.Empty(TournamentSeedingUtils.OrderTeamSeeds([]));
        Assert.False(TournamentSeedingUtils.IsSeedable([]));
    }

    [Fact]
    public void A_field_of_one_is_seedable_and_so_is_a_full_one()
    {
        Assert.True(TournamentSeedingUtils.IsSeedable([10]));
        Assert.True(TournamentSeedingUtils.IsSeedable(
            Enumerable.Range(1, EventConstants.BracketMaximumEntrants).ToArray()));
    }

    [Fact]
    public void A_field_larger_than_the_client_can_draw_is_not_seedable()
    {
        var tooMany = Enumerable.Range(1, EventConstants.BracketMaximumEntrants + 1).ToArray();

        Assert.False(TournamentSeedingUtils.IsSeedable(tooMany));
    }
}
