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
/// reporter and to the room (<c>PlayerReadyHandler</c>), and the captures show the
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
    PlayerReadyService ready,
    MatchStartRunService matchStart,
    ILogger<BriefingReadyPublishService> logger)
{
    /// <summary>
    /// Spacing between repeats. The captures' repeats are 2.4–4.3 s apart, which is
    /// the same operations tick the briefing clock is written on.
    /// </summary>
    public const int PublishPeriodMilliseconds = 2500;

    /// <summary>
    /// Writes the room's ready set to one peer, repeatedly, until the round opens or
    /// the peer goes.
    /// </summary>
    /// <param name="context">Context the records are written through.</param>
    public async Task PublishAsync(PeerContext context)
    {
        var session = context.Session;

        while (MatchRecordPeerUtils.IsAlive(session) && !roundState.IsOpen)
        {
            // The tick ends the moment the round opens: this task is awaited
            // before the beat, and a period slept out after the run was written
            // would hold the beat's first frame behind it.
            await roundState.WaitBriefingTickAsync(session, PublishPeriodMilliseconds);
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
    }

    /// <summary>
    /// Writes the room's ready set to the peer once.
    /// </summary>
    /// <remarks>
    /// Nothing is written while the room's briefing has heard from nobody: the set
    /// is what the reports make it, and a briefing no player has answered has no
    /// generation of its own to publish under
    /// (<see cref="MatchStartRunService.LastReportIdentifier"/>).
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private async Task PublishOnceAsync(PeerContext context)
    {
        if (matchStart.LastReportIdentifier is not { } reported)
        {
            return;
        }

        var generation = MatchRecordGenerationUtils.OfReadinessReport(reported);
        var publication = ready.BuildPublication(generation);
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
