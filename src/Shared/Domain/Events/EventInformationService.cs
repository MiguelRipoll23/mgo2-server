using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Builds the 905-byte event information record that <c>0x4908</c>/<c>0x4909</c>
/// and <c>0x4904</c>/<c>0x4905</c> carry. The record is the same shape for all
/// three selectors; only the record's selector name and the advertised settings
/// change.
/// <para>
/// The schedule is emitted as two whole Unix epoch seconds derived from the
/// configured daily window, rather than as a stored timestamp, so an operator
/// states the hours they mean and a deployment does not go stale.
/// </para>
/// </summary>
/// <param name="options">Operator configuration the record advertises.</param>
public sealed class EventInformationService(IOptions<EventOptions> options)
{
    private readonly EventOptions settings = options.Value;

    /// <summary>Builds the record for one selector.</summary>
    /// <param name="selector">Lobby selector the record describes.</param>
    /// <returns>The 905-byte record.</returns>
    public byte[] BuildRecord(int selector)
    {
        var now = DateTimeOffset.UtcNow;
        return BuildRecord(
            EventConstants.TransientEventIdentifier,
            selector,
            now,
            EventHostEnvironment.CreateDefault());
    }

    /// <summary>Builds the record with an explicit environment and time.</summary>
    /// <param name="eventIdentifier">Event correlation identifier.</param>
    /// <param name="selector">Lobby selector the record describes.</param>
    /// <param name="now">Moment the schedule is derived from.</param>
    /// <param name="environment">Host environment to embed.</param>
    /// <returns>The 905-byte record.</returns>
    public byte[] BuildRecord(
        int eventIdentifier,
        int selector,
        DateTimeOffset now,
        EventHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var schedule = ScheduleFor(now);
        var rewards = settings.WinRewardTable();

        var writer = new PacketWriter();
        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)eventIdentifier);
        writer.WritePadding(4);
        writer.WriteUInt16(0);
        writer.WriteFixedString(SelectorName(selector), 16);
        writer.WritePadding(16);
        EventHostEnvironmentUtils.Write(writer, environment);
        writer.WritePadding(8);
        writer.WriteUInt32(0);
        writer.WriteFixedString(settings.Title, 64);
        writer.WriteFixedString(settings.Description, 512);

        // Ten consecutive-win rewards, then the separate participation reward.
        foreach (var reward in rewards)
        {
            writer.WriteInt32(reward);
        }

        writer.WriteInt32(settings.ParticipationReward);

        // Two u32 epoch seconds the client converts into a local window.
        writer.WriteUInt32((uint)schedule.Start);
        writer.WriteUInt32((uint)schedule.End);

        // Eleven untouched bytes, then the rule byte and the drawn win count.
        writer.WritePadding(11);
        writer.WriteUInt8(settings.InformationRuleFlags);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(settings.DisplayedWinCount);

        var record = writer.Build();
        if (record.Length != EventConstants.InformationWireSize)
        {
            throw new InvalidOperationException(
                $"The event information record is {record.Length} bytes, expected " +
                $"{EventConstants.InformationWireSize}.");
        }

        return record;
    }

    /// <summary>
    /// The window the record advertises, as epoch seconds. The window is daily,
    /// so a closing minute at or below the opening one rolls into the next day.
    /// </summary>
    /// <param name="now">Moment to derive the window from.</param>
    public (int Start, int End) ScheduleFor(DateTimeOffset now)
    {
        var zone = settings.ResolveZone();
        var localDate = TimeZoneInfo.ConvertTime(now, zone).Date;

        var startLocal = localDate.AddMinutes(settings.StartMinute);
        var endLocal = localDate.AddMinutes(settings.EndMinute);
        if (endLocal <= startLocal)
        {
            endLocal = localDate.AddDays(1).AddMinutes(settings.EndMinute);
        }

        return (ToEpochSeconds(startLocal, zone), ToEpochSeconds(endLocal, zone));
    }

    /// <summary>Converts one wall-clock moment in a zone to Unix epoch seconds.</summary>
    private static int ToEpochSeconds(DateTime local, TimeZoneInfo zone)
    {
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
        return checked((int)new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    /// <summary>Name the record carries for one selector.</summary>
    /// <param name="selector">Lobby selector.</param>
    public static string SelectorName(int selector) => selector switch
    {
        EventConstants.TournamentSelector => "TOURNAMENT",
        EventConstants.TournamentRegistrationSelector => "TOURNAMENT ENTRY",
        _ => "SURVIVAL",
    };
}
