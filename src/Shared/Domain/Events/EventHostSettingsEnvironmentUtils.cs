using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The host-environment block read out of a character's stored host settings,
/// and the comparison two of them are the same preset by.
/// <para>
/// The settings live in one place — the character's own host-settings row, with
/// a typed column per field — so there is no copy of them on the room and none
/// to fall out of step with the row. A room is read as running what its host
/// has saved, which means a host who edits the settings after opening a room
/// changes what that room is taken to be running. That is the shape the
/// reference has: its settings are columns, and a room is described by them.
/// </para>
/// <para>
/// The mapping is deliberately the only one: an environment is built here and
/// compared here, so a field added to the settings has to be added once rather
/// than once per reader that wants to know whether a room runs a preset.
/// </para>
/// </summary>
public static class EventHostSettingsEnvironmentUtils
{
    /// <summary>
    /// The environment a settings row describes: the rotation, weapon
    /// restrictions, rule timers and common bytes the client pushed, made into
    /// the block every other reader of an environment already works with.
    /// </summary>
    /// <param name="settings">Settings row to read.</param>
    /// <returns>The environment the row states.</returns>
    public static EventHostEnvironment ToEnvironment(CharacterHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var environment = new EventHostEnvironment
        {
            MaximumPlayers = settings.MaxPlayers,
            BriefingTime = settings.BriefingTime,
            Stance = settings.Stance,
            LevelLimitTolerance = settings.LevelLimitTolerance,
            StandardRateOrLevelLimitBase = settings.LevelLimitBase,
            UniqueRed = settings.UniqueRed,
            UniqueBlue = settings.UniqueBlue,
            CommonA = settings.CommonA,
            CommonB = settings.CommonB,
            IdleKick = settings.IdleKick,
            TeamKillKick = settings.TeamKillKick,
            CaptureExtraTime = settings.CaptureExtraTime ? 1 : 0,
            SneakingSnakeKills = settings.SneakingSnakeKills,
        };

        for (var slot = 0; slot < EventHostEnvironment.RotationSlots; slot++)
        {
            environment.SetRotation(
                slot,
                Byte(settings.RotationRules, slot),
                Byte(settings.RotationMaps, slot),
                Byte(settings.RotationFlags, slot));
        }

        if (settings.WeaponRestrictions is { } restrictions)
        {
            Array.Copy(
                restrictions,
                environment.WeaponRestrictions,
                Math.Min(restrictions.Length, environment.WeaponRestrictions.Length));
        }

        // The block's order, which the settings row stores in the same sequence:
        // sneaking, capture, rescue, team deathmatch, deathmatch, base, bomb and
        // team sneaking, each with the words it carries.
        var timers = settings.RuleTimers;
        environment.SneakingTime = Timer(timers, 0);
        environment.SneakingRounds = Timer(timers, 1);
        environment.CaptureTime = Timer(timers, 2);
        environment.CaptureRounds = Timer(timers, 3);
        environment.RescueTime = Timer(timers, 4);
        environment.RescueRounds = Timer(timers, 5);
        environment.TeamDeathmatchTime = Timer(timers, 6);
        environment.TeamDeathmatchRounds = Timer(timers, 7);
        environment.TeamDeathmatchTickets = Timer(timers, 8);
        environment.DeathmatchTime = Timer(timers, 9);
        environment.DeathmatchTickets = Timer(timers, 10);
        environment.BaseTime = Timer(timers, 11);
        environment.BaseRounds = Timer(timers, 12);
        environment.BombTime = Timer(timers, 13);
        environment.BombRounds = Timer(timers, 14);
        environment.TeamSneakingTime = Timer(timers, 15);
        environment.TeamSneakingRounds = Timer(timers, 16);
        return environment;
    }

    /// <summary>
    /// Whether two environments are the same preset. The live fields are left out
    /// because they change while a match runs, and so are the fields a settings row
    /// cannot state: the team skins, the network status, the team-balance byte, the
    /// unread hole and the trailing host-options byte. Those are the environment
    /// block's own layout rather than settings the client pushes, so comparing them
    /// would compare two values this code put there itself — and a preset edited
    /// through them could never be matched by any host.
    /// </summary>
    /// <param name="first">Environment to compare.</param>
    /// <param name="second">Environment to compare it with.</param>
    public static bool HasSameStaticSettings(EventHostEnvironment first, EventHostEnvironment second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.MaximumPlayers != second.MaximumPlayers
            || first.BriefingTime != second.BriefingTime
            || first.Stance != second.Stance
            || first.LevelLimitTolerance != second.LevelLimitTolerance
            || first.StandardRateOrLevelLimitBase != second.StandardRateOrLevelLimitBase
            || first.UniqueRed != second.UniqueRed
            || first.UniqueBlue != second.UniqueBlue
            || first.CommonA != second.CommonA
            || first.CommonB != second.CommonB
            || first.IdleKick != second.IdleKick
            || first.TeamKillKick != second.TeamKillKick
            || first.CaptureExtraTime != second.CaptureExtraTime
            || first.SneakingSnakeKills != second.SneakingSnakeKills)
        {
            return false;
        }

        for (var slot = 0; slot < EventHostEnvironment.RotationSlots; slot++)
        {
            if (!first.Rotations[slot].AsSpan().SequenceEqual(second.Rotations[slot]))
            {
                return false;
            }
        }

        return first.WeaponRestrictions.AsSpan().SequenceEqual(second.WeaponRestrictions)
            && Timers(first).AsSpan().SequenceEqual(Timers(second));
    }

    /// <summary>The seventeen rule words, in the block's order, so they can be compared as one run.</summary>
    private static int[] Timers(EventHostEnvironment environment) =>
    [
        environment.SneakingTime,
        environment.SneakingRounds,
        environment.CaptureTime,
        environment.CaptureRounds,
        environment.RescueTime,
        environment.RescueRounds,
        environment.TeamDeathmatchTime,
        environment.TeamDeathmatchRounds,
        environment.TeamDeathmatchTickets,
        environment.DeathmatchTime,
        environment.DeathmatchTickets,
        environment.BaseTime,
        environment.BaseRounds,
        environment.BombTime,
        environment.BombRounds,
        environment.TeamSneakingTime,
        environment.TeamSneakingRounds,
    ];

    /// <summary>One byte of a rotation component, or zero when the row carries none.</summary>
    private static int Byte(short[]? component, int index) =>
        component is { } values && index < values.Length ? values[index] & 0xff : 0;

    /// <summary>One rule word, or zero when the row carries fewer than the block does.</summary>
    private static int Timer(int[]? timers, int index) =>
        timers is { } values && index < values.Length ? values[index] : 0;
}
