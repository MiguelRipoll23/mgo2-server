namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The measured stretches a round and a stage spend on their result screens before
/// the next round or the next briefing may begin.
/// </summary>
/// <remarks>
/// <para>
/// They are one reference because the two ends are one shape: a round that ends is
/// followed by its client's own screens, and the room may not open the next round
/// or begin the next briefing until they are behind it. A round inside a stage is
/// released by its clients' post-screen reports; a stage's last round is released
/// by its measured screen pace, because the stage result is the client script's
/// own timed sequence. A caller reads the value its boundary takes from here rather than
/// keeping a number of its own.
/// </para>
/// <para>
/// <b>The round's hold is 24.033 s, measured — and it is the client's pace, not
/// a host timer.</b> The first round of <c>mgo2-started-on-game-round1</c> writes
/// its zero at t+191.599 and its successor opens at t+215.632; through the stretch
/// the reference repeats the zero every four seconds and opens 0.19 s after the
/// last post-screen publication, which is the report that ends the wait
/// (<c>docs/analysis/join-stall-after-mode2.md</c> §66). The image has no such
/// constant: the screens are the stage script's own, driven by its round-over
/// event. This host releases the next round on that pace: a client's post-screen
/// report is sent only once the round it precedes has been told to it, so the
/// report cannot be what opens the round. Measured twice on 2026-11-27 — the
/// live client reported 2 s after a 30 s bound had opened round 2, and 2 s
/// after a 60 s one.
/// </para>
/// <para>
/// <b>The stage's hold is 44.792 s, measured.</b> In
/// <c>rounded-ended-by-reaching-tickets-limit.pcapng</c> its final round's result
/// is written at t+163.597 and the next stage's <c>0x0b</c> count opens at
/// t+208.389; the client's round result, score graph and stage result fill it. The
/// stage's own result is written 21.133 s behind the round's, into the same
/// screens. Like the round's hold it is the reference client's pace, not a timer
/// the image carries — the next briefing is the scripts' to start.
/// </para>
/// </remarks>
public static class MatchScreenTimingsUtils
{
    /// <summary>
    /// The measured pace of a round's own result and score graph: the span the
    /// screens are armed with, and the point at which the room stops waiting for a
    /// post-screen report and opens the next round of the stage.
    /// </summary>
    public const int RoundEndScreensMilliseconds = 24_000;

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
