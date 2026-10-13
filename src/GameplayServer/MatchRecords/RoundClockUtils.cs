using Mgo2Server.GameplayServer.Match;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round's clock: how long the round's own setting says it runs, and how much
/// of it is left.
/// </summary>
/// <remarks>
/// <para>
/// The unit and the arithmetic live here rather than in the round's state, because
/// two writers need the same numbers: the opener writes the whole clock into the
/// round's opening run and waits the same length out, and the beat writes what is
/// left of it on every frame. A second reading of
/// <see cref="MatchRecordBodiesUtils.ClockUnitMilliseconds"/> in the beat is how a
/// clock comes to be written in units the round was not opened in.
/// </para>
/// <para>
/// Nothing here is a countdown the caller moves: the remaining time is computed
/// from when the round opened, so a reader that reads twice sees the difference
/// and a writer that misses a frame does not lose ticks.
/// </para>
/// </remarks>
public static class RoundClockUtils
{
    /// <summary>
    /// How long a round runs, in milliseconds, from the time its rule states.
    /// </summary>
    /// <param name="round">The round the room's rotation names.</param>
    /// <returns>The round's length in milliseconds.</returns>
    public static int TimeLimitMilliseconds(MatchRotationEntry round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round.TimeLimitUnits * MatchRecordBodiesUtils.ClockUnitMilliseconds;
    }

    /// <summary>
    /// How much of a round's clock is left at a moment, in the stream's own
    /// 125 ms units, never below zero.
    /// </summary>
    /// <param name="round">The round the room's rotation names.</param>
    /// <param name="openedAtMilliseconds">When the round opened, in Unix milliseconds.</param>
    /// <param name="nowMilliseconds">The moment to read it at, in Unix milliseconds.</param>
    /// <returns>Units of the round clock still to run.</returns>
    public static int RemainingUnits(
        MatchRotationEntry round,
        long openedAtMilliseconds,
        long nowMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(round);

        var spent = (int)((nowMilliseconds - openedAtMilliseconds)
            / MatchRecordBodiesUtils.ClockUnitMilliseconds);

        // A round's end is the opener's to write and not the clock's: a value that
        // ran below zero would be a round this room left some time ago.
        return Math.Max(round.TimeLimitUnits - spent, 0);
    }
}
