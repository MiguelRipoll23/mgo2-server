using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the rule that reads one empty request as four operations. The client
/// distinguishes them only by the screen it is on, so a wrong branch is silent:
/// it cancels a team whose player asked to enter, or enters an event the player
/// was only reading about.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventEntryRoutingTests
{
    [Fact]
    public void The_registration_lobby_always_submits_a_team()
    {
        Assert.Equal(
            EventEntryRoute.SubmitTournamentTeam,
            EventEntryUtils.Resolve(
                EventConstants.TournamentRegistrationSelector,
                hasTeam: true,
                hasSelectedEvent: true));
    }

    [Fact]
    public void The_registration_lobby_does_nothing_without_a_team_or_an_event()
    {
        // Nothing to submit: a request that names no team would otherwise create
        // an entry from nothing.
        Assert.Equal(
            EventEntryRoute.Unsupported,
            EventEntryUtils.Resolve(
                EventConstants.TournamentRegistrationSelector,
                hasTeam: false,
                hasSelectedEvent: true));

        Assert.Equal(
            EventEntryRoute.Unsupported,
            EventEntryUtils.Resolve(
                EventConstants.TournamentRegistrationSelector,
                hasTeam: true,
                hasSelectedEvent: false));
    }

    [Fact]
    public void A_survival_team_with_no_event_open_is_withdrawing_its_entry()
    {
        Assert.Equal(
            EventEntryRoute.CancelSurvivalEntry,
            EventEntryUtils.Resolve(
                EventConstants.SurvivalSelector,
                hasTeam: true,
                hasSelectedEvent: false));
    }

    [Fact]
    public void An_event_opened_after_the_team_formed_is_an_entry_not_a_withdrawal()
    {
        // The player opened an event and asked to enter it. Withdrawing the team
        // would answer the opposite of the request, so the selected event wins
        // even though a team is waiting.
        Assert.Equal(
            EventEntryRoute.EnterEventSolo,
            EventEntryUtils.Resolve(
                EventConstants.SurvivalSelector,
                hasTeam: true,
                hasSelectedEvent: true));
    }

    [Fact]
    public void A_survival_connection_with_an_event_open_enters_without_a_team()
    {
        Assert.Equal(
            EventEntryRoute.EnterEventSolo,
            EventEntryUtils.Resolve(
                EventConstants.SurvivalSelector,
                hasTeam: false,
                hasSelectedEvent: true));
    }

    [Fact]
    public void A_survival_connection_with_no_team_has_nothing_to_withdraw()
    {
        Assert.Equal(
            EventEntryRoute.Unsupported,
            EventEntryUtils.Resolve(
                EventConstants.SurvivalSelector,
                hasTeam: false,
                hasSelectedEvent: false));
    }

    [Fact]
    public void The_tournament_lobby_enters_an_individual_player()
    {
        // The Tournament lobby is not the registration lobby: a player there
        // enters as itself, with or without a team of its own.
        Assert.Equal(
            EventEntryRoute.EnterEventSolo,
            EventEntryUtils.Resolve(
                EventConstants.TournamentSelector,
                hasTeam: true,
                hasSelectedEvent: true));

        Assert.Equal(
            EventEntryRoute.EnterEventSolo,
            EventEntryUtils.Resolve(
                EventConstants.TournamentSelector,
                hasTeam: false,
                hasSelectedEvent: true));
    }

    [Fact]
    public void The_tournament_lobby_does_nothing_without_an_open_event()
    {
        // Without a detail screen there is no event the player could have asked
        // to enter, and no Survival waiting state to withdraw either.
        Assert.Equal(
            EventEntryRoute.Unsupported,
            EventEntryUtils.Resolve(
                EventConstants.TournamentSelector,
                hasTeam: true,
                hasSelectedEvent: false));
    }

    [Fact]
    public void A_lobby_that_is_not_an_event_lobby_serves_none_of_it()
    {
        Assert.Equal(
            EventEntryRoute.Unsupported,
            EventEntryUtils.Resolve(
                lobbySubtype: 1,
                hasTeam: true,
                hasSelectedEvent: true));
    }

    [Fact]
    public void Only_the_character_own_team_of_one_is_reused_as_its_entrant()
    {
        // A repeat entry belongs to the team of one the character already owns.
        Assert.True(EventEntryUtils.IsReusableEntrantTeam(1, 1, 1));

        // A formed roster enters as a team through its own screens, and a team of
        // another event is that event's entrant.
        Assert.False(EventEntryUtils.IsReusableEntrantTeam(2, 1, 1));
        Assert.False(EventEntryUtils.IsReusableEntrantTeam(1, 2, 1));
    }

    [Fact]
    public void An_event_window_ends_at_the_moment_it_closes()
    {
        Assert.False(EventEntryUtils.IsWindowOpen(nowSeconds: 99, startSeconds: 100, endSeconds: 200));
        Assert.True(EventEntryUtils.IsWindowOpen(nowSeconds: 100, startSeconds: 100, endSeconds: 200));
        Assert.True(EventEntryUtils.IsWindowOpen(nowSeconds: 199, startSeconds: 100, endSeconds: 200));

        // Half-open, so the first moment of the next day does not belong to two
        // events at once.
        Assert.False(EventEntryUtils.IsWindowOpen(nowSeconds: 200, startSeconds: 100, endSeconds: 200));
    }
}
