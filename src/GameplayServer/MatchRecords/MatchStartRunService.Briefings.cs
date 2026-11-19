namespace Mgo2Server.GameplayServer.MatchRecords;

public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Whether the room is between two rounds of one game: the round that just
    /// ended has more of its game to play.
    /// </summary>
    /// <remarks>
    /// <b>That stretch is not a briefing.</b> A game of several rounds is stated as
    /// consecutive identical rotation entries (<c>MatchSettings.IsLastRoundOfGame</c>)
    /// and the captures play those rounds back to back: <c>mgo2-game2</c> re-readies on the
    /// sibling set at t+107.978 and its round opens at t+109.120 on a whole clock,
    /// and a client that joined a round in progress was served that round at 1 324
    /// of 2 400 units with no briefing (<c>mgo2-joined-game-in-progress-last-round</c>
    /// t+59.200, <c>docs/protocol/MATCH_FLOW.md</c> §5).
    /// </remarks>
    private bool IsBetweenRoundsOfGame =>
        roundState.HasRoundEnded
        && !roundState.IsOpen
        && !matchSettings.IsLastRoundOfGame(roundState.RoundNumber);

    /// <summary>
    /// Begins the briefing a peer entering the room is shown, unless the room is
    /// inside a game that has not ended.
    /// </summary>
    private void BeginBriefingForEntry()
    {
        if (roundState.IsOpen || IsBetweenRoundsOfGame || roundState.IsStageEnding || stageBriefingPending)
        {
            logger.LogDebug(
                "The room is inside a game that has not ended, so no briefing begins for the "
                + "peer entering it: it is served the round");
            return;
        }

        BeginBriefing();
    }
}
