namespace Mgo2Server.Shared.Domain.Events;

/// <summary>Outcome of writing an event schedule.</summary>
public enum ScheduleWriteOutcome
{
    /// <summary>The schedule was written.</summary>
    Written,

    /// <summary>No schedule carries that identifier.</summary>
    NotFound,

    /// <summary>
    /// The window cannot be entered: it is negative, or it closes at or before
    /// the moment it opens.
    /// </summary>
    InvalidWindow,
}
