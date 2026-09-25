using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the bracket core. Seeds are the bracket order and adjacent seeds meet,
/// so the pairings are a property of the frozen order rather than of any
/// shuffling — which is exactly what makes a restart resume the same bracket,
/// and exactly what an off-by-one would silently break.
/// </summary>
[Trait("Category", "Shared")]
public sealed class TournamentBracketTests
{
    [Fact]
    public void A_power_of_two_field_plays_adjacent_seeds_first()
    {
        var bracket = new TournamentBracketTree([1, 2, 3, 4]);

        Assert.Equal(4, bracket.Size);
        Assert.Equal(2, bracket.RoundCount);

        var firstRound = bracket.ReadyFixtures().OrderBy(fixture => fixture.Node).ToList();
        Assert.Equal(2, firstRound.Count);
        Assert.Equal((1, 2), (firstRound[0].FirstTeamIdentifier, firstRound[0].SecondTeamIdentifier));
        Assert.Equal((3, 4), (firstRound[1].FirstTeamIdentifier, firstRound[1].SecondTeamIdentifier));
        Assert.Equal(1, firstRound[0].Round);
        Assert.Equal(1, firstRound[1].Round);
    }

    [Fact]
    public void A_short_field_is_padded_and_the_seat_advances_as_a_bye()
    {
        var bracket = new TournamentBracketTree([1, 2, 3]);

        Assert.Equal(4, bracket.Size);

        // Seed three has no opponent, so it is already through to the final and
        // is not a ready fixture.
        var ready = bracket.ReadyFixtures();
        Assert.Single(ready);
        Assert.Equal((1, 2), (ready[0].FirstTeamIdentifier, ready[0].SecondTeamIdentifier));

        bracket.RecordWinner(ready[0].Node, 2);

        var final = bracket.ReadyFixtures();
        Assert.Single(final);
        Assert.Equal(2, final[0].Round);
        Assert.Equal((2, 3), (final[0].FirstTeamIdentifier, final[0].SecondTeamIdentifier));

        bracket.RecordWinner(final[0].Node, 3);
        Assert.Equal(3, bracket.Champion());
    }

    [Fact]
    public void A_field_of_one_is_its_own_champion()
    {
        var bracket = new TournamentBracketTree([7]);

        Assert.Equal(1, bracket.Size);
        Assert.Equal(0, bracket.RoundCount);
        Assert.Equal(7, bracket.Champion());
        Assert.Empty(bracket.ReadyFixtures());
    }

    [Fact]
    public void Round_numbers_count_from_the_first_round()
    {
        var bracket = new TournamentBracketTree([1, 2, 3, 4, 5, 6, 7, 8]);
        var ready = bracket.ReadyFixtures();

        Assert.Equal(4, ready.Count);
        Assert.All(ready, fixture => Assert.Equal(1, fixture.Round));

        // The four first-round nodes feed two semifinal nodes, which feed the
        // final at the root.
        Assert.Equal(2, bracket.RoundOf(3));
        Assert.Equal(2, bracket.RoundOf(2));
        Assert.Equal(3, bracket.RoundOf(1));
    }

    [Fact]
    public void A_replayed_result_changes_nothing_and_is_reported_as_such()
    {
        var bracket = new TournamentBracketTree([1, 2]);
        var fixture = bracket.ReadyFixtures()[0];

        Assert.Equal(TournamentResultOutcome.Recorded, bracket.RecordWinner(fixture.Node, 1));
        Assert.Equal(TournamentResultOutcome.Replayed, bracket.RecordWinner(fixture.Node, 1));
        Assert.Equal(1, bracket.Champion());
    }

    [Fact]
    public void A_result_that_names_neither_team_is_refused()
    {
        var bracket = new TournamentBracketTree([1, 2]);
        var fixture = bracket.ReadyFixtures()[0];

        Assert.Equal(TournamentResultOutcome.NotAFixture, bracket.RecordWinner(fixture.Node, 9));
        Assert.Equal(0, bracket.Champion());

        // The root of a four-team field has unresolved children, so a result
        // reported against it is refused rather than taken on trust. (In a
        // two-team field the root is itself the first fixture.)
        var fourTeams = new TournamentBracketTree([1, 2, 3, 4]);
        Assert.Equal(TournamentResultOutcome.NotAFixture, fourTeams.RecordWinner(1, 1));
        Assert.Equal(0, fourTeams.Champion());
    }

    [Fact]
    public void A_conflicting_second_result_for_a_played_fixture_is_refused()
    {
        var bracket = new TournamentBracketTree([1, 2]);
        var fixture = bracket.ReadyFixtures()[0];

        bracket.RecordWinner(fixture.Node, 1);
        Assert.Equal(TournamentResultOutcome.NotAFixture, bracket.RecordWinner(fixture.Node, 2));

        Assert.Equal(1, bracket.Champion());
    }

    [Fact]
    public void A_full_eight_team_bracket_resolves_to_one_champion()
    {
        var bracket = new TournamentBracketTree([1, 2, 3, 4, 5, 6, 7, 8]);

        // Seeds 1, 3, 5 and 7 win their first-round fixtures and then the
        // higher seed of each pair keeps winning.
        foreach (var round in new[] { 1, 2, 3 })
        {
            var fixtures = bracket.ReadyFixtures().OrderBy(fixture => fixture.Node).ToList();
            Assert.NotEmpty(fixtures);
            Assert.All(fixtures, fixture => Assert.Equal(round, fixture.Round));
            foreach (var fixture in fixtures)
            {
                bracket.RecordWinner(fixture.Node, fixture.FirstTeamIdentifier);
            }
        }

        Assert.Equal(1, bracket.Champion());
        Assert.Empty(bracket.ReadyFixtures());
    }

    [Fact]
    public void Duplicate_and_negative_entrants_are_refused()
    {
        Assert.Throws<ArgumentException>(() => new TournamentBracketTree([1, 1]));
        Assert.Throws<ArgumentException>(() => new TournamentBracketTree([-1]));
        Assert.Throws<ArgumentException>(() => new TournamentBracketTree([]));
    }

    [Fact]
    public void A_field_larger_than_the_client_can_draw_is_refused()
    {
        var tooMany = Enumerable.Range(1, EventConstants.BracketMaximumEntrants + 1).ToArray();

        Assert.Throws<ArgumentException>(() => new TournamentBracketTree(tooMany));
    }
}
