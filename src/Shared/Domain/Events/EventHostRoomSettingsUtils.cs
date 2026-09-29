using System.Text.Json;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The settings a room was opened with, kept on the room itself.
/// <para>
/// The settings a host has saved are theirs to change, so they cannot say what a
/// room is running: a room opened before the host edited them would be read as
/// running the edit. The room therefore carries its own copy, taken from the
/// settings it was created from, and the copy is what has to agree with the
/// event's own before the room may host a match. That is the reference's shape —
/// its room row keeps the settings JSON and its host environment is built from
/// the row, not from the host's saved settings.
/// </para>
/// <para>
/// The copy is the room's common-settings blob: an object with the dedicated
/// flag the room is recognised by, and the host-environment block beside it,
/// base64, exactly as <see cref="EventHostEnvironmentUtils"/> writes it. Keeping
/// the block rather than a second, hand-written mapping of it is deliberate: a
/// field added to the environment would otherwise have to be remembered here as
/// well, and a room that silently lost one would be read as running settings
/// nobody chose.
/// </para>
/// </summary>
public static class EventHostRoomSettingsUtils
{
    /// <summary>Serializer settings of the room blob; the Web defaults are camel case.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The room's settings as the room row keeps them: the flags it is
    /// recognised by, and the environment it runs when it was created from
    /// settings a client pushed.
    /// </summary>
    /// <param name="dedicated">Whether the host created the room as a dedicated host.</param>
    /// <param name="settings">Settings the room was created from, or null when none were ever saved.</param>
    /// <returns>The blob to store on the room.</returns>
    public static string Compose(bool dedicated, CharacterHostSettings? settings)
    {
        var document = new RoomSettingsDocument
        {
            Dedicated = dedicated,
            HostEnvironment = settings is null ? null : Encode(ToEnvironment(settings)),
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    /// <summary>
    /// The environment a room is running, read back from the room's own copy.
    /// </summary>
    /// <param name="common">Room settings blob.</param>
    /// <returns>The environment, or null when the room never kept one.</returns>
    public static EventHostEnvironment? ReadEnvironment(string? common)
    {
        if (string.IsNullOrWhiteSpace(common))
        {
            return null;
        }

        RoomSettingsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<RoomSettingsDocument>(common, SerializerOptions);
        }
        catch (JsonException)
        {
            // A blob that cannot be read is not a room running the preset, which
            // is the same answer an unreadable blob gives the dedicated rule.
            return null;
        }

        return document is null ? null : Decode(document.HostEnvironment);
    }

    /// <summary>
    /// Whether a room is running the settings an event requires. A room that kept
    /// none cannot be running them, so it is refused rather than assumed.
    /// </summary>
    /// <param name="common">Room settings blob.</param>
    /// <param name="required">Environment the room has to be running.</param>
    public static bool Matches(string? common, EventHostEnvironment required)
    {
        ArgumentNullException.ThrowIfNull(required);

        return ReadEnvironment(common) is { } stored && HasSameStaticSettings(stored, required);
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

    /// <summary>
    /// The environment a settings row describes: the rotation, weapon
    /// restrictions, rule timers and common bytes the client pushed, made into
    /// the block every other reader of an environment already works with.
    /// </summary>
    /// <param name="settings">Settings row a room was created from.</param>
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

    private static string Encode(EventHostEnvironment environment)
    {
        var writer = new PacketWriter();
        EventHostEnvironmentUtils.Write(writer, environment);
        return Convert.ToBase64String(writer.Build());
    }

    private static EventHostEnvironment? Decode(string? block)
    {
        if (string.IsNullOrWhiteSpace(block))
        {
            return null;
        }

        try
        {
            return EventHostEnvironmentUtils.Read(Convert.FromBase64String(block));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            // A block of the wrong size is not an environment this code wrote.
            return null;
        }
    }

    /// <summary>One byte of a rotation component, or zero when the row carries none.</summary>
    private static int Byte(short[]? component, int index) =>
        component is { } values && index < values.Length ? values[index] & 0xff : 0;

    /// <summary>One rule word, or zero when the row carries fewer than the block does.</summary>
    private static int Timer(int[]? timers, int index) =>
        timers is { } values && index < values.Length ? values[index] : 0;

    /// <summary>The room settings blob, as the room row stores it.</summary>
    private sealed class RoomSettingsDocument
    {
        /// <summary>Whether the room was created as a dedicated host.</summary>
        public bool Dedicated { get; set; }

        /// <summary>The environment block the room runs, base64, or null when it kept none.</summary>
        public string? HostEnvironment { get; set; }
    }
}
