using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the settings a room keeps on its own row. The room's copy is what the
/// event's settings rule reads, so it has to survive the trip through the room
/// blob whole: a field lost on the way would be read as a host running settings
/// nobody chose, and a match that never finds a host is the result.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventHostRoomSettingsTests
{
    [Fact]
    public void A_room_keeps_the_settings_it_was_created_from()
    {
        var settings = Settings();
        var environment = EventHostRoomSettingsUtils.ReadEnvironment(
            EventHostRoomSettingsUtils.Compose(dedicated: true, settings));

        Assert.NotNull(environment);
        Assert.True(EventHostRoomSettingsUtils.HasSameStaticSettings(
            environment,
            EventHostRoomSettingsUtils.ToEnvironment(settings)));

        // Spot-checked as well, so the round trip is not the only thing holding
        // the mapping up: these are read from the room, not from the row.
        Assert.Equal(17, environment.MaximumPlayers);
        Assert.Equal(1, environment.BriefingTime);
        Assert.Equal(2, environment.Stance);
        Assert.Equal(0x04, environment.CommonA);
        Assert.Equal(0x16, environment.StandardRateOrLevelLimitBase);
        Assert.Equal(5, environment.TeamDeathmatchTime);
        Assert.Equal(50, environment.TeamDeathmatchTickets);
        Assert.Equal(1, environment.CaptureExtraTime);
        Assert.Equal([4, 4, 4, 4, 0], Enumerable.Range(0, 5).Select(slot => environment.Rotations[slot][0]));
        Assert.Equal([1, 2, 3, 4, 0], Enumerable.Range(0, 5).Select(slot => environment.Rotations[slot][1]));
        Assert.Equal(0x11, environment.WeaponRestrictions[0]);
    }

    [Fact]
    public void The_dedicated_flag_is_read_from_the_room_blob()
    {
        // The flag travels beside the environment, so the room is recognised as a
        // dedicated host by the same blob that says what it runs.
        Assert.True(EventHostEligibilityUtils.IsDedicated(
            EventHostRoomSettingsUtils.Compose(dedicated: true, Settings())));
        Assert.False(EventHostEligibilityUtils.IsDedicated(
            EventHostRoomSettingsUtils.Compose(dedicated: false, Settings())));
    }

    [Fact]
    public void The_flag_does_not_change_the_settings_a_room_runs()
    {
        var dedicated = EventHostRoomSettingsUtils.ReadEnvironment(
            EventHostRoomSettingsUtils.Compose(dedicated: true, Settings()));
        var ordinary = EventHostRoomSettingsUtils.ReadEnvironment(
            EventHostRoomSettingsUtils.Compose(dedicated: false, Settings()));

        Assert.NotNull(dedicated);
        Assert.NotNull(ordinary);
        Assert.True(EventHostRoomSettingsUtils.HasSameStaticSettings(dedicated, ordinary));
    }

    [Fact]
    public void A_room_without_settings_has_no_environment()
    {
        // A host that never pushed settings opened a room that cannot say what it
        // runs. That is a state to report rather than a preset to guess at.
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment(
            EventHostRoomSettingsUtils.Compose(dedicated: true, settings: null)));
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment("{}"));
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment(""));
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment(null));
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment("not json"));
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment("""{"hostEnvironment":"not base64"}"""));
        Assert.Null(EventHostRoomSettingsUtils.ReadEnvironment(
            """{"hostEnvironment":"AAAA"}"""));
    }

    [Theory]
    [InlineData("timer")]
    [InlineData("rotation")]
    [InlineData("weapons")]
    [InlineData("players")]
    [InlineData("briefing")]
    [InlineData("common")]
    public void Settings_of_its_own_are_a_different_preset(string difference)
    {
        var required = EventHostRoomSettingsUtils.ToEnvironment(Settings());
        var own = EventHostRoomSettingsUtils.ToEnvironment(Settings(difference));

        Assert.False(EventHostRoomSettingsUtils.HasSameStaticSettings(required, own));
    }

    [Fact]
    public void The_same_settings_are_the_same_preset()
    {
        Assert.True(EventHostRoomSettingsUtils.HasSameStaticSettings(
            EventHostRoomSettingsUtils.ToEnvironment(Settings()),
            EventHostRoomSettingsUtils.ToEnvironment(Settings())));
    }

    [Fact]
    public void A_room_that_kept_none_is_not_running_the_required_settings()
    {
        var required = EventHostEnvironment.CreateDefault();

        Assert.False(EventHostRoomSettingsUtils.Matches("{}", required));
        Assert.False(EventHostRoomSettingsUtils.Matches("not json", required));
        Assert.False(EventHostRoomSettingsUtils.Matches(null, required));
        Assert.True(EventHostRoomSettingsUtils.Matches(
            EventHostRoomSettingsUtils.Compose(true, Settings()),
            EventHostRoomSettingsUtils.ToEnvironment(Settings())));
    }

    /// <summary>
    /// A settings row of a host that saved settings of their own: the shipping
    /// preset, apart from the one field the named difference changes.
    /// </summary>
    private static CharacterHostSettings Settings(string difference = "")
    {
        var settings = new CharacterHostSettings
        {
            Name = "SURVIVAL_HOST",
            MaxPlayers = 17,
            BriefingTime = 1,
            Stance = 2,
            LevelLimitTolerance = 3,
            LevelLimitBase = 0x16,
            CommonA = 0x04,
            CommonB = 0x20,
            UniqueRed = 0x01,
            UniqueBlue = 0x02,
            IdleKick = 4,
            TeamKillKick = 5,
            CaptureExtraTime = true,
            SneakingSnakeKills = 3,
            RotationRules = Slots(4, 4, 4, 4),
            RotationMaps = Slots(1, 2, 3, 4),
            RotationFlags = Slots(0, 1, 0, 1),
            RuleTimers = [0, 0, 0, 0, 0, 0, 5, 2, 50, 0, 0, 0, 0, 0, 0, 0, 0],
            WeaponRestrictions = Restriction(),
        };

        switch (difference)
        {
            case "timer":
                settings.RuleTimers![6] = 10;
                break;
            case "rotation":
                settings.RotationMaps![1] = 9;
                break;
            case "weapons":
                settings.WeaponRestrictions![3] = 0xff;
                break;
            case "players":
                settings.MaxPlayers = 16;
                break;
            case "briefing":
                settings.BriefingTime = 2;
                break;
            case "common":
                settings.CommonA = 0x0c;
                break;
        }

        return settings;
    }

    /// <summary>One rotation component as a stored row carries it: sixteen slots.</summary>
    private static short[] Slots(params short[] values)
    {
        var slots = new short[EventHostEnvironment.RotationSlots];
        values.CopyTo(slots, 0);
        return slots;
    }

    private static byte[] Restriction()
    {
        var restriction = new byte[16];
        restriction[0] = 0x11;
        return restriction;
    }
}
