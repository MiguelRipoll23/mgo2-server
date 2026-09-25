using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the rule that turns an event identifier a client named into an event
/// it may actually enter. The identifier arrives in a request, so the whole
/// burden of "is this a real, open Tournament" falls on this one predicate —
/// and every way it can be wrong is a way a player enters something that was
/// never announced.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventScheduleTests
{
    private const long Now = 1_700_000_000;

    private static EventSchedule Create(
        int subtype = EventConstants.TournamentRegistrationSelector,
        bool enabled = true,
        long publishStart = 0,
        long publishEnd = 0,
        int teamCapacity = EventConstants.BracketMaximumEntrants) =>
        new()
        {
            Identifier = EventConstants.TransientEventIdentifier,
            LobbySubtype = subtype,
            Enabled = enabled,
            PublishStart = publishStart,
            PublishEnd = publishEnd,
            TeamCapacity = teamCapacity,
        };

    private static DateTimeOffset Moment(long seconds) =>
        DateTimeOffset.FromUnixTimeSeconds(seconds);

    [Fact]
    public void An_event_with_no_schedule_is_not_open()
    {
        // The identifier named a row that is not there, which to a client is the
        // same thing as an event that is not running.
        Assert.False(EventScheduleService.IsOpen(null, EventConstants.TournamentRegistrationSelector, Moment(Now)));
    }

    [Fact]
    public void An_unpublished_event_is_not_open()
    {
        var schedule = Create(enabled: false, publishStart: Now - 100);

        Assert.False(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now)));
    }

    [Fact]
    public void An_event_opens_at_its_publishing_start_and_closes_at_its_end()
    {
        var schedule = Create(publishStart: Now, publishEnd: Now + 100);

        // The opening moment is inside the window and the closing moment is not,
        // because an event that has ended is not one anybody may still enter.
        Assert.False(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now - 1)));
        Assert.True(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now)));
        Assert.True(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now + 99)));
        Assert.False(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now + 100)));
    }

    [Fact]
    public void A_closing_moment_of_zero_means_the_event_never_closes()
    {
        var schedule = Create(publishStart: Now, publishEnd: 0);

        Assert.True(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now + 86_400)));
    }

    [Fact]
    public void A_row_naming_another_mode_is_not_a_tournament()
    {
        // A Survival event is published and in window, and it is still not an
        // event a player may take a Tournament place in.
        var schedule = Create(subtype: EventConstants.SurvivalSelector, publishEnd: Now + 100);

        Assert.True(EventScheduleService.IsPublished(schedule, Now));
        Assert.False(EventScheduleService.IsOpen(schedule, EventConstants.TournamentRegistrationSelector, Moment(Now)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    [InlineData(9, EventConstants.BracketMaximumEntrants)]
    [InlineData(1_000, EventConstants.BracketMaximumEntrants)]
    public void A_field_is_never_larger_than_a_bracket_can_be_drawn(int stated, int expected)
    {
        // A row claiming more teams than the client can render is read as the
        // largest drawable field rather than as a field too large to show.
        Assert.Equal(expected, EventScheduleService.TeamCapacityOf(Create(teamCapacity: stated)));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(100, 0, true)]
    [InlineData(100, 101, true)]
    [InlineData(100, 100, false)]
    [InlineData(100, 99, false)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    public void A_window_must_contain_a_moment_or_mean_never(long start, long end, bool enterable)
    {
        // Zero at the closing end is how an operator states an open-ended event,
        // so it is a window rather than a missing one. Any other closing moment
        // has to be strictly later than the opening one: a window that closes
        // as it opens contains no moment at all, which is a mistake.
        Assert.Equal(enterable, EventScheduleService.IsWindowEnterable(start, end));
    }
}
