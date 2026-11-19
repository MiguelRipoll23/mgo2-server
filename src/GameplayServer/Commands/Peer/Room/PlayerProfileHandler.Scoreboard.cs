using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

public sealed partial class PlayerProfileHandler
{
    /// <summary>
    /// Merges a scoreboard delta a peer wrote into the host's own score: the bits
    /// this file names, and nothing for the ones it does not.
    /// </summary>
    /// <param name="context">Context the delta arrived through.</param>
    /// <param name="delta">Slot and values the delta carried.</param>
    private void MergeScoreboard(PeerContext context, MatchScoreboardFramesUtils.ScoreboardDelta delta)
    {
        logger.LogDebug(
            "UDP {LocalPort}: {RemoteAddress} reports a scoreboard delta for slot {Slot} with "
            + "{ValueCount} value(s)",
            context.LocalPort,
            context.Remote,
            delta.Slot,
            delta.Values.Count);

        foreach (var value in delta.Values)
        {
            switch (value.Bit)
            {
                case MatchScoreboardFramesUtils.KillsBit:
                    score.NoteKills(delta.Slot, value.Value);
                    break;
                case MatchScoreboardFramesUtils.DeathsBit:
                    score.NoteDeaths(delta.Slot, value.Value);
                    break;
                case MatchScoreboardFramesUtils.ScoreBit:
                    score.NoteScore(delta.Slot, value.Value);
                    break;
                default:
                    break;
            }
        }
    }
}
