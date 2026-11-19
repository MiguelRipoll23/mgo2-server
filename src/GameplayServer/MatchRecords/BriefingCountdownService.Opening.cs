using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The opening half of a briefing's countdown: the held count a stage end writes
/// before any peer has reported into the briefing.
/// </summary>
public sealed partial class BriefingCountdownService
{
    /// <summary>
    /// Peers the stage-end opening has already written this briefing's held count
    /// to. The count is per peer, and a task started behind the opening must not
    /// write it a second time.
    /// </summary>
    private readonly HashSet<PeerSession> openingPeers = [];

    /// <summary>
    /// Writes the held count of a briefing opened by a stage end, before any peer
    /// has reported into it, once per briefing.
    /// </summary>
    /// <remarks>
    /// The reference host writes this record at the stage end itself
    /// (t+210.237 of `rounded-ended-by-reaching-tickets-limit.pcapng`), and its
    /// client reports into the briefing 0.8 s later. A briefing that waits for the
    /// report before it writes any count has nothing to pull the client off the
    /// result screens, which is the black screen this closes.
    /// </remarks>
    /// <param name="context">Context the record is written through.</param>
    public async Task PublishStageOpeningAsync(PeerContext context)
    {
        if (!Remaining(DateTimeOffset.UtcNow, out var remaining))
        {
            return;
        }

        lock (gate)
        {
            if (!openingPeers.Add(context.Session))
            {
                return;
            }
        }

        try
        {
            await MatchRecordSendUtils.SendAsync(
                context,
                [
                    new(
                        countdownIdentifier,
                        BriefingCountdownBodiesUtils.CountdownBody(
                            BriefingCountdownBodiesUtils.CountdownSeconds(remaining),
                            briefingStates.ReadyMask),
                        sequences.Take(countdownIdentifier)),
                ]);
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the stage-end briefing's held count to {RemoteAddress} was not sent",
                context.LocalPort,
                context.Remote);
        }
    }
}
