using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the rule that decides who won from what the players reported. It is
/// reached only when a native terminal report did not arrive, so it is the path
/// a mistake would hide in longest — and a wrong winner advances the wrong team
/// through a bracket.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventOutcomeTests
{
    private static EventSnapshot CreateTeam(params int[] characterIdentifiers)
    {
        var team = new EventSnapshot
        {
            SnapshotIdentifier = 7,
            Sequence = 1,
            State = EventConstants.TeamJoinableState,
            LobbyIdentifier = 12,
            MatchType = EventConstants.SurvivalSelector,
            EventIdentifier = EventConstants.TransientEventIdentifier,
        };

        for (var index = 0; index < characterIdentifiers.Length; index++)
        {
            team.Participants[index].CharacterIdentifier = characterIdentifiers[index];
            team.Participants[index].State = EventConstants.ParticipantReadyState;
        }

        return team;
    }

    private static EventStatReport Report(
        int characterIdentifier,
        int roundsWon = 0,
        bool aborted = false,
        int score = 0,
        int kills = 0,
        int deaths = 0) =>
        new()
        {
            MatchIdentifier = 1,
            CharacterIdentifier = characterIdentifier,
            RoundsWon = roundsWon,
            Aborted = aborted,
            Score = score,
            Kills = kills,
            Deaths = deaths,
        };

    private static Dictionary<int, EventStatReport> Reports(params EventStatReport[] reports) =>
        reports.ToDictionary(report => report.CharacterIdentifier);

    [Fact]
    public void A_team_with_a_member_still_to_report_is_incomplete()
    {
        var team = CreateTeam(1, 2);

        var aggregate = EventOutcomeUtils.Aggregate(team, Reports(Report(1, roundsWon: 1)));

        Assert.False(aggregate.Complete);
        Assert.Equal(1, aggregate.RoundsWon);
    }

    [Fact]
    public void Empty_slots_do_not_keep_a_team_incomplete()
    {
        var team = CreateTeam(1, 2);

        var aggregate = EventOutcomeUtils.Aggregate(
            team,
            Reports(Report(1, roundsWon: 1), Report(2, roundsWon: 1)));

        Assert.True(aggregate.Complete);
        Assert.Equal(2, aggregate.RoundsWon);
    }

    [Fact]
    public void Reports_are_summed_over_the_team()
    {
        var team = CreateTeam(1, 2);

        var aggregate = EventOutcomeUtils.Aggregate(
            team,
            Reports(
                Report(1, roundsWon: 2, score: 100, kills: 5, deaths: 1),
                Report(2, roundsWon: 1, score: 50, kills: 3, deaths: 2)));

        Assert.Equal(3, aggregate.RoundsWon);
        Assert.Equal(150, aggregate.Score);
        Assert.Equal(8, aggregate.Kills);
        Assert.Equal(3, aggregate.Deaths);
    }

    [Fact]
    public void More_rounds_won_decides_the_match()
    {
        var first = new EventStatsAggregate(true, 3, 0, 0, 0, 0);
        var second = new EventStatsAggregate(true, 1, 0, 0, 0, 0);

        var result = EventOutcomeUtils.InferWinner(10, 20, first, second);

        Assert.NotNull(result);
        Assert.Equal(10, result!.Value.Winner);
        Assert.Equal(20, result.Value.Loser);
    }

    [Fact]
    public void Fewer_aborts_wins_when_the_rounds_are_equal()
    {
        var first = new EventStatsAggregate(true, 2, 2, 0, 0, 0);
        var second = new EventStatsAggregate(true, 2, 0, 0, 0, 0);

        var result = EventOutcomeUtils.InferWinner(10, 20, first, second);

        // Quitting is a disadvantage, not a tie-break in its favour.
        Assert.Equal(20, result!.Value.Winner);
    }

    [Fact]
    public void Score_then_kills_then_deaths_break_the_remaining_ties()
    {
        // Score decides.
        Assert.Equal(10, EventOutcomeUtils.InferWinner(
            10,
            20,
            new EventStatsAggregate(true, 1, 0, 500, 0, 0),
            new EventStatsAggregate(true, 1, 0, 400, 9, 0))!.Value.Winner);

        // Kills decide when the score is equal.
        Assert.Equal(10, EventOutcomeUtils.InferWinner(
            10,
            20,
            new EventStatsAggregate(true, 1, 0, 400, 9, 0),
            new EventStatsAggregate(true, 1, 0, 400, 4, 0))!.Value.Winner);

        // Fewer deaths decide when everything else is equal.
        Assert.Equal(10, EventOutcomeUtils.InferWinner(
            10,
            20,
            new EventStatsAggregate(true, 1, 0, 400, 4, 2),
            new EventStatsAggregate(true, 1, 0, 400, 4, 8))!.Value.Winner);
    }

    [Fact]
    public void Indistinguishable_reports_decide_nothing()
    {
        var identical = new EventStatsAggregate(true, 2, 0, 100, 5, 1);

        Assert.Null(EventOutcomeUtils.InferWinner(10, 20, identical, identical));

        // A draw is a draw; inventing a winner would advance the wrong team.
        Assert.Equal(0, EventOutcomeUtils.Compare(identical, identical));
    }

    [Fact]
    public void An_incomplete_team_decides_nothing_however_lopsided_it_looks()
    {
        var first = new EventStatsAggregate(false, 0, 0, 0, 0, 0);
        var second = new EventStatsAggregate(true, 9, 0, 900, 90, 0);

        Assert.Null(EventOutcomeUtils.InferWinner(10, 20, first, second));
    }

    [Fact]
    public void A_terminal_report_resolves_its_two_identities_to_teams()
    {
        // Teams 10 and 20, represented by characters 1000 and 2000.
        Assert.Equal(10, EventOutcomeUtils.ResolveTerminalWinner(10, 1000, 20, 2000, 1000, 2000));
        Assert.Equal(20, EventOutcomeUtils.ResolveTerminalWinner(10, 1000, 20, 2000, 2000, 1000));
    }

    [Fact]
    public void A_terminal_report_that_does_not_fit_the_match_resolves_to_nothing()
    {
        // Someone else entirely than the two teams of this match.
        Assert.Equal(0, EventOutcomeUtils.ResolveTerminalWinner(10, 1000, 20, 2000, 999, 2000));
        Assert.Equal(0, EventOutcomeUtils.ResolveTerminalWinner(10, 1000, 20, 2000, 1000, 999));

        // A team reported as both sides, or as neither.
        Assert.Equal(0, EventOutcomeUtils.ResolveTerminalWinner(10, 1000, 20, 2000, 1000, 1000));
        Assert.Equal(0, EventOutcomeUtils.ResolveTerminalWinner(10, 1000, 20, 2000, 0, 0));
    }

    [Fact]
    public void The_outcome_waits_for_the_grace_to_pass()
    {
        var now = DateTimeOffset.UnixEpoch.AddSeconds(100);
        var grace = TimeSpan.FromSeconds(1);

        Assert.False(EventOutcomeUtils.IsDecisionDue(now, null, grace));
        Assert.False(EventOutcomeUtils.IsDecisionDue(now, now, grace));
        Assert.True(EventOutcomeUtils.IsDecisionDue(now, now.AddSeconds(-1), grace));
    }

    [Fact]
    public void A_zero_grace_decides_as_soon_as_a_report_exists()
    {
        var now = DateTimeOffset.UnixEpoch.AddSeconds(100);

        Assert.True(EventOutcomeUtils.IsDecisionDue(now, now, TimeSpan.Zero));
    }

    [Fact]
    public void The_grace_configuration_is_validated()
    {
        var options = new Mgo2Server.Shared.Options.EventOptions { ReportGraceMilliseconds = -1 };
        Assert.Throws<InvalidOperationException>(options.Validate);

        var sweep = new Mgo2Server.Shared.Options.EventOptions { OutcomeSweepMilliseconds = 10 };
        Assert.Throws<InvalidOperationException>(sweep.Validate);
    }
}
