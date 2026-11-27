using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round-over half of the result writer: the <c>0x0e</c> record that tells
/// the client's scripts the round is over, and with what verdict.
/// </summary>
/// <remarks>
/// It is written before the result, which is the order the image's match code
/// publishes them in: <c>0x762F38</c> calls <c>P2P_PublishOpcode0E</c> and then
/// writes the result state. The record travels on the round's operations set, so
/// it is numbered with the result that follows it.
/// </remarks>
public sealed partial class MatchResultService
{
    /// <summary>
    /// Writes the closed round's round-over verdict to a peer.
    /// </summary>
    /// <param name="context">Context the verdict is written through.</param>
    /// <param name="generation">Identifier set the round played under.</param>
    private async Task WriteRoundOverAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation)
    {
        if (!MatchRecordPeerUtils.IsAlive(context.Session))
        {
            return;
        }

        var verdict = roundState.LastRoundOverVerdict;
        var ordinal = sequences.Take(generation.Operations);
        var record = new MatchRecordFramesUtils.RecordMessage(
            generation.Operations,
            MatchRoundOverBodiesUtils.RoundOver(verdict),
            ordinal);

        await MatchRecordSendUtils.SendAsync(context, [record]);
        logger.LogDebug(
            "UDP {LocalPort}: writing round {RoundNumber}'s round-over verdict {Verdict} to {RemoteAddress}",
            context.LocalPort,
            roundState.RoundNumber,
            verdict,
            context.Remote);
    }
}
