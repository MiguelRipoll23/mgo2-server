using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The side score every result screen states: the rounds each player's team has
/// won in the game now playing.
/// </summary>
/// <remarks>
/// It is split from <c>MatchScoreService.cs</c> for the size rule, and because it
/// outlives what the rest of that file counts: the round's kills, deaths and score
/// are cleared when the next round opens, and a game's rounds won are not.
/// </remarks>
public sealed partial class MatchScoreService
{
    /// <summary>
    /// Rounds each slot's side has won in the game now playing. It is apart from
    /// <see cref="rows"/> because a round's counters are cleared at every open and
    /// this one is the game's.
    /// </summary>
    private readonly Dictionary<byte, int> roundWins = [];

    /// <summary>
    /// Awards the round to the players of the side that won it, and queues the
    /// scoreboard change.
    /// </summary>
    /// <remarks>
    /// <b>The counter is the game's, and it steps a whole side at once.</b> It is
    /// the scoreboard's <see cref="MatchScoreboardFramesUtils.RoundsWonBit"/>, which
    /// the replay console names "rounds won (whole team at once, +5)" and the corpus
    /// reads against <c>state[2]</c> over 28 round ends in 16 replays: the code for a
    /// side stepped exactly that side's players 14 of 14 and 10 of 10, and nobody
    /// stepped on the two draws and the two no-result rounds
    /// (<c>.gametools/replay-console/app.js</c>). It is the number the result screens
    /// put at the top — a side's <c>02</c> against the other's <c>00</c> — so a
    /// round that names no side credits nobody.
    /// </remarks>
    /// <param name="slots">Slots of the players on the winning side.</param>
    /// <returns>How many players were credited.</returns>
    public int NoteRoundWin(IReadOnlyCollection<byte> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        lock (gate)
        {
            foreach (var slot in slots)
            {
                var wins = roundWins.TryGetValue(slot, out var stored) ? stored + 1 : 1;
                roundWins[slot] = wins;
                Queue(slot, MatchScoreboardFramesUtils.RoundsWonBit, wins);
                logger.LogDebug(
                    "Player in slot {Slot} has now won {Wins} round(s) of the game",
                    slot,
                    wins);
            }

            return slots.Count;
        }
    }

    /// <summary>
    /// The rounds the player in a slot has won in the game now playing.
    /// </summary>
    /// <remarks>
    /// It is what the game's own result is read from: the side whose players hold
    /// the rounds is the side that took the game, which is how the one split replay
    /// contradicts a match decided by its last round.
    /// </remarks>
    /// <param name="slot">Roster slot the player holds.</param>
    /// <returns>Rounds the slot's player has won since the game's counters started.</returns>
    public int RoundsWon(byte slot)
    {
        lock (gate)
        {
            return roundWins.TryGetValue(slot, out var wins) ? wins : 0;
        }
    }

    /// <summary>
    /// Starts the rounds-won counters over, which is what a decided game does: the
    /// score its result screen states is that game's and not the process's.
    /// </summary>
    /// <remarks>
    /// It is called once the game's last round has been awarded and published, so
    /// the final number reaches the screen before the next game starts from zero.
    /// A room that empties clears it too — the match no player is left in is over.
    /// </remarks>
    public void ResetRoundWins()
    {
        lock (gate)
        {
            if (roundWins.Count == 0)
            {
                return;
            }

            logger.LogInformation(
                "The game is over; the rounds-won counters of {PlayerCount} player(s) start over",
                roundWins.Count);
            roundWins.Clear();
        }
    }
}
