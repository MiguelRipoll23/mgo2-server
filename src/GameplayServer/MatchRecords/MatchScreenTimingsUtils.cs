namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The measured stretches a round and a stage spend on their result screens before
/// the next round or the next briefing may begin.
/// </summary>
/// <remarks>
/// <para>
/// They are one reference because the two ends are one shape: a round that ends is
/// followed by its client's own screen time, and the room may not open the next
/// round or begin the next briefing until that time has run. A caller reads the
/// hold its boundary takes from here rather than keeping a number of its own.
/// </para>
/// <para>
/// <b>The round's hold is 24.033 s, measured.</b> The first round of
/// <c>mgo2-started-on-game-round1</c> writes its zero at t+191.599 and its
/// successor opens at t+215.632; the reference repeats the zero every four seconds
/// through the stretch and writes the round's own run only after it
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §66). The value is rounded to
/// the measured tenth of a second.
/// </para>
/// <para>
/// <b>The stage's hold is 44.792 s, measured.</b> In
/// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> its final round's result
/// is written at t+163.597 and the next stage's <c>0x0b</c> count opens at
/// t+208.389; the client's round result, score graph and stage result fill it. The
/// stage's own result is written 21.133 s behind the round's, into the same
/// screens.
/// </para>
/// </remarks>
public static class MatchScreenTimingsUtils
{
    /// <summary>
    /// How long a round's own result and score graph run before the next round of
    /// the same stage may open.
    /// </summary>
    public const int RoundEndScreensMilliseconds = 24_000;

    /// <summary>
    /// How long the next round waits for the room's clients to report it once the
    /// result screens are over. It is the bound on the report gate, not the
    /// reference's own interval: the reference waits for the last client, and a
    /// client that never reports must not hold the room for ever.
    /// </summary>
    public const int RoundReportWaitMilliseconds = 10_000;

    /// <summary>
    /// Delay from the round's result write to the stage's, which is the stage
    /// result screen's own instant.
    /// </summary>
    public const int StageResultMilliseconds = 21_133;

    /// <summary>
    /// How long a stage's result screens run before its next briefing may begin.
    /// </summary>
    public const int StageEndMilliseconds = 44_800;
}
