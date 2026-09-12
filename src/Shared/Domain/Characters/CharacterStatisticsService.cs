using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
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

    /// <summary>Consecutive deaths suffered in the round.</summary>
    public int ConsecutiveDeaths { get; init; }

    /// <summary>Consecutive headshots achieved in the round.</summary>
    public int ConsecutiveHeadshots { get; init; }

    /// <summary>Suicides.</summary>
    public int Suicides { get; init; }

    /// <summary>Score earned.</summary>
    public int Score { get; init; }

    /// <summary>Experience earned.</summary>
    public int Experience { get; init; }

    /// <summary>Rolls performed.</summary>
    public int Rolls { get; init; }

    /// <summary>Close-quarters-combat holds applied.</summary>
    public int CqcGiven { get; init; }

    /// <summary>Close-quarters-combat holds taken.</summary>
    public int CqcTaken { get; init; }

    /// <summary>Knife kills.</summary>
    public int KnifeKills { get; init; }

    /// <summary>Knife stuns.</summary>
    public int KnifeStuns { get; init; }

    /// <summary>Team kills.</summary>
    public int TeamKills { get; init; }

    /// <summary>Melee attacks delivered.</summary>
    public int Melee { get; init; }

    /// <summary>Melee attacks received.</summary>
    public int MeleeReceived { get; init; }

    /// <summary>Radio messages sent.</summary>
    public int Radio { get; init; }

    /// <summary>Chat messages sent.</summary>
    public int Chat { get; init; }

    /// <summary>Salutes performed.</summary>
    public int Salutes { get; init; }

    /// <summary>Enemies spotted.</summary>
    public int Spotted { get; init; }

    /// <summary>Times spotted by an enemy.</summary>
    public int SelfSpotted { get; init; }

    /// <summary>Bases captured.</summary>
    public int BasesCaptured { get; init; }

    /// <summary>Bases destroyed.</summary>
    public int BasesDestroyed { get; init; }

    /// <summary>Bombs disarmed.</summary>
    public int BombDisarms { get; init; }

    /// <summary>Rescue targets saved.</summary>
    public int RescueTargetSaved { get; init; }

    /// <summary>Rescue targets defended.</summary>
    public int RescueTargetDefended { get; init; }

    /// <summary>Times the rescue target was reached first.</summary>
    public int RescueTargetReachedFirst { get; init; }

    /// <summary>Race checkpoints passed.</summary>
    public int RaceCheckpoints { get; init; }

    /// <summary>Times a box was used.</summary>
    public int BoxUses { get; init; }

    /// <summary>Seconds spent inside a box.</summary>
    public int BoxTime { get; init; }

    /// <summary>Seconds played.</summary>
    public int Time { get; init; }

    /// <summary>Points earned from assists.</summary>
    public int PointsAssist { get; init; }

    /// <summary>Points earned from bases.</summary>
    public int PointsBase { get; init; }

    /// <summary>Teammates woken up.</summary>
    public int Wakeups { get; init; }

    /// <summary>Boosts applied.</summary>
    public int Boosts { get; init; }

    /// <summary>Scans performed.</summary>
    public int Scans { get; init; }

    /// <summary>Seconds spent with evasion gear.</summary>
    public int EvasionTime { get; init; }

    /// <summary>Whether the round was aborted.</summary>
    public bool Aborted { get; init; }

    /// <summary>Game mode the round was played in.</summary>
    public int GameMode { get; init; }
}

