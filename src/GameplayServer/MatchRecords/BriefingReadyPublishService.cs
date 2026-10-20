using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// Repeats the room's ready set to one peer for as long as its room is briefing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The host answers a briefing more than once, and this is the rest of the
/// answers.</b> Every report already gets the room's ready set written back to the
/// reporter and to the room (<c>JoinReportHandler</c>), and the captures show the
/// host repeating that set on its next operations tick without waiting for another
/// report: in <c>mgo2-game-started-on-briefing</c> the first answer goes out at
/// t+56.123 and the same set again at t+60.383, the second at t+112.619 and again
/// at t+114.978, the third at t+144.750 and again at t+147.306, and the fourth at
/// t+154.370 and again at t+156.885 — 2.4–4.3 s apart each time, with the room's
/// round still 40 s out (<c>docs/protocol/BRIEFING_FLOW.md</c> §2). A host that
/// answered only when a report arrived would leave a client whose report had been
/// heard, and whose room was still waiting on somebody else, with nothing on the
/// wire that says so.
/// </para>
/// <para>
/// <b>The set is the same one the report's answer carries, under the generation the
/// room is briefing on.</b> Each record is one reporting player's own byte, in
/// publication order, and its fourth byte is that player's place in the
/// publication — which is why a repeat repeats those values rather than taking new
/// ones. The identifier is the payload record of the generation the room's last
/// report arrived on: the capture's set moves from <c>0x12de</c> to
/// <c>0x1ade</c> when its client re-reports under the other, and its answer moves
/// with it.
/// </para>
/// <para>
/// <b>It does not fault.</b> It runs beside the waits for the round rather than
/// inside them, so a failed send says what ending it says anyway: the peer is gone.
/// </para>
/// </remarks>
/// <param name="roundState">The room's round, whose opening ends the publication.</param>
/// <param name="ready">The room's briefing states, which are what is published.</param>
/// <param name="matchStart">Writer of the round, whose last report names the generation.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BriefingReadyPublishService(
    MatchRoundStateService roundState,
    BriefingStateService briefingStates,
    MatchStartRunService matchStart,
    ILogger<BriefingReadyPublishService> logger)
{
    /// <summary>
    /// Spacing between repeats: the briefing's one tick, which the captures' repeats
    /// and the briefing clock are both written on.
    /// </summary>
    /// <remarks>
    /// <b>2.35 s and not 2.5 s.</b> The repeats measured 2.4 to 4.3 s apart, a
    /// missed tick being the upper half of that range, and the seven-byte countdown
    /// written beside them medians 2.372 s over the ten reference captures. The
    /// period is <see cref="MatchRoundStateService.BriefingTickMilliseconds"/>, which
    /// holds the measurement.
    /// </remarks>
    public const int PublishPeriodMilliseconds = MatchRoundStateService.BriefingTickMilliseconds;

    /// <summary>
    /// Writes the room's ready set to one peer, repeatedly, until the peer's own
    /// run is written, the round opens, or the peer goes — and once more on the
    /// tick after that run.
    /// </summary>
    /// <remarks>
    /// <b>The run is followed by one repeat and by nothing else.</b> Both reference
    /// hosts write the set with the run the peer's report asked for and again on
    /// their next operations tick, 2.1 to 2.4 s later, and not again while the
    /// briefing runs: <c>mgo2-started-briefing-then-solo-game</c> writes
    /// <c>e0 03 …</c> at t+57.626 and t+59.993 and nothing of the set until its
    /// round, and <c>mgo2-game-started-on-briefing</c> answers its first report at
    /// t+56.123 on <c>0x12de</c> and repeats the same set at t+60.383, its run
    /// having gone out at t+58.255. The repeats after that are answers to other
    /// players' reports, which the handler writes
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §15).
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var session = context.Session;

        while (MatchRecordPeerUtils.IsAlive(session)
            && !roundState.IsOpen
            && !roundState.HasRunWritten(session))
        {
            // The tick ends the moment the round opens: this task is awaited
            // before the beat, and a period slept out after the run was written
            // would hold the beat's first frame behind it.
            await roundState.WaitBriefingTickAsync(session);
            if (!MatchRecordPeerUtils.IsAlive(session) || roundState.IsOpen)
            {
                return;
            }

            try
            {
                await PublishOnceAsync(context);
            }
            catch (Exception exception)
            {
                logger.LogDebug(
                    exception,
                    "UDP {LocalPort}: the ready-set publication to {RemoteAddress} stopped on a failed send",
                    context.LocalPort,
                    session.RemoteAddress);
                return;
            }
        }

        // The one repeat the run is followed by. It is written on the same tick the
        // loop above slept out, under the set the peer's own report named, and it
        // is the last thing this service writes for the peer: a room whose set
        // moves again says so through the handler that took the report.
        if (!MatchRecordPeerUtils.IsAlive(session) || roundState.IsOpen)
        {
            return;
        }

        await roundState.WaitBriefingTickAsync(session);
        if (!MatchRecordPeerUtils.IsAlive(session) || roundState.IsOpen)
        {
            return;
        }

        try
        {
            await PublishOnceAsync(context);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the ready-set publication to {RemoteAddress} stopped on a failed send",
                context.LocalPort,
                session.RemoteAddress);
        }
    }

    /// <summary>
    /// Writes the room's ready set to the peer once.
    /// </summary>
    /// <remarks>
    /// Nothing is written while the room's briefing has heard from nobody: the set
    /// is what the reports make it, and a briefing no player has answered has no
    /// generation of its own to publish under
    /// (<see cref="MatchStartRunService.LastJoinReportIdentifier"/>).
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private async Task PublishOnceAsync(PeerContext context)
    {
        if (matchStart.LastJoinReportIdentifier is not { } reported)
        {
            return;
        }

        var generation = MatchRecordGenerationUtils.OfJoinReport(reported);
        var publication = briefingStates.BuildPublication(generation);
        if (publication.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: repeating the room's {RecordCount} ready state(s) to {RemoteAddress} on "
            + "0x{Identifier:x4} while the briefing waits",
            context.LocalPort,
            publication.Count,
            context.Remote,
            generation.MatchStartPayload);

        await RosterRunSendUtils.SendAsync(context, publication);
    }
}
