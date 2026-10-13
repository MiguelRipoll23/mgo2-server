using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The waiting half of <see cref="MatchRoundStateService"/>: the three waits the
/// channel's writers make on the room's round.
/// </summary>
/// <remarks>
/// It lives apart from the state it waits on because the state is the shared
/// thing and the waits are each caller's own timing — the beat's bounded wait is
/// not the round opener's unbounded one — and together they outgrow the file
/// limit the state alone does not.
/// </remarks>
public sealed partial class MatchRoundStateService
{
    /// <summary>
    /// Waits for the round's opening run to have been written to a peer, so the
    /// beat's offsets are counted from a frame that is already on the wire.
    /// </summary>
    /// <param name="session">Session the run is written to.</param>
    /// <param name="timeoutMilliseconds">How long to wait for it.</param>
    public async Task WaitForRunWrittenAsync(
        PeerSession session,
        int timeoutMilliseconds = RunWaitMilliseconds)
    {
        var waited = 0;
        while (waited < timeoutMilliseconds)
        {
            lock (gate)
            {
                if (writtenRuns.Contains(session) || !isOpen)
                {
                    return;
                }
            }

            if (!MatchRecordPeerUtils.IsAlive(session))
            {
                return;
            }

            await Task.Delay(PollMilliseconds);
            waited += PollMilliseconds;
        }
    }

    /// <summary>
    /// Waits for a round to open, for as long as the caller allows and no
    /// longer, and returns early when the peer the wait is for has gone.
    /// </summary>
    /// <remarks>
    /// The wait is bounded by the caller rather than here because two writers
    /// need two different answers. The beat must go silent for nothing, so it
    /// waits a few tens of seconds and then writes regardless; the round opener
    /// must not write its run before a round exists, so it waits as long as the
    /// peer is there.
    /// </remarks>
    /// <param name="session">Session the waiting writer writes to.</param>
    /// <param name="timeoutMilliseconds">
    /// How long to wait, or <see cref="WaitWhilePeerIsThere"/> to wait for as
    /// long as the peer is there.
    /// </param>
    public async Task WaitForOpenAsync(
        PeerSession session,
        int timeoutMilliseconds = OpenWaitMilliseconds)
    {
        if (IsOpen)
        {
            return;
        }

        logger.LogDebug(
            "Waiting up to {WaitMilliseconds} ms for a round to open before writing to {RemoteAddress}",
            timeoutMilliseconds,
            session.RemoteAddress);

        var waited = 0;
        while (!IsOpen && waited < timeoutMilliseconds)
        {
            if (!MatchRecordPeerUtils.IsAlive(session))
            {
                logger.LogDebug(
                    "Stopped waiting for a round for {RemoteAddress}: the peer has gone quiet",
                    session.RemoteAddress);
                return;
            }

            await Task.Delay(PollMilliseconds);
            waited += PollMilliseconds;
        }

        if (!IsOpen && waited >= timeoutMilliseconds)
        {
            logger.LogDebug(
                "No round opened within {WaitMilliseconds} ms for {RemoteAddress}",
                timeoutMilliseconds,
                session.RemoteAddress);
        }
    }

    /// <summary>
    /// Waits for a round to end, and returns early when the peer has gone.
    /// </summary>
    /// <param name="roundNumber">Round to wait for the end of.</param>
    /// <param name="session">Session the waiting writer writes to.</param>
    /// <param name="timeoutMilliseconds">
    /// How long to wait. It defaults to <see cref="WaitWhilePeerIsThere"/>,
    /// because a caller waits for a round to end in order to write the next
    /// one and a round's length is not its to choose.
    /// </param>
    public async Task WaitForCloseAsync(
        int roundNumber,
        PeerSession session,
        int timeoutMilliseconds = WaitWhilePeerIsThere)
    {
        var waited = 0;
        while (RoundNumber == roundNumber && IsOpen && waited < timeoutMilliseconds)
        {
            if (!MatchRecordPeerUtils.IsAlive(session))
            {
                return;
            }

            await Task.Delay(PollMilliseconds);
            waited += PollMilliseconds;
        }
    }
}
