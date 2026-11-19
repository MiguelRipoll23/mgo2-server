namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The counters the round is scored from, one row per player, and the pending
/// scoreboard bits waiting to be published for them.
/// </summary>
/// <remarks>
/// The two streak fields are counters, not points: the score awards and the death
/// score penalty are coefficients in <see cref="MatchScoreRulesUtils"/>. A kill
/// grows <c>KillStreak</c>; a death clears it and grows <c>ConsecutiveDeaths</c>,
/// which is the value bit <c>153</c> states on a death.
/// </remarks>
public sealed partial class MatchScoreService
{
    private sealed class Row
    {
        public int Kills;
        public int Deaths;
        public int Score;
        public bool KillsKnown;
        public bool ScoreKnown;
        public int Headshots;
        public int KillStreak;
        public int BestKillStreak;
        public int ConsecutiveDeaths;
        public int Combo;
        public bool KillStreakReset;
        public int SentLastResult = -1;
        public int SentStreak = -1;
    }

    private Row RowOf(byte slot)
    {
        if (!rows.TryGetValue(slot, out var row))
        {
            row = new Row();
            rows[slot] = row;
        }

        return row;
    }

    private void Queue(byte slot, int bit, int value)
    {
        if (!pending.TryGetValue(slot, out var values))
        {
            values = [];
            pending[slot] = values;
        }

        values[bit] = value;
    }
}
