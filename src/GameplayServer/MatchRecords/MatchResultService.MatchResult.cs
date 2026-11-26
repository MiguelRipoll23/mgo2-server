namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The game's own result: which side took it, decided from the rounds the room
/// awarded and the score its rule counts.
/// </summary>
/// <remarks>
/// Split from <c>MatchResultService.cs</c> for the size rule. The round's result
/// and the stage's are in the other partials; this is the value the same state
/// write carries at <c>state[3]</c>.
/// </remarks>
public sealed partial class MatchResultService
{
    /// <summary>
    /// The game's result when the round that just ended decided it: the side that
    /// took every round, the side with the higher total score when the rounds were
    /// split, or <c>null</c> when no side can be named.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A split game is decided on the higher total score.</b> Of the replays that
    /// decide a match, three were swept and each states the sweeping side; the fourth
    /// splits its rounds one each and gives the match to the side that lost the last
    /// of them, on the higher total score (<c>replay_360824_6</c>). The totals are
    /// the ones the rule counts (<see cref="MatchScoreService.TeamLimits"/>), so the
    /// side the result screen names is the side the scoreboard puts ahead.
    /// </para>
    /// <para>
    /// <b>A level split names no side.</b> No capture or replay states which side
    /// takes a game whose rounds and totals are both level, so <c>state[3]</c> is
    /// left unwritten there rather than invented; <c>state[2]</c> alone still goes
    /// out with the round's result.
    /// </para>
    /// </remarks>
    private async Task<byte?> DecideMatchResultAsync()
    {
        if (!roundState.LastRoundClosedGame)
        {
            return null;
        }

        var slots = await lobbySlots.ReadAsync(CancellationToken.None);
        var holders = slots
            .Where(slot => slot.Team is not null)
            .GroupBy(slot => slot.Team!.Value)
            .Where(side => side.Any(slot => score.RoundsWon(slot.Slot) > 0))
            .Select(side => side.Key)
            .ToList();
        if (holders.Count == 1)
        {
            return MatchRecordBodiesUtils.ResultCode(holders[0]);
        }

        if (holders.Count == 0)
        {
            return null;
        }

        if (holders.Count > 2)
        {
            logger.LogInformation(
                "The game's rounds went to {SideCount} sides, and this host scores no higher side among them, "
                + "so no match result is written for it",
                holders.Count);
            return null;
        }

        // The one replay that splits its rounds gives the match to the side with the
        // higher total score, and not to the side that took the last round. The
        // totals are the rule's own, so the screen and the result cannot disagree.
        var totals = score.TeamLimits(slots)
            .Where(team => holders.Contains(team.Team))
            .OrderByDescending(team => team.Score)
            .ToList();
        if (totals.Count < 2)
        {
            logger.LogInformation(
                "The game's rounds went to more than one side, but this host scores no higher side among "
                + "them, so no match result is written for it");
            return null;
        }

        if (totals[0].Score > totals[1].Score)
        {
            logger.LogInformation(
                "The game's rounds went to both sides, so it goes to team {Team} on the higher total score "
                + "({Score} against {Other})",
                totals[0].Team,
                totals[0].Score,
                totals[1].Score);
            return MatchRecordBodiesUtils.ResultCode(totals[0].Team);
        }

        logger.LogInformation(
            "The game's rounds went to both sides and their totals are level, so no match result is "
            + "written for it");
        return null;
    }
}
