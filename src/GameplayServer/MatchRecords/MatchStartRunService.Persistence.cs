using Mgo2Server.GameplayServer.Match;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Writes the round that just closed to the room's own rows.
    /// </summary>
    /// <remarks>
    /// The round and its opening instant are passed in because the close clears
    /// the room's copy, and the report needs the entry the round actually played.
    /// The write snapshots the score synchronously, before its first await, so a
    /// slow database cannot lose a round's counters to the next open.
    /// </remarks>
    /// <param name="round">The entry the closed round played, or <c>null</c>.</param>
    /// <param name="openedAt">When the closed round opened, or <c>null</c>.</param>
    /// <param name="roundResult">Result code the round closed on, or <c>null</c>.</param>
    private void RecordClosedRound(
        MatchRotationEntry? round,
        DateTimeOffset? openedAt,
        byte? roundResult)
    {
        if (round is null)
        {
            return;
        }

        var seconds = openedAt is { } start
            ? (int)Math.Max(0, Math.Round((DateTimeOffset.UtcNow - start).TotalSeconds))
            : 0;
        _ = persistence.RecordRoundAsync(round, roundResult, seconds);
    }

    /// <summary>
    /// Stages the rotation entry the next briefing is on in the room's row.
    /// </summary>
    /// <param name="roundNumber">The round whose stage end released the briefing.</param>
    private async Task PublishBriefingStageAsync(int roundNumber)
    {
        try
        {
            await matchSettings.PublishNextGameAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The stage that closed with round {RoundNumber} began its next briefing, but the "
                + "room row could not be staged on that briefing's rotation entry",
                roundNumber);
        }
    }
}
