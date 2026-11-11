namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The kill half of the score: what one death moves on the killer's row and the
/// victim's, in the shape the reference host writes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on <c>working-server-v2.pcapng</c>.</b> A death writes the victim's
/// deaths and score first and the killer's kills and score behind them, and the
/// score is <c>3·kills + headshots + combo − 2·deaths</c>, clamped at zero: slot 10
/// stands at <c>5 kills, 3 headshots, combo 6</c> and scores <c>21</c> at t+74.02,
/// and its next death takes it to <c>19</c>. The combo (bit <c>47</c>) grows by one
/// less than the killer's streak on each consecutive kill — <c>1, 3, 6</c> for a run
/// of four — and a death ends the run.
/// </para>
/// <para>
/// <b>A headshot is the kill the record says it is.</b> The reference sets the
/// killer's headshot count (bit <c>11</c>) on exactly the kills whose last damage
/// record is subtype <c>0x12</c>: all twelve of them in the capture, and none of the
/// thirty-two kills that ended on an ordinary <c>0x11</c>. The killer's last-result
/// code (bit <c>152</c>) reads <c>5</c> after a headshot and <c>1</c> after any other
/// kill, and the streak (bit <c>153</c>) the run's length. Both ride only when they
/// change, and a death restates them as <c>2</c> and <c>1</c>.
/// </para>
/// </remarks>
public sealed partial class MatchScoreService
{
    /// <summary>Last-result code after an ordinary kill.</summary>
    private const int KillResult = 1;

    /// <summary>Last-result code a death leaves on the victim.</summary>
    private const int DeathResult = 2;

    /// <summary>Last-result code after a headshot kill.</summary>
    private const int HeadshotResult = 5;

    /// <summary>
    /// Records a kill and the death that came with it, which is what a kill-feed
    /// record names: the killer's slot and the victim's.
    /// </summary>
    /// <remarks>
    /// A killer that is also the victim is a suicide: it is a death and not a
    /// kill, which is the replay parser's own reading of the feed
    /// (<c>docs/analysis/replay-console/rpdt.js</c>).
    /// </remarks>
    /// <param name="killerSlot">Slot the feed names as the killer.</param>
    /// <param name="victimSlot">Slot the feed names as the victim.</param>
    /// <param name="countDeath">
    /// Whether this call is the one that counts the death. A player's own vitals
    /// report reaching zero is counted where it arrives
    /// (<see cref="Commands.Peer.Gameplay.PlayerVitalsHandler"/>), so a kill a bot
    /// lands on a player is credited here without counting that death a second
    /// time.
    /// </param>
    /// <param name="headshot">Whether the blow that ended the victim was a headshot.</param>
    /// <returns><c>true</c> when the round's score changed.</returns>
    public bool NoteKill(byte killerSlot, byte victimSlot, bool countDeath = true, bool headshot = false)
    {
        lock (gate)
        {
            var victim = RowOf(victimSlot);
            if (countDeath)
            {
                victim.Deaths++;
                Queue(victimSlot, MatchScoreboardFramesUtils.DeathsBit, victim.Deaths);
                QueueScore(victimSlot, victim);
            }

            // A death ends the victim's run, and the reference restates the
            // last-result code and streak the player now holds.
            victim.Streak = 0;
            QueueResult(victimSlot, victim, DeathResult, 1);

            if (killerSlot == victimSlot)
            {
                logger.LogDebug(
                    "Player in slot {Slot} killed itself; the death is counted and the kill is not",
                    killerSlot);
                return true;
            }

            var killer = RowOf(killerSlot);
            killer.Kills++;
            killer.KillsKnown = true;
            killer.Streak++;
            killer.Combo += killer.Streak - 1;
            if (headshot)
            {
                killer.Headshots++;
                Queue(killerSlot, MatchScoreboardFramesUtils.HeadshotsBit, killer.Headshots);
            }

            Queue(killerSlot, MatchScoreboardFramesUtils.KillsBit, killer.Kills);
            QueueScore(killerSlot, killer);
            if (killer.Streak > 1)
            {
                Queue(killerSlot, MatchScoreboardFramesUtils.ComboBit, killer.Combo);
            }

            QueueResult(killerSlot, killer, headshot ? HeadshotResult : KillResult, killer.Streak);
            logger.LogDebug(
                "Player in slot {KillerSlot} killed the player in slot {VictimSlot}{Headshot}; {Kills} kill(s), "
                + "{Deaths} death(s)",
                killerSlot,
                victimSlot,
                headshot ? " with a headshot" : string.Empty,
                killer.Kills,
                victim.Deaths);
            return true;
        }
    }

    /// <summary>
    /// Queues a player's last-result code and streak, each only when it differs from
    /// what the room was last told.
    /// </summary>
    /// <param name="slot">Slot the values belong to.</param>
    /// <param name="row">The player's row.</param>
    /// <param name="result">Last-result code to state.</param>
    /// <param name="streak">Streak to state.</param>
    private void QueueResult(byte slot, Row row, int result, int streak)
    {
        if (row.SentLastResult != result)
        {
            row.SentLastResult = result;
            Queue(slot, MatchScoreboardFramesUtils.LastResultBit, result);
        }

        if (row.SentStreak != streak)
        {
            row.SentStreak = streak;
            Queue(slot, MatchScoreboardFramesUtils.StreakBit, streak);
        }
    }
}
