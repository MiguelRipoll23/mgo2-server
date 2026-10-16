using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The waiting half of <see cref="MatchRoundStateService"/>: the three waits the
/// channel's writers make on the room's round.
/// </summary>
/// <remarks>
/// It lives apart from the state it waits on because the state is the shared
/// thing and the waits are each caller's own timing, and together they outgrow
/// the file limit the state alone does not.
/// </remarks>
public sealed partial class MatchRoundStateService
{
    /// <summary>
    /// Whether this peer has been served a run for the phase now running: the
    /// briefing's while the room is still briefed, or the round's once one plays.
    /// </summary>
    /// <remarks>
    /// <b>It is the seam the briefing's own writers change hands on.</b> The run is
    /// the peer's own briefing answer, and every briefing record is dated from it:
    /// the countdown is written from it and not before it
    /// (<see cref="BriefingCountdownService.PublishAsync"/>), the ready set is
    /// repeated once on the tick after it
    /// (<see cref="BriefingReadyPublishService.PublishAsync"/>), and the room's
    /// rules pair and count are the beat's from there, the beat starting on the run
    /// itself (<see cref="MatchRecordService"/>). A run written for the round opens
    /// the same latch, which is why the writers turn on it rather than on the
    /// round's own open. The measurement is
    /// <c>docs/analysis/join-stall-after-mode2.md</c> §15; §10 of that file read the
    /// countdown as ending on the run, and the captures place it after it.
    /// </remarks>
    /// <param name="session">Session the run would have been written to.</param>
    /// <returns><c>true</c> once a run has been written to that peer.</returns>
    public bool HasRunWritten(PeerSession session)
    {
        lock (gate)
        {
            return writtenRuns.Contains(session);
        }
    }

    /// <summary>
    /// Waits for the run the beat's offsets are counted from to have been written
    /// to a peer.
    /// </summary>
    /// <remarks>
    /// <b>It is the peer's own run and not a round that is waited for.</b>
    /// Everything the beat writes is measured as an offset from the run, and a
    /// room being briefed is served one of its own — the run the briefing's first
    /// report asks for, two to three seconds after it arrives
    /// (`docs/protocol/BRIEFING_FLOW.md` §2). A wait for a round would hold the
    /// beat behind a round that a room of one never opens, which is the shape of a
    /// client that is answered its run and then hears nothing else at all; the
    /// wait is bounded by the peer instead, because a peer that has not reported
    /// yet has no run to wait for and one that has gone must not be waited on.
    /// </remarks>
    /// <param name="session">Session the run is written to.</param>
    /// <param name="timeoutMilliseconds">
    /// How long to wait for it. It defaults to <see cref="WaitWhilePeerIsThere"/>,
    /// because the run is written on the client's own report.
    /// </param>
    public async Task WaitForRunWrittenAsync(
        PeerSession session,
        int timeoutMilliseconds = WaitWhilePeerIsThere)
    {
        var waited = 0;
        while (waited < timeoutMilliseconds)
        {
            if (HasRunWritten(session))
            {
                return;
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
    /// Waits out one briefing tick: the period the caller names, or until the
    /// round opens, whichever comes first, and returns early when the peer has
    /// gone.
    /// </summary>
    /// <remarks>
    /// <b>The round's own opening cuts the tick short.</b> The briefing writers run
    /// beside the waits for the round and are awaited before the beat, so a tick
    /// that slept its whole period after the round had opened would hold the beat's
    /// first frame — and the item-box group and the vitals onset counted from it —
    /// behind a timer nothing is waiting on. The poll is this class's own, so the
    /// beat follows the run by at most it.
    /// </remarks>
    /// <param name="session">Session the waiting writer writes to.</param>
    /// <param name="milliseconds">Length of the tick.</param>
    public async Task WaitBriefingTickAsync(PeerSession session, int milliseconds)
    {
        var waited = 0;
        while (!IsOpen && waited < milliseconds)
        {
            if (!MatchRecordPeerUtils.IsAlive(session))
            {
                return;
            }

            await Task.Delay(PollMilliseconds);
            waited += PollMilliseconds;
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
