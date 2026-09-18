namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// The pair of login stamps a character carries: the login recorded for it and the one
/// before, which is the pair the client draws side by side.
/// <para>
/// A null stamp means no login has been recorded since the columns existed, which reads
/// as "no data" rather than as a login at the epoch — the same distinction the reference
/// server draws with a null column, and the reason these are not defaulted to zero.
/// </para>
/// </summary>
/// <param name="PreviousLoginTime">Unix timestamp of the login the recorded one replaced.</param>
/// <param name="LastLoginTime">Unix timestamp of the login recorded for the character.</param>
public readonly record struct CharacterLoginTimes(int? PreviousLoginTime, int? LastLoginTime)
{
    /// <summary>Seconds in a day, the unit an absence is measured in.</summary>
    private const int SecondsPerDay = 86_400;

    /// <summary>
    /// Whole days since the login this pair records, or zero when it records none.
    /// <para>
    /// Measured from the recorded login rather than from the previous one because it is
    /// taken as the stamps rotate: one title is unlocked by an <em>absence</em>, so a
    /// caller that rotated first would measure every gap as zero and make that title
    /// unreachable. A login stamped in the future — a clock that went backwards, or a
    /// stamp written by another machine — reads as no gap rather than as a negative age.
    /// </para>
    /// </summary>
    /// <param name="now">Unix timestamp to measure to.</param>
    public int DaysSinceLastLogin(int now) =>
        LastLoginTime is { } lastLogin ? Math.Max(0, (now - lastLogin) / SecondsPerDay) : 0;

    /// <summary>The pair after a login: the recorded login becomes the previous one.</summary>
    /// <param name="now">Unix timestamp of the login.</param>
    public CharacterLoginTimes RotatedTo(int now) => new(LastLoginTime, now);
}
