using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the create-team request: the size it is read at, and the event the
/// new team is filed under. A wrong size is refused, and a refusal is an
/// ordinary outcome that logs nothing, so a drift here reaches the player as a
/// bare network error with an empty log.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventTeamCreationTests
{
    [Fact]
    public void The_create_request_is_exactly_the_shared_record_prefix()
    {
        // name 16 + comment 128 + flags 1 + password 16 + match type 1
        // + the six constant-zero bytes the record ends on.
        Assert.Equal(168, EventTeamCreationUtils.CreateRequestWireSize);
        Assert.True(EventTeamCreationUtils.IsExpectedCreateRequestShape(168));
    }

    [Theory]
    [InlineData(178)]
    [InlineData(184)]
    [InlineData(167)]
    [InlineData(0)]
    public void Anything_but_the_record_is_not_the_create_request(int payloadLength)
    {
        // 178 and 184 are the sizes the reference server reads. Its layout is
        // the shared team's struct offsets carried past the end of the request,
        // so a build that trusted them refused every real create.
        Assert.False(EventTeamCreationUtils.IsExpectedCreateRequestShape(payloadLength));
    }

    [Theory]
    [InlineData(LobbySubtypeConstants.Survival)]
    [InlineData(LobbySubtypeConstants.Tournament)]
    [InlineData(LobbySubtypeConstants.FreeBattle)]
    public void A_lobby_with_one_standing_event_files_every_team_under_it(int lobbySubtype)
    {
        Assert.True(EventTeamCreationUtils.TryResolveEventIdentifier(
            lobbySubtype, selectedEventIdentifier: null, out var eventIdentifier));
        Assert.Equal(EventConstants.TransientEventIdentifier, eventIdentifier);
    }

    [Fact]
    public void A_survival_team_ignores_a_stale_event_selection()
    {
        // Survival's information record is the only one it can join, so a
        // selection left over from another screen must not move the team.
        Assert.True(EventTeamCreationUtils.TryResolveEventIdentifier(
            LobbySubtypeConstants.Survival, selectedEventIdentifier: 42, out var eventIdentifier));
        Assert.Equal(EventConstants.TransientEventIdentifier, eventIdentifier);
    }

    [Fact]
    public void A_registration_lobby_needs_the_event_the_player_selected()
    {
        Assert.True(EventTeamCreationUtils.TryResolveEventIdentifier(
            LobbySubtypeConstants.TournamentRegistration,
            selectedEventIdentifier: 42,
            out var eventIdentifier));
        Assert.Equal(42, eventIdentifier);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_registration_lobby_refuses_a_team_with_no_event_selected(int? selectedEventIdentifier)
    {
        // Defaulting this into the transient record would file the team under an
        // event the bracket never knew about, which is worse than refusing.
        Assert.False(EventTeamCreationUtils.TryResolveEventIdentifier(
            LobbySubtypeConstants.TournamentRegistration,
            selectedEventIdentifier,
            out var eventIdentifier));
        Assert.Equal(0, eventIdentifier);
    }
}
