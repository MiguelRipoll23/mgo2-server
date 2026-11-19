namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The counters one player finished a round with, as the round's report stores
/// them.
/// </summary>
/// <param name="Kills">Kills the round credited to the player.</param>
/// <param name="Deaths">Deaths the round recorded for the player.</param>
/// <param name="Score">Score the player finished the round on.</param>
/// <param name="HeadshotKills">Kills the round credited as headshots.</param>
/// <param name="BestStreak">Longest consecutive kill run the player had.</param>
public sealed record PlayerRoundStats(
    int Kills,
    int Deaths,
    int Score,
    int HeadshotKills,
    int BestStreak);

public sealed partial class MatchScoreService
{
    /// <summary>
    /// Takes the round's counters before the next open clears them.
    /// </summary>
    /// <remarks>
    /// A snapshot is taken rather than the live rows read later because the
    /// report is written beside the round's end and the next round may open
    /// while the database is still being reached. The copy is the report's, and
    /// the live score goes on moving for the screens.
    /// </remarks>
    /// <returns>Each player's counters, keyed by the room's player-table slot.</returns>
    public Dictionary<byte, PlayerRoundStats> SnapshotRound()
    {
        lock (gate)
        {
            return rows.ToDictionary(
                entry => entry.Key,
                entry => new PlayerRoundStats(
                    entry.Value.Kills,
                    entry.Value.Deaths,
                    entry.Value.Score,
                    entry.Value.Headshots,
                    entry.Value.BestStreak));
        }
    }
}
