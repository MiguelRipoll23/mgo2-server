using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the Tournament registration rules. They decide who may hold a place
/// and which place they get, and both are easy to get subtly wrong in a way no
/// packet size would reveal — an off-by-one in a limit excludes exactly the
/// players who sit on the boundary.
/// </summary>
[Trait("Category", "Shared")]
public sealed class TournamentRegistrationTests
{
    private static EventOptions CreateOptions(
        int minimumLevel = 0,
        int maximumLevel = 0) =>
        new()
        {
            TournamentMinimumLevel = minimumLevel,
            TournamentMaximumLevel = maximumLevel,
        };

    private static EventSchedule CreateSchedule(int teamCapacity) =>
        new()
        {
            Identifier = EventConstants.TransientEventIdentifier,
            LobbySubtype = EventConstants.TournamentRegistrationSelector,
            Enabled = true,
            TeamCapacity = teamCapacity,
        };

    [Fact]
    public void Zero_limits_accept_every_level()
    {
        var options = CreateOptions();

        Assert.True(TournamentRegistrationUtils.IsLevelEligible(1, options));
        Assert.True(TournamentRegistrationUtils.IsLevelEligible(50, options));
    }

    [Fact]
    public void Limits_are_inclusive_on_both_ends()
    {
        var options = CreateOptions(minimumLevel: 10, maximumLevel: 20);

        Assert.False(TournamentRegistrationUtils.IsLevelEligible(9, options));
        Assert.True(TournamentRegistrationUtils.IsLevelEligible(10, options));
        Assert.True(TournamentRegistrationUtils.IsLevelEligible(20, options));
        Assert.False(TournamentRegistrationUtils.IsLevelEligible(21, options));
    }

    [Fact]
    public void An_open_upper_bound_accepts_any_level_above_the_minimum()
    {
        var options = CreateOptions(minimumLevel: 10);

        Assert.False(TournamentRegistrationUtils.IsLevelEligible(9, options));
        Assert.True(TournamentRegistrationUtils.IsLevelEligible(10, options));
        Assert.True(TournamentRegistrationUtils.IsLevelEligible(1_000, options));
    }

    [Fact]
    public void Team_places_and_player_places_are_different_counts()
    {
        // A submitted team takes one team place however many players it brings,
        // while reservations are counted in players. Sharing one number would
        // either fill the bracket with one team's roster or over-admit teams.
        Assert.Equal(8, EventScheduleService.TeamCapacityOf(CreateSchedule(teamCapacity: 8)));
        Assert.Equal(
            8 * EventConstants.TeamMemberLimit,
            TournamentRegistrationUtils.PlaceCapacity(
                EventScheduleService.TeamCapacityOf(CreateSchedule(teamCapacity: 8))));
    }

    [Fact]
    public void Team_slots_are_allocated_over_team_places()
    {
        var capacity = EventScheduleService.TeamCapacityOf(CreateSchedule(teamCapacity: 3));

        Assert.Equal(0, TournamentRegistrationUtils.NextSlot([], capacity));
        Assert.Equal(1, TournamentRegistrationUtils.NextSlot([0], capacity));

        // A full field reports the capacity, which is how the submission knows to
        // refuse rather than to write a team into a place that does not exist.
        Assert.Equal(3, TournamentRegistrationUtils.NextSlot([0, 1, 2], capacity));
    }

    [Fact]
    public void Place_capacity_is_stated_in_teams_and_counted_in_players()
    {
        Assert.Equal(32 * EventConstants.TeamMemberLimit, TournamentRegistrationUtils.PlaceCapacity(32));
    }

    [Fact]
    public void A_field_states_its_own_places_rather_than_inheriting_the_configured_ones()
    {
        // Two events running at once each have their own field, so the places a
        // schedule states are counted from that schedule and not from a
        // deployment-wide bracket size the events would otherwise share.
        Assert.Equal(4 * EventConstants.TeamMemberLimit, TournamentRegistrationUtils.PlaceCapacity(4));
    }

    [Fact]
    public void Next_slot_reuses_the_lowest_released_place()
    {
        Assert.Equal(0, TournamentRegistrationUtils.NextSlot([], capacity: 8));
        Assert.Equal(2, TournamentRegistrationUtils.NextSlot([0, 1], capacity: 8));

        // A released index is filled before the tail grows, so the occupied set
        // stays contiguous.
        Assert.Equal(1, TournamentRegistrationUtils.NextSlot([0, 2, 3], capacity: 8));
    }

    [Fact]
    public void Next_slot_reports_a_full_field_by_returning_the_capacity()
    {
        Assert.Equal(4, TournamentRegistrationUtils.NextSlot([0, 1, 2, 3], capacity: 4));
    }

    [Fact]
    public void Configuration_refuses_limits_that_admit_nobody()
    {
        var options = CreateOptions(minimumLevel: 40, maximumLevel: 20);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Configuration_refuses_a_negative_limit()
    {
        var options = CreateOptions(minimumLevel: -1);

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
