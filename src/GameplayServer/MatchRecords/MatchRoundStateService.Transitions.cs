using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The round's transitions that are not a round opening or closing: the reset
/// an emptied room takes, and the result the round before it ended on.
/// </summary>
/// <remarks>
/// It is split from <c>MatchRoundStateService.cs</c> for the size rule and
/// because the two members here answer a different question from the rest: the
/// main file is the round now playing, and this is what the room does around it
/// — the reset when nobody is left in it
/// (<see cref="MatchStartRunService.ResetWhenEmpty"/>), and the result a
/// closed round leaves for the writers that publish it.
/// </remarks>
public sealed partial class MatchRoundStateService
{
    /// <summary>
    /// Returns an emptied room to its first round: the round now playing is
    /// dropped without a result and the count, the clock and the settings start
    /// over.
    /// </summary>
    /// <remarks>
    /// <b>A room nobody is in has no round.</b> The count, the clock and the
    /// settings belong to a match, and the match whose clients have all gone is
    /// over: the next client into the room arrives at its first round and not into
    /// the one that was playing when the room emptied. It is the same reason the
    /// settings are resolved again at the next open. Nothing is written here — the
    /// opener that owns the transition is the one that publishes it
    /// (<see cref="MatchStartRunService.ResetWhenEmpty"/>).
    /// </remarks>
    /// <returns><c>true</c> when a round was playing and has been dropped.</returns>
    public bool Reset()
    {
        lock (gate)
        {
            var ended = isOpen;
            isOpen = false;
            lastRoundResult = null;
            roundSettings = null;
            openedAt = null;
            roundNumber = 0;
            vitalsArrived = false;
            writtenRuns.Clear();
            logger.LogInformation(
                ended
                    ? "The room emptied; the round count, the round's clock and the round's tickets start over"
                    : "The room emptied between rounds; the round count, the round's clock and the round's "
                        + "tickets start over");
            return ended;
        }
    }

    /// <summary>
    /// The result the round that just ended was closed with, or <c>null</c> when
    /// this host had none for it.
    /// </summary>
    /// <remarks>
    /// It is kept past the round's close because the result is written behind it:
    /// a peer's round watcher sees the round close and writes the write it never
    /// had while the round was playing.
    /// </remarks>
    public byte? LastRoundResult
    {
        get
        {
            lock (gate)
            {
                return lastRoundResult;
            }
        }
    }
}
