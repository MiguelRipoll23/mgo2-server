using System.Globalization;

namespace Mgo2Server.Shared.Domain.Automatch;

/// <summary>
/// A window automatching is open in, expressed in zone-local time. A window may
/// wrap past midnight, which is the ordinary case for an evening schedule in a
/// zone whose players are spread over it.
/// </summary>
/// <param name="Start">First moment the window is open.</param>
/// <param name="End">Moment the window closes.</param>
public readonly record struct AutomatchWindow(TimeOnly Start, TimeOnly End)
{
    /// <summary>Whether the window contains a moment. The start is inside it and the end is not.</summary>
    /// <param name="time">Moment to test.</param>
    public bool Contains(TimeOnly time) =>
        Start <= End
            ? time >= Start && time < End
            : time >= Start || time < End;
}

/// <summary>Parses the open-hours schedule and resolves the zone it is expressed in.</summary>
public static class AutomatchWindowUtils
{
    /// <summary>Accepted clock formats: both write a twenty-four hour time.</summary>
    private static readonly string[] Formats = ["HH:mm", "H:mm"];

    /// <summary>
    /// Parses a schedule of <c>HH:mm-HH:mm</c> pairs separated by commas. An empty
    /// value yields no windows, which the caller reads as "open all day" rather
    /// than as "never open": a schedule nobody configured must not close a feature
    /// that used to run.
    /// </summary>
    /// <param name="text">Schedule text.</param>
    /// <exception cref="InvalidOperationException">Thrown when an entry is not a window.</exception>
    public static List<AutomatchWindow> Parse(string? text)
    {
        var windows = new List<AutomatchWindow>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return windows;
        }

        foreach (var entry in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bounds = entry.Split('-', StringSplitOptions.TrimEntries);
            if (bounds.Length != 2 ||
                !TimeOnly.TryParseExact(bounds[0], Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !TimeOnly.TryParseExact(bounds[1], Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                throw new InvalidOperationException(
                    $"AUTOMATCH_WINDOWS entry '{entry}' is not an HH:mm-HH:mm window.");
            }

            windows.Add(new AutomatchWindow(start, end));
        }

        return windows;
    }

    /// <summary>
    /// Resolves the zone a schedule is expressed in, defaulting to UTC and
    /// refusing a zone this host does not know rather than quietly running the
    /// schedule in the wrong hours.
    /// </summary>
    /// <param name="identifier">Zone identifier, as the host names it.</param>
    /// <exception cref="InvalidOperationException">Thrown when the zone is unknown or unusable.</exception>
    public static TimeZoneInfo ResolveZone(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(identifier);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException(
                $"AUTOMATCH_TIMEZONE '{identifier}' is not a zone this host knows. Use an IANA name " +
                "such as Europe/Madrid, or UTC.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"AUTOMATCH_TIMEZONE '{identifier}' is known to this host but not usable.");
        }
    }
}
