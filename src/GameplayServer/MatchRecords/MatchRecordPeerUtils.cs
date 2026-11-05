using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Udp;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The rule that decides whether the recorded host's stream should keep being
/// written to a peer.
/// </summary>
/// <remarks>
/// <para>
/// The recorded host never stops writing: it keeps the channel moving for as
/// long as the peer is there. A dedicated host has to stop somewhere, and the
/// place it stops is the reaper's own window — the records are not a reason to
/// keep a departed peer alive, and the session is dropped on exactly that
/// interval (<see cref="PeerSessionService.DefaultIdleTimeout"/>).
/// </para>
/// <para>
/// This is why the rule is shared rather than restated: a stream that outlives
/// the session it is written through keeps drawing from a counter nobody reads,
/// and a stream that gives up earlier leaves a peer that is still there with a
/// channel that has gone silent, which is the shape of a stalled join.
/// </para>
/// </remarks>
public static class MatchRecordPeerUtils
{
    /// <summary>
    /// How long a peer may be quiet before the records stop for it. It is the
    /// reaper's window and not a second, independent guess.
    /// </summary>
    public static TimeSpan IdleTimeout => PeerSessionService.DefaultIdleTimeout;

    /// <summary>
    /// Whether the records should keep being written to a peer: the peer is
    /// still there until it has been quiet for the whole window, and a session
    /// that has been replaced by a newer one is over whatever its clock says
    /// (<see cref="PeerSession.Retired"/>).
    /// </summary>
    /// <param name="session">Session the records are written to.</param>
    /// <returns><c>true</c> while the peer is still there.</returns>
    public static bool IsAlive(PeerSession session) =>
        !session.Retired &&
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.LastSeenAt
        < IdleTimeout.TotalMilliseconds;

    /// <summary>
    /// What a peer's answers had reached, for the log that has to say which record
    /// it was on when it stopped following its briefing.
    /// </summary>
    /// <remarks>
    /// <b>The answers are the only thing a peer still says once its roster is out.</b>
    /// A client that draws its briefing screen keeps answering this host's records
    /// and one that turns away stops, so the last answer, its age and how many there
    /// were in all separate "the screen was shown and then left" from "the screen
    /// was never reached" without a capture of the client's own side. That is the
    /// question a join with no briefing leaves open
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §68).
    /// </remarks>
    /// <param name="session">Session to describe.</param>
    /// <returns>A phrase naming the last answer, or that there is none.</returns>
    public static string AnswersSoFar(PeerSession session)
    {
        if (session.LastAcknowledgedRecord is not { } answered)
        {
            return "it has answered none of this host's records";
        }

        var age = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.LastAcknowledgedAt) / 1000.0;
        return $"its last answer was 0x{answered.Type:x4} ordinal {answered.Ordinal} "
            + $"{age:F1} s ago, {session.AcknowledgedRecords.Count} record(s) in all";
    }
}
