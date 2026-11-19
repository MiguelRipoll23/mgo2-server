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
/// and its next death takes it to <c>19</c>. The <c>3</c>s and the <c>1</c> are the
/// rule's <b>kill, headshot and combo score awards</b>; the <c>−2</c> is its
/// <b>death score penalty</b>, the negative column 1 of the same rule row
/// (<see cref="MatchScoreRulesUtils"/>). The combo (bit <c>47</c>) grows by one less
/// than the killer's kill streak on each consecutive kill — <c>1, 3, 6</c> for a run
/// of four. A death resets the kill streak; the combo total it has paid stays on
/// the round's score.
/// </para>
/// <para>
/// <b>A headshot is the kill the record says it is.</b> The reference sets the
/// killer's headshot count (bit <c>11</c>) on exactly the kills whose last damage
/// record is subtype <c>0x12</c>: all twelve of them in the capture, and none of the
/// thirty-two kills that ended on an ordinary <c>0x11</c>. The killer's last-result
/// code (bit <c>152</c>) reads <c>5</c> after a headshot and <c>1</c> after any other
/// kill, and the streak (bit <c>153</c>) the run's length. Both ride only when they
/// change, and a death restates them as <c>2</c> and its consecutive-death count.
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
            // A feed for a death the simulation already counted is the same kill,
            // not another one. The report carries no event id, so the pair and a
            // beat-sized window are the identity: the same victim cannot go down
            // twice inside it, while a second kill after a respawn is seconds away.
            var now = Environment.TickCount64;
            if (recentKills.TryGetValue((killerSlot, victimSlot), out var killedAt)
                && now - killedAt < KillDedupMilliseconds)
            {
                logger.LogDebug(
                    "Ignoring a repeated kill report for slot {VictimSlot} by slot {KillerSlot} within "
                    + "{Milliseconds} ms",
                    victimSlot,
                    killerSlot,
                    KillDedupMilliseconds);
                return false;
            }

            recentKills[(killerSlot, victimSlot)] = now;

            // The vitals path and a kill feed can both report one death;
            // count it once and let the other report only credit the killer.
            var victim = RowOf(victimSlot);
            NoteDeathLocked(victimSlot, victim, countDeath);

            if (killerSlot == victimSlot)
            {
                logger.LogDebug(
                    "Player in slot {Slot} killed itself; the death is counted and the kill is not",
                    killerSlot);
                return true;
            }

            // Slot zero is this host's own seat — the dedicated host's character,
            // `server-99999` — and no player or bot of the round holds it. A kill
            // credited there is drawn on a row the room does not have, which is where
            // every bot's kill ended up: the death is counted and the kill is not.
            if (killerSlot == 0)
            {
                logger.LogDebug(
                    "A kill named slot zero, which holds no player of the round; the death in slot {VictimSlot} "
                    + "is counted and the kill is not",
                    victimSlot);
                return true;
            }

            var killer = RowOf(killerSlot);
            killer.Kills++;
            killer.KillsKnown = true;
            killer.KillStreak++;
            killer.ConsecutiveDeaths = 0;
            killer.BestKillStreak = Math.Max(killer.BestKillStreak, killer.KillStreak);
            var restateStreak = killer.KillStreakReset;
            killer.KillStreakReset = false;
            killer.Combo += killer.KillStreak - 1;
            if (headshot)
            {
                killer.Headshots++;
                Queue(killerSlot, MatchScoreboardFramesUtils.HeadshotsBit, killer.Headshots);
            }

            Queue(killerSlot, MatchScoreboardFramesUtils.KillsBit, killer.Kills);
            QueueScore(killerSlot, killer);
            if (killer.KillStreak > 1)
            {
                Queue(killerSlot, MatchScoreboardFramesUtils.ComboBit, killer.Combo);
            }

            QueueResult(
                killerSlot,
                killer,
                headshot ? HeadshotResult : KillResult,
                killer.KillStreak,
                forceStreak: restateStreak);
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
    /// Ends a player's unbroken kill run: the kill streak returns to zero, the
    /// consecutive-death count grows, and the death's last-result code and that
    /// count are stated.
    /// </summary>
    /// <remarks>
    /// <b>The combo total is not rolled back.</b> The reference's combo field
    /// continues across a death (slot 22's 28 at t+184.35 and t+216.41 in
    /// <c>pcapng2</c>), while the next kill's kill streak starts again at one. The
    /// score is therefore recomputed with the combo it already holds, not with a
    /// zero.
    /// </remarks>
    /// <param name="slot">Slot the player holds.</param>
    /// <param name="row">The player's row.</param>
    private void EndKillStreak(byte slot, Row row)
    {
        row.KillStreak = 0;
        row.ConsecutiveDeaths++;
        row.KillStreakReset = true;
        QueueResult(slot, row, DeathResult, row.ConsecutiveDeaths);
    }

    /// <summary>
    /// Queues a player's last-result code and streak value, each only when it differs
    /// from what the room was last told.
    /// </summary>
    /// <param name="slot">Slot the values belong to.</param>
    /// <param name="row">The player's row.</param>
    /// <param name="result">Last-result code to state.</param>
    /// <param name="streak">
    /// Value of the streak bit to state: the kill run after a kill, the
    /// consecutive-death count after a death.
    /// </param>
    /// <param name="forceStreak">
    /// Whether to state the streak even when it equals the last value sent. The
    /// first kill after a death is the case: its streak is one, which the death
    /// result already stated, but the client needs the new run's value on the kill.
    /// </param>
    private void QueueResult(byte slot, Row row, int result, int streak, bool forceStreak = false)
    {
        if (row.SentLastResult != result)
        {
            row.SentLastResult = result;
            Queue(slot, MatchScoreboardFramesUtils.LastResultBit, result);
        }

        if (forceStreak || row.SentStreak != streak)
        {
            row.SentStreak = streak;
            Queue(slot, MatchScoreboardFramesUtils.StreakBit, streak);
        }
    }
}
