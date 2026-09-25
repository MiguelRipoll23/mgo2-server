using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards which room may host an event match. The rule is what stands between a
/// Survival match and being dropped into a stranger's private room, so both the
/// name and the dedicated flag have to be required rather than assumed.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventHostEligibilityTests
{
    [Fact]
    public void A_named_and_dedicated_room_is_an_event_host()
    {
        Assert.True(EventHostEligibilityUtils.IsDedicatedEventHost(
            "SURVIVAL_HOST",
            """{"dedicated":true}"""));

        // The client sends the name as typed.
        Assert.True(EventHostEligibilityUtils.IsDedicatedEventHost(
            "survival_host",
            """{"dedicated":true}"""));

        Assert.True(EventHostEligibilityUtils.IsDedicatedEventHost(
            "TOURNAMENT_HOST",
            """{"dedicated":true}"""));
    }

    [Fact]
    public void The_name_alone_is_not_enough()
    {
        // Any player can name a room anything, so the name without the flag would
        // let a private room be chosen as a host.
        Assert.False(EventHostEligibilityUtils.IsDedicatedEventHost("SURVIVAL_HOST", "{}"));
        Assert.False(EventHostEligibilityUtils.IsDedicatedEventHost(
            "SURVIVAL_HOST",
            """{"dedicated":false}"""));
    }

    [Fact]
    public void The_flag_alone_is_not_enough()
    {
        Assert.False(EventHostEligibilityUtils.IsDedicatedEventHost("MY ROOM", """{"dedicated":true}"""));
        Assert.False(EventHostEligibilityUtils.IsDedicatedEventHost(null, """{"dedicated":true}"""));
    }

    [Fact]
    public void An_unreadable_settings_blob_is_not_a_dedicated_room()
    {
        Assert.False(EventHostEligibilityUtils.IsDedicated("not json"));
        Assert.False(EventHostEligibilityUtils.IsDedicated(string.Empty));
        Assert.False(EventHostEligibilityUtils.IsDedicated(null));
    }

    [Fact]
    public void An_idle_host_is_alone_in_its_own_room()
    {
        Assert.True(EventHostEligibilityUtils.IsIdle(7, [7]));
        Assert.True(EventHostEligibilityUtils.IsIdle(7, [7, 0]));
    }

    [Fact]
    public void A_host_that_has_collected_players_is_not_idle()
    {
        Assert.False(EventHostEligibilityUtils.IsIdle(7, [7, 8]));
    }

    [Fact]
    public void An_absent_host_is_not_idle()
    {
        // A host that has left cannot start a match in the room it left.
        Assert.False(EventHostEligibilityUtils.IsIdle(7, []));
        Assert.False(EventHostEligibilityUtils.IsIdle(7, [8]));
    }

    [Fact]
    public void Capacity_counts_the_host_s_own_slot()
    {
        // Sixteen players need seventeen seats because the host sits in one.
        Assert.False(EventHostEligibilityUtils.HasCapacity(maximumPlayers: 16, participantCount: 16));
        Assert.True(EventHostEligibilityUtils.HasCapacity(maximumPlayers: 17, participantCount: 16));

        Assert.True(EventHostEligibilityUtils.HasCapacity(maximumPlayers: 8, participantCount: 7));
        Assert.False(EventHostEligibilityUtils.HasCapacity(maximumPlayers: 8, participantCount: 8));
    }

    [Fact]
    public void A_match_larger_than_a_survival_match_is_not_accepted()
    {
        Assert.False(EventHostEligibilityUtils.HasCapacity(maximumPlayers: 64, participantCount: 17));
    }

    [Fact]
    public void The_mode_has_to_agree()
    {
        // A host is dedicated to one mode, so a Survival match may not be seated
        // in a room running Tournament.
        Assert.False(EventHostEligibilityUtils.AcceptsMatch(
            gameLobbySubtype: EventConstants.TournamentSelector,
            matchType: EventConstants.SurvivalSelector,
            maximumPlayers: 16,
            participantCount: 4));

        Assert.True(EventHostEligibilityUtils.AcceptsMatch(
            gameLobbySubtype: EventConstants.SurvivalSelector,
            matchType: EventConstants.SurvivalSelector,
            maximumPlayers: 16,
            participantCount: 4));
    }
}
