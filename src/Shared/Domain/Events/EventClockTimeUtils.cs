namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Turns the clock times an operator states into the epoch seconds a schedule
/// window is stored in.
/// <para>
/// The window is stored in epoch seconds because that is what the protocol
/// carries, but an epoch second is not something a person can read off a
/// calendar. An operator says "20:00" and this works out which second that is,
/// which is the whole point: the number the row holds is a detail of the storage
/// and should not be the number the moderator has to produce.
/// </para>
/// <para>
/// Times are read in the zone the caller states, not in the server's, because
/// an event is announced to players in the lobby's own clock and a schedule
/// that quietly means a different hour than the one it was announced for is
/// worse than one that is refused.
/// </para>
/// </summary>
public static class EventClockTimeUtils
{
    /// <summary>Longest clock time the parser reads, "HH:MM".</summary>
    private const int MaximumLength = 5;

    /// <summary>Words an operator may write instead of a closing time.</summary>
    private static readonly string[] OpenEndedWords = ["never", "open", "none", "always"];

    /// <summary>
    /// Reads a clock time of the form <c>HH:MM</c>, or one of the words that
    /// stand for an event that never closes.
    /// </summary>
    /// <param name="text">What the operator wrote.</param>
    /// <param name="now">Moment the time is read against.</param>
    /// <param name="timeZone">Zone the time is stated in.</param>
    /// <param name="rollToTomorrow">
    /// Whether a time that has already passed today moves to tomorrow. An
    /// opening time wants this: an event asked for at 21:00 to open at 20:00
    /// means tomorrow's 20:00, not an event that opened an hour ago.
    /// </param>
    /// <param name="epochSecond">The second the text names, or zero for never.</param>
    /// <returns>Whether the text was understood.</returns>
    public static bool TryParse(
        string? text,
        DateTimeOffset now,
        TimeZoneInfo timeZone,
        bool rollToTomorrow,
        out long epochSecond)
    {
        epochSecond = 0;
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (OpenEndedWords.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            // Zero is how a row states an event that stays published, so it is
            // a value rather than the absence of one.
            return true;
        }

        if (!TryReadHourAndMinute(trimmed, out var hour, out var minute))
        {
            return false;
        }

        var local = TimeZoneInfo.ConvertTime(now, timeZone).Date;
        var resolved = new DateTimeOffset(
            local.Year,
            local.Month,
            local.Day,
            hour,
            minute,
            0,
            timeZone.BaseUtcOffset);

        if (rollToTomorrow && resolved <= now)
        {
            resolved = resolved.AddDays(1);
        }

        epochSecond = resolved.ToUnixTimeSeconds();
        return true;
    }

    /// <summary>
    /// Writes a stored moment back as a clock time, so a reply shows the hour an
    /// operator gave rather than the second the row holds.
    /// </summary>
    /// <param name="epochSecond">Moment to write.</param>
    /// <param name="timeZone">Zone to write it in.</param>
    public static string Describe(long epochSecond, TimeZoneInfo timeZone) =>
        DateTimeOffset
            .FromUnixTimeSeconds(epochSecond)
            .ToOffset(timeZone.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(epochSecond)))
            .ToString("HH:mm");

    /// <summary>
    /// Reads <c>HH:MM</c>. A bare hour is read as that hour on the hour, since
    /// "20" is what somebody types when they mean eight in the evening and
    /// making them add ":00" is making them fail for no gain.
    /// </summary>
    private static bool TryReadHourAndMinute(string text, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (text.Length is 0 or > MaximumLength)
        {
            return false;
        }

        var parts = text.Split(':');
        if (parts.Length is < 1 or > 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out hour) || hour is < 0 or > 23)
        {
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        return int.TryParse(parts[1], out minute) && minute is >= 0 and <= 59;
    }
}