/// <summary>
/// Owns the lifetime statistics of characters and applies the statistics one
/// host-reported round contributes.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class CharacterStatisticsService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Finds the statistics of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterStatistics?> FindByCharacterIdentifierAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterStatistics
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.CharacterIdentifier == characterIdentifier, cancellationToken);
    }

    /// <summary>Returns the statistics of a character, creating a default row when there is none.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<CharacterStatistics> GetOrCreateAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindByCharacterIdentifierAsync(characterIdentifier, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var defaults = ModeStatisticsCodec.DefaultJson;
        var created = new CharacterStatistics
        {
            CharacterIdentifier = characterIdentifier,
            DeathmatchStatistics = defaults,
            TeamDeathmatchStatistics = defaults,
            RescueStatistics = defaults,
            CaptureStatistics = defaults,
            BaseStatistics = defaults,
            BombStatistics = defaults,
            SneakingStatistics = defaults,
            TeamSneakingStatistics = defaults,
            SdmStatistics = defaults,
            ScapStatistics = defaults,
            RaceStatistics = defaults,
        };

        await using var context = await CreateContextAsync(cancellationToken);
        context.CharacterStatistics.Add(created);
        await context.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>Applies the statistics of one round to a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="round">Statistics reported for the round.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task ApplyRoundStatisticsAsync(
        int characterIdentifier,
        RoundStatistics round,
        CancellationToken cancellationToken = default)
    {
        var statistics = await GetOrCreateAsync(characterIdentifier, cancellationToken);

        await using var context = await CreateContextAsync(cancellationToken);
        var row = await context.CharacterStatistics
            .FirstAsync(item => item.CharacterIdentifier == characterIdentifier, cancellationToken);

        row.Kills += round.Kills;
        row.Deaths += round.Deaths;
        row.Wins += round.Wins;
        row.Score += round.Score;
        row.Rounds += 1;
        row.Stuns += round.Stuns;
        row.StunsReceived += round.StunsReceived;
        row.StunsFriendly += round.StunsFriendly;
        row.HeadshotKills += round.HeadshotKills;
        row.HeadshotDeaths += round.HeadshotDeaths;
        row.HeadshotStuns += round.HeadshotStuns;
        row.HeadshotStunsReceived += round.HeadshotStunsReceived;
        row.LockKills += round.LockKills;
        row.LockDeaths += round.LockDeaths;
        row.LockStuns += round.LockStuns;
        row.LockStunsReceived += round.LockStunsReceived;
        row.ConsecutiveKills = Math.Max(row.ConsecutiveKills, round.ConsecutiveKills);
        row.ConsecutiveDeaths = Math.Max(row.ConsecutiveDeaths, round.ConsecutiveDeaths);
        row.ConsecutiveHeadshots = Math.Max(row.ConsecutiveHeadshots, round.ConsecutiveHeadshots);
        row.Suicides += round.Suicides;
        row.Salutes += round.Salutes;
        row.Radio += round.Radio;
        row.Chat += round.Chat;
        row.CqcGiven += round.CqcGiven;
        row.CqcTaken += round.CqcTaken;
        row.Rolls += round.Rolls;
        row.Melee += round.Melee;
        row.MeleeReceived += round.MeleeReceived;
        row.KnifeKills += round.KnifeKills;
        row.KnifeStuns += round.KnifeStuns;
        row.TeamKills += round.TeamKills;
        row.Spotted += round.Spotted;
        row.SelfSpotted += round.SelfSpotted;
        row.BasesCaptured += round.BasesCaptured;
        row.BasesDestroyed += round.BasesDestroyed;
        row.BombDisarms += round.BombDisarms;
        row.RescueTargetSaved += round.RescueTargetSaved;
        row.RescueTargetDefended += round.RescueTargetDefended;
        row.RescueTargetReachedFirst += round.RescueTargetReachedFirst;
        row.RaceCheckpoints += round.RaceCheckpoints;
        row.BoxUses += round.BoxUses;
        row.BoxTime += round.BoxTime;
        row.Boosts += round.Boosts;
        row.Scans += round.Scans;
        row.EvasionTime += round.EvasionTime;
        row.Wakeups += round.Wakeups;
        row.PointsAssist += round.PointsAssist;
        row.PointsBase += round.PointsBase;
        row.TotalTime += round.Time;

        // The per-mode blob accumulates the same round from its own baseline,
        // which is the row read before this call.
        var mode = ModeStatisticsCodec.ForMode(statistics, round.GameMode);
        mode.Wins += round.Wins;
        mode.Rounds += 1;
        mode.Score += round.Score;
        mode.Time += round.Time;
        mode.Kills += round.Kills;
        mode.Deaths += round.Deaths;
        mode.Stuns += round.Stuns;
        mode.StunsRec += round.StunsReceived;
        mode.HsKills += round.HeadshotKills;
        mode.HsDeaths += round.HeadshotDeaths;
        mode.HsStuns += round.HeadshotStuns;
        mode.HsStunsRec += round.HeadshotStunsReceived;
        mode.LockKills += round.LockKills;
        mode.LockDeaths += round.LockDeaths;
        mode.LockStuns += round.LockStuns;
        mode.LockStunsRec += round.LockStunsReceived;
        ModeStatisticsCodec.AssignBlob(row, round.GameMode, ModeStatisticsCodec.Serialize(mode));

        row.LastUpdated = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Reads the per-mode tally of a character.</summary>
    /// <param name="statistics">Statistics row of the character.</param>
    /// <param name="gameMode">Game mode index.</param>
    public static ModeStatistics GetModeStatistics(CharacterStatistics statistics, int gameMode) =>
        ModeStatisticsCodec.ForMode(statistics, gameMode);
}
