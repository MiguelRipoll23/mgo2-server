using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The repeat half of the round opener: the run a peer was written, sent once
/// more one briefing tick behind it, which is the second instant every capture
/// writes.
/// </summary>
/// <remarks>
/// It lives apart from the run's own writer because the two answer different
/// questions — one builds the opening set, this one decides whether the room is
/// still in the phase that set was for — and because the writer had reached the
/// repository's file limit.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Writes a peer the run's records again, one briefing tick after the run
    /// itself, which is the second instant every capture writes.
    /// </summary>
    /// <remarks>
    /// <b>Only while the room is still in the phase the run was written for.</b>
    /// A briefing whose round opened inside the tick is a room whose run the
    /// round's own writer is about to replace, and repeating the briefing's set
    /// over it would put a whole clock on a round already playing. The repeat does
    /// not fault: it runs beside the waits for the round, so an exception here
    /// would be an unobserved one, and a send that fails says the peer is gone,
    /// which is what ending quietly means.
    /// </remarks>
    /// <param name="context">Context the repeat is written through.</param>
    /// <param name="generation">Identifier set the run was written under.</param>
    /// <param name="run">The run as it was written.</param>
    /// <param name="roundNumber">Round number the run was written for.</param>
    /// <param name="wasOpen">Whether a round was playing when it was written.</param>
    private async Task RepeatRunAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        IReadOnlyList<MatchRecordFramesUtils.RecordMessage> run,
        int roundNumber,
        bool wasOpen)
    {
        try
        {
            await Task.Delay(BriefingCountdownBodiesUtils.PublishPeriodMilliseconds);

            if (!MatchRecordPeerUtils.IsAlive(context.Session))
            {
                return;
            }

            if (roundState.RoundNumber != roundNumber || roundState.IsOpen != wasOpen)
            {
                logger.LogDebug(
                    "UDP {LocalPort}: the room moved on before the tick behind the run written to "
                    + "{RemoteAddress}, so the run is not repeated",
                    context.LocalPort,
                    context.Remote);
                return;
            }

            logger.LogDebug(
                "UDP {LocalPort}: repeating the run written to {RemoteAddress} one tick behind it, "
                + "without its opening answer",
                context.LocalPort,
                context.Remote);
            await MatchRecordSendUtils.SendAsync(
                context,
                MatchStartRunFramesUtils.RepeatOf(generation, run));

            // A fresh briefing's repeat carries the item-box groups again, numbered
            // on from the run's own, and the pair goes on behind it for the stretch
            // every capture holds before the briefing goes quiet
            // (ContinueItemBoxGroupsAsync).
            //
            // Its shape is the room's and not the round number's: a room staged
            // mid-rotation writes the filler and keeps writing it, and the two must
            // agree or the groups stop after the first tick (`AddRunItemBoxes`).
            if (!wasOpen && !roundState.HasRoundEnded)
            {
                await RepeatItemBoxGroupsAsync(context, generation);
                await ContinueItemBoxGroupsAsync(context, generation);
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the run written to {RemoteAddress} was not repeated",
                context.LocalPort,
                context.Remote);
        }
    }
}
