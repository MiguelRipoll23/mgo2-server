namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The reset half of a briefing's countdown: when the last client leaves, the
/// clock and the closing zero are dropped so the next briefing starts fresh.
/// </summary>
public sealed partial class BriefingCountdownService
{
    /// <summary>Drops the countdown's clock and its closing zero, because the room has emptied.</summary>
    public void Reset()
    {
        lock (gate)
        {
            countdownIdentifier = MatchRecordGenerationUtils.First.Operations;
            startedAt = null;
            countingFrom = null;
            length = TimeSpan.Zero;
            ended = false;
        }
    }
}
