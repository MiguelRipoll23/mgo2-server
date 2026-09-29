using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

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

    [Theory]
    [InlineData("SURVIVAL_HOST", true)]
    [InlineData("survival_host", true)]
    [InlineData("Survival_Host", true)]
    [InlineData("TOURNAMENT_HOST", true)]
    [InlineData("tournament_host", true)]
    [InlineData("SURVIVAL_HOST 2", false)]
    [InlineData("SURVIVAL_HOSTS", false)]
    [InlineData(" MY SURVIVAL_HOST", false)]
    [InlineData("MY ROOM", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_two_host_names_are_reserved(string? name, bool expected)
    {
        // A reserved name is a role rather than a room, so the room list hides it
        // and the create-game request refuses it without the dedicated flag.
        Assert.Equal(expected, EventHostEligibilityUtils.IsReservedHostName(name));
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

    [Fact]
    public void A_room_hosts_a_match_only_when_all_three_rules_hold()
    {
        Assert.True(EventHostEligibilityUtils.IsEligibleHost(
            Room(members: [7], maximumPlayers: 8),
            matchType: EventConstants.SurvivalSelector,
            participantCount: 7));

        // A player's own room is not a host however free it is, and a host that has
        // collected a player is running a game rather than offering one.
        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            Room(name: "MY ROOM", members: [7], maximumPlayers: 8),
            EventConstants.SurvivalSelector,
            7));

        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            Room(members: [7, 8], maximumPlayers: 8),
            EventConstants.SurvivalSelector,
            7));

        // The room's own mode has to agree, and the match has to fit beside the
        // host's slot.
        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            Room(members: [7], maximumPlayers: 8, lobbySubtype: EventConstants.TournamentSelector),
            EventConstants.SurvivalSelector,
            4));

        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            Room(members: [7], maximumPlayers: 8),
            EventConstants.SurvivalSelector,
            participantCount: 8));
    }

    [Fact]
    public void A_host_serves_the_mode_its_name_stands_for_not_its_lobby_s()
    {
        // The role sets the room's mode when the room is created, so a dedicated
        // host opened in another lobby still serves the mode it is named for.
        Assert.Equal(EventConstants.SurvivalSelector, EventHostEligibilityUtils.HostSubtype("SURVIVAL_HOST"));
        Assert.Equal(EventConstants.TournamentSelector, EventHostEligibilityUtils.HostSubtype("tournament_host"));
        Assert.Null(EventHostEligibilityUtils.HostSubtype("MY ROOM"));
        Assert.Null(EventHostEligibilityUtils.HostSubtype(null));
    }

    [Fact]
    public void Only_the_survival_role_is_the_one_nobody_may_walk_into()
    {
        // The two roles are served by different rules: a Tournament host is handed
        // its players by a draw, while a Survival host is leased a match and may be
        // entered by nobody but the two teams it was leased to.
        Assert.True(EventHostEligibilityUtils.IsSurvivalHost(
            Room(name: "survival_host", lobbySubtype: EventConstants.SurvivalSelector)));
        Assert.False(EventHostEligibilityUtils.IsSurvivalHost(
            Room(name: "TOURNAMENT_HOST", lobbySubtype: EventConstants.TournamentSelector)));

        // Neither is a role on the name alone.
        Assert.False(EventHostEligibilityUtils.IsSurvivalHost(Room(common: "{}")));
        Assert.False(EventHostEligibilityUtils.IsTournamentHost(
            Room(name: "TOURNAMENT_HOST", common: "{}", lobbySubtype: EventConstants.TournamentSelector)));

        // A Tournament host is one only while it is running the role's own mode.
        Assert.True(EventHostEligibilityUtils.IsTournamentHost(
            Room(name: "TOURNAMENT_HOST", lobbySubtype: EventConstants.TournamentSelector)));
        Assert.False(EventHostEligibilityUtils.IsTournamentHost(
            Room(name: "TOURNAMENT_HOST", lobbySubtype: EventConstants.SurvivalSelector)));
    }

    [Fact]
    public void A_join_may_name_only_the_mode_the_room_runs()
    {
        var room = Room(lobbySubtype: EventConstants.SurvivalSelector);
        Assert.True(EventHostEligibilityUtils.AcceptsJoinMode(room, EventConstants.SurvivalSelector));
        Assert.False(EventHostEligibilityUtils.AcceptsJoinMode(room, LobbySubtypeConstants.FreeBattle));

        // A request that stops before the byte makes no claim, and a room that
        // cannot state its own mode is not asked for one.
        Assert.True(EventHostEligibilityUtils.AcceptsJoinMode(
            room,
            EventHostEligibilityUtils.UnnamedSubtype));
        Assert.True(EventHostEligibilityUtils.AcceptsJoinMode(
            Room(lobbySubtype: 0),
            LobbySubtypeConstants.FreeBattle));

        // The Tournament host is reached through the ordinary room screen, which
        // may name Free Battle, so it takes that as well as its own mode.
        var tournament = Room(name: "TOURNAMENT_HOST", lobbySubtype: EventConstants.TournamentSelector);
        Assert.True(EventHostEligibilityUtils.AcceptsJoinMode(tournament, LobbySubtypeConstants.FreeBattle));
        Assert.True(EventHostEligibilityUtils.AcceptsJoinMode(tournament, EventConstants.TournamentSelector));

        // A room that is not that role gets no such exception.
        Assert.False(EventHostEligibilityUtils.AcceptsJoinMode(
            Room(name: "TOURNAMENT_HOST", common: "{}", lobbySubtype: EventConstants.TournamentSelector),
            LobbySubtypeConstants.FreeBattle));
    }

    [Fact]
    public void A_host_is_chosen_whatever_lobby_it_was_opened_in()
    {
        // The role sets the room's mode when the room is created, so the room that
        // hosts a Survival match need not sit in the Survival lobby.
        var elsewhere = Room(members: [7], maximumPlayers: 17, lobbySubtype: EventConstants.SurvivalSelector);
        elsewhere.LobbyIdentifier = 99;
        var wrongMode = Room(members: [7], maximumPlayers: 17, lobbySubtype: EventConstants.TournamentSelector);
        var busy = Room(members: [7, 8], maximumPlayers: 17);

        Assert.Same(
            elsewhere,
            EventHostRoomPoolService.SelectHost(
                [wrongMode, busy, elsewhere],
                EventConstants.SurvivalSelector,
                participantCount: 16,
                requiredSettings: null));

        Assert.Null(EventHostRoomPoolService.SelectHost(
            [wrongMode, busy],
            EventConstants.SurvivalSelector,
            participantCount: 16,
            requiredSettings: null));
    }

    [Fact]
    public void A_room_that_is_running_the_events_settings_may_host_it()
    {
        var preset = EventHostEnvironment.CreateDefault();
        var room = Room(common: EventHostRoomSettingsUtils.Compose(true, SurvivalPreset()));

        Assert.True(EventHostEligibilityUtils.IsEligibleHost(
            room,
            EventConstants.SurvivalSelector,
            participantCount: 4,
            preset));

        Assert.Same(
            room,
            EventHostRoomPoolService.SelectHost(
                [room],
                EventConstants.SurvivalSelector,
                participantCount: 4,
                preset));
    }

    [Fact]
    public void A_room_running_settings_of_its_own_may_not_host_the_event()
    {
        // The host saved their own settings: a longer team-deathmatch round. The
        // room is a dedicated host in the right mode with room to spare, and it is
        // still refused, because the event is played on the event's settings.
        var room = Room(common: EventHostRoomSettingsUtils.Compose(true, SurvivalPreset(teamDeathmatchTime: 10)));

        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            room,
            EventConstants.SurvivalSelector,
            participantCount: 4,
            EventHostEnvironment.CreateDefault()));

        // The same room is a host when the deployment does not ask for the settings.
        Assert.True(EventHostEligibilityUtils.IsEligibleHost(
            room,
            EventConstants.SurvivalSelector,
            participantCount: 4));
    }

    [Fact]
    public void A_room_that_kept_no_settings_cannot_be_running_them()
    {
        // A room created before the settings were kept, or without a push behind
        // it, cannot state what it runs, so it is refused rather than assumed.
        var room = Room(common: """{"dedicated":true}""");

        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            room,
            EventConstants.SurvivalSelector,
            participantCount: 4,
            EventHostEnvironment.CreateDefault()));

        Assert.False(EventHostEligibilityUtils.IsEligibleHost(
            Room(common: "not json"),
            EventConstants.SurvivalSelector,
            participantCount: 4,
            EventHostEnvironment.CreateDefault()));
    }

    /// <summary>
    /// The shipping Survival preset as a settings row, which is what a host that
    /// opened a room from it pushed. The snake-kill count is stated because the
    /// row's own default is 3 while the preset leaves it at zero, and the preset
    /// is what a host has to have saved to match it.
    /// </summary>
    private static CharacterHostSettings SurvivalPreset(int teamDeathmatchTime = 5) => new()
    {
        MaxPlayers = 17,
        BriefingTime = 1,
        LevelLimitBase = 0x16,
        CommonA = 0x04,
        SneakingSnakeKills = 0,
        RotationRules = Slots(4, 4, 4, 4),
        RotationMaps = Slots(1, 2, 3, 4),
        RotationFlags = Slots(0, 0, 0, 0),
        RuleTimers = [0, 0, 0, 0, 0, 0, teamDeathmatchTime, 2, 50, 0, 0, 0, 0, 0, 0, 0, 0],
    };

    /// <summary>One rotation component as a stored row carries it: sixteen slots.</summary>
    private static short[] Slots(params short[] values)
    {
        var slots = new short[EventHostEnvironment.RotationSlots];
        values.CopyTo(slots, 0);
        return slots;
    }

    /// <summary>A room row, with the roster and settings the three rules read.</summary>
    private static Game Room(
        string name = "SURVIVAL_HOST",
        string common = """{"dedicated":true}""",
        int hostIdentifier = 7,
        List<int>? members = null,
        int maximumPlayers = 17,
        int lobbySubtype = EventConstants.SurvivalSelector) =>
        new()
        {
            Name = name,
            Common = common,
            HostIdentifier = hostIdentifier,
            MaximumPlayers = maximumPlayers,
            LobbySubtype = lobbySubtype,
            Players = [.. (members ?? [hostIdentifier]).Select(characterIdentifier => new GamePlayer
            {
                CharacterIdentifier = characterIdentifier,
            })],
        };
}
