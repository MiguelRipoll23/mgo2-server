using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Builds the per-mode statistics matrix of the personal-stats burst (0x4105), which
/// the screen asks for twice: the cumulative page and the monthly one.
/// <para>
/// Every cell is addressed by the client's parser as a grid position, so the row and
/// column counts here are the layout, not a presentation choice.
/// </para>
/// </summary>
public static class PersonalStatisticsMatrixBuilder
{
    /// <summary>Number of mode rows the statistics model carries.</summary>
    private const int ModeRows = 8;

    /// <summary>
    /// Row slots of the grid, in the order the 1.36 parser reads them.
    /// <para>
    /// Its loop walks eighteen row slots and steps over six of them — 8, 9, 10, 11,
    /// 13 and 14 — so twelve rows travel per packet, and the disc build's eight are
    /// the first eight of them, in the same order and at the same grid positions.
    /// The four rows the disc build has no mode for are served as zeros rather than
    /// carrying an invented statistic, which is what the rest of this builder does
    /// with every figure it cannot measure.
    /// </para>
    /// </summary>
    private static readonly int[] GridRows = [0, 1, 2, 3, 4, 5, 6, 7, 12, 15, 16, 17];

    /// <summary>Number of columns in a mode row.</summary>
    private const int StatColumns = 18;

    /// <summary>Column carrying the score, the only signed one.</summary>
    private const int ScoreColumn = 3;

    /// <summary>Summary column carrying the play time.</summary>
    private const int SummaryPlaySecondsColumn = 17;

    /// <summary>
    /// Summary column carrying the total rewards. It is the cell the client's
    /// character block holds at `T+0x484` — the player-details card's "TOTAL
    /// REWARDS" figure — because the eighth wire block is the card's summary row,
    /// not a game mode. It is deliberately not the level: the card derives that
    /// from the experience the header carries.
    /// </summary>
    private const int SummaryTotalRewardsColumn = 13;

    /// <summary>
    /// Builds one mode matrix.
    /// <para>
    /// The eighth row block is not a mode: it is the summary row the player-details
    /// card reads, and only the cumulative page fills it, because the card reads
    /// matrix zero and nothing is known to read the other. The four rows after it
    /// exist only in the 1.36 grid and are sent empty.
    /// </para>
    /// </summary>
    /// <param name="statistics">Statistics of the character, when it has any.</param>
    /// <param name="page">Page selector: zero cumulative, one monthly.</param>
    /// <param name="character">Character the matrix describes.</param>
    public static byte[] Build(CharacterStatistics? statistics, int page, Character character)
    {
        var matrix = new PacketWriter();
        matrix.WriteUInt32(0);
        matrix.WriteUInt32((uint)page);

        foreach (var gridRow in GridRows)
        {
            for (var column = 0; column < StatColumns; column++)
            {
                // Grid rows past the disc build's eight are slots the 1.36 client
                // reserves for modes this server does not serve; they travel as
                // zeros rather than repeating a neighbouring row's figures.
                matrix.WriteUInt32(gridRow < ModeRows ? ReadStatistic(statistics, gridRow, column) : 0);
            }
        }

        if (page != 0)
        {
            return matrix.Build();
        }

        // The summary row feeds the player-details card: play time and the
        // character's total rewards.
        var payload = matrix.Build();
        var summaryBase = 8 + (ModeRows - 1) * StatColumns * 4;
        var playSeconds = character.CreatedAt.ToUnixTimeSeconds() > 0 && statistics is not null ? statistics.TotalTime : 0;
        BinaryUtility.WriteUInt32BigEndian(payload, summaryBase + SummaryPlaySecondsColumn * 4, (uint)playSeconds);
        BinaryUtility.WriteUInt32BigEndian(
            payload,
            summaryBase + SummaryTotalRewardsColumn * 4,
            (uint)character.TotalRewards);
        return payload;
    }

    private static uint ReadStatistic(CharacterStatistics? statistics, int mode, int column)
    {
        if (statistics is null)
        {
            return 0;
        }

        var modeStatistics = statistics.ForMode(mode);
        var values = new int[]
        {
            modeStatistics.Kills,
            modeStatistics.Deaths,
            modeStatistics.LockKills,
            modeStatistics.Score,
            modeStatistics.Stuns,
            modeStatistics.StunsRec,
            modeStatistics.HsKills,
            modeStatistics.HsDeaths,
            modeStatistics.HsStuns,
            modeStatistics.HsStunsRec,
            modeStatistics.LockStuns,
            modeStatistics.LockDeaths,
            modeStatistics.LockStunsRec,
            modeStatistics.Score,
            modeStatistics.Rounds,
            0,
            modeStatistics.Wins,
            modeStatistics.Time,
        };

        var value = column < values.Length ? values[column] : 0;
        // Column three carries the score, the only signed column.
        return column == ScoreColumn ? unchecked((uint)value) : (uint)value;
    }
}
