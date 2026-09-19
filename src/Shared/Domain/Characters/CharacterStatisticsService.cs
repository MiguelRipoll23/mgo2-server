using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>The statistics one host-reported round contributes to a character.</summary>
public sealed record RoundStatistics
{
    /// <summary>Kills.</summary>
    public int Kills { get; init; }

    /// <summary>Deaths.</summary>
    public int Deaths { get; init; }

    /// <summary>Rounds won.</summary>
    public int Wins { get; init; }

    /// <summary>Stuns delivered.</summary>
    public int Stuns { get; init; }

    /// <summary>Stuns received.</summary>
    public int StunsReceived { get; init; }

    /// <summary>Friendly-fire stuns delivered.</summary>
    public int StunsFriendly { get; init; }

    /// <summary>Headshot kills.</summary>
    public int HeadshotKills { get; init; }

    /// <summary>Headshot deaths.</summary>
    public int HeadshotDeaths { get; init; }

    /// <summary>Headshot stuns delivered.</summary>
    public int HeadshotStuns { get; init; }

    /// <summary>Headshot stuns received.</summary>
    public int HeadshotStunsReceived { get; init; }

    /// <summary>Lock-on kills.</summary>
    public int LockKills { get; init; }

    /// <summary>Lock-on deaths.</summary>
    public int LockDeaths { get; init; }

    /// <summary>Lock-on stuns delivered.</summary>
    public int LockStuns { get; init; }

    /// <summary>Lock-on stuns received.</summary>
    public int LockStunsReceived { get; init; }

    /// <summary>Consecutive kills achieved in the round.</summary>
    public int ConsecutiveKills { get; init; }

    /// <summary>Score earned.</summary>
    public int Score { get; init; }

    /// <summary>Seconds played.</summary>
    public int Time { get; init; }

    /// <summary>Experience earned.</summary>
    public int Experience { get; init; }

    /// <summary>Whether the round was aborted.</summary>
    public bool Aborted { get; init; }

    /// <summary>Game mode the round was played in.</summary>
    public int GameMode { get; init; }
}

/// <summary>
/// Reads the lifetime statistics of a character, summed from its round reports at
/// query time.
/// <para>
/// There is no accumulator: the reports are the store, so a sum is the whole of the
/// derivation and no write path can drift from the read one.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class CharacterStatisticsService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Returns the lifetime statistics of a character, or <c>null</c> when it has no
    /// stored round report at all — the state a character that has never finished a
    /// round is in, and not the same as a character whose statistics are all zero.
    /// </summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterStatistics?> FindByCharacterIdentifierAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var rows = await context.RoundReports
            .AsNoTracking()
            .Where(report => report.TargetCharacterIdentifier == characterIdentifier)
            .GroupBy(report => report.Rule)
            .Select(group => new
            {
                Rule = group.Key,
                Rounds = group.Count(),
                Wins = group.Sum(report => (int)report.Wins),
                Kills = group.Sum(report => (int)report.Kills),
                Deaths = group.Sum(report => (int)report.Deaths),
                Score = group.Sum(report => (int)report.Score),
                Stuns = group.Sum(report => (int)report.Stuns),
                StunsReceived = group.Sum(report => (int)report.StunsReceived),
                HeadshotKills = group.Sum(report => (int)report.HeadshotKills),
                HeadshotDeaths = group.Sum(report => (int)report.HeadshotDeaths),
                HeadshotStuns = group.Sum(report => (int)report.HeadshotStuns),
                HeadshotStunsReceived = group.Sum(report => (int)report.HeadshotStunsReceived),
                LockKills = group.Sum(report => (int)report.LockKills),
                LockDeaths = group.Sum(report => (int)report.LockDeaths),
                LockStuns = group.Sum(report => (int)report.LockStuns),
                LockStunsReceived = group.Sum(report => (int)report.LockStunsReceived),
                Seconds = group.Sum(report => (long)report.Seconds),
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var modes = CharacterStatistics.CreateEmptyModes();
        var rounds = 0;
        var wins = 0;
        var kills = 0;
        var deaths = 0;
        var stuns = 0;
        var stunsReceived = 0;
        var headshotKills = 0;
        var headshotDeaths = 0;
        var lockKills = 0;
        var score = 0;
        long seconds = 0;

        foreach (var row in rows)
        {
            rounds += row.Rounds;
            wins += row.Wins;
            kills += row.Kills;
            deaths += row.Deaths;
            stuns += row.Stuns;
            stunsReceived += row.StunsReceived;
            headshotKills += row.HeadshotKills;
            headshotDeaths += row.HeadshotDeaths;
            lockKills += row.LockKills;
            score += row.Score;
            seconds += row.Seconds;

            if (row.Rule is < 0 or >= CharacterStatistics.ModeCount)
            {
                continue;
            }

            var mode = modes[row.Rule];
            mode.Rounds = row.Rounds;
            mode.Wins = row.Wins;
            mode.Score = row.Score;
            mode.Time = (int)row.Seconds;
            mode.Kills = row.Kills;
            mode.Deaths = row.Deaths;
            mode.Stuns = row.Stuns;
            mode.StunsRec = row.StunsReceived;
            mode.HsKills = row.HeadshotKills;
            mode.HsDeaths = row.HeadshotDeaths;
            mode.HsStuns = row.HeadshotStuns;
            mode.HsStunsRec = row.HeadshotStunsReceived;
            mode.LockKills = row.LockKills;
            mode.LockDeaths = row.LockDeaths;
            mode.LockStuns = row.LockStuns;
            mode.LockStunsRec = row.LockStunsReceived;
        }

        return new CharacterStatistics
        {
            Rounds = rounds,
            Wins = wins,
            Kills = kills,
            Deaths = deaths,
            Stuns = stuns,
            StunsReceived = stunsReceived,
            HeadshotKills = headshotKills,
            HeadshotDeaths = headshotDeaths,
            LockKills = lockKills,
            Score = score,
            TotalTime = (int)seconds,
            Modes = modes,
        };
    }
}
