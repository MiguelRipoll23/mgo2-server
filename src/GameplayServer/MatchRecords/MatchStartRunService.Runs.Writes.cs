using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The single-round half of the round opener's writer: the run a round that is
/// already playing is opened with.
/// </summary>
/// <remarks>
/// Split from <c>MatchStartRunService.Runs.cs</c> so neither file exceeds the size
/// a source file is allowed; the flow that calls it and the run builder it shares
/// are there.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Writes one round's opening run to a peer and repeats it behind itself.
    /// </summary>
    /// <param name="context">Context the run is written through.</param>
    /// <param name="generation">Identifier set the round plays under.</param>
    private async Task WriteRunAsync(PeerContext context, MatchRecordGenerationUtils generation)
    {
        if (roundState.RoundSettings is not { } round)
        {
            // The round closed under the wait — its clock ran out before this
            // peer's worker reached the run — so there is nothing to open it with.
            logger.LogDebug(
                "UDP {LocalPort}: round {RoundNumber} closed before its opening run was written to "
                + "{RemoteAddress}",
                context.LocalPort,
                roundState.RoundNumber,
                context.Remote);
            return;
        }
        await WriteRoundStartStateAsync(context, generation, round);
        await WriteRunCoreAsync(context, generation, round, roundState.RemainingUnits, briefing: false);
    }
}
