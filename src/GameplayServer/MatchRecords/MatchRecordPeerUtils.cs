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
    /// still there until it has been quiet for the whole window.
    /// </summary>
    /// <param name="session">Session the records are written to.</param>
    /// <returns><c>true</c> while the peer is still there.</returns>
    public static bool IsAlive(PeerSession session) =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - session.LastSeenAt
        < IdleTimeout.TotalMilliseconds;
}
