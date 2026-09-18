using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Rankings;

/// <summary>
/// The boards behind the Rankings screens, derived at query time from data that is
/// already stored.
/// <para>
/// There is no ranking table and no precomputed board: each board is a projection
/// of the lifetime statistics, the round reports or the host votes, exactly as the
/// reference server reads them. A board that nothing can source returns an empty
/// list rather than a column of zeroes, because an empty board is a well-formed
/// answer the client renders as an unselectable row, while zeroes would look
/// identical on screen while claiming something was measured.
/// </para>
/// <para>
/// Boards with a time dimension answer the MONTH half of the client's toggle from
/// the timestamped rows (round reports, host votes); the boards whose only source
/// is a lifetime aggregate are lifetime regardless of the toggle.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class RankingBoardService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Ratings are carried as 8.8 fixed point, matching the client's gauge.</summary>
    private const int FixedPointScale = 256;

    /// <summary>A player board: score by mode, activeness, grade points or host rating.</summary>
    /// <param name="key">Board selector of the request.</param>
    /// <param name="rule">Game mode of the request, used by the score board only.</param>
    /// <param name="currentMonth">Whether the periodic half of the toggle was asked for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<RankingBoardRow>> PlayerBoardAsync(
        int key,
        int rule,
        bool currentMonth,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        return key switch
        {
            // A mode with no statistics blob would otherwise be answered from the
            // deathmatch blob, which would mislabel one board as another.
            RankingKeys.Score when rule >= 0 && rule <= RankingKeys.MaximumGameMode =>
                await PlayerScoreBoardAsync(context, rule, cancellationToken),
            RankingKeys.Activeness => await PlayerActivenessBoardAsync(context, currentMonth, cancellationToken),
            RankingKeys.GradePoint => await PlayerGradePointBoardAsync(context, cancellationToken),
            RankingKeys.HostRating => await HostRatingBoardAsync(context, currentMonth, cancellationToken),
            RankingKeys.InstructorRating =>
                await InstructorRatingBoardAsync(context, currentMonth, cancellationToken),
            _ => [],
        };
    }

    /// <summary>A clan board: clan score, activeness or grade points.</summary>
    /// <param name="key">Board selector of the request.</param>
    /// <param name="currentMonth">Whether the periodic half of the toggle was asked for.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<RankingBoardRow>> ClanBoardAsync(
        int key,
        bool currentMonth,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        return key switch
        {
            RankingKeys.ClanScore => await ClanScoreBoardAsync(context, cancellationToken),
            RankingKeys.Activeness => await ClanActivenessBoardAsync(context, currentMonth, cancellationToken),
            RankingKeys.GradePoint => await ClanGradePointBoardAsync(context, cancellationToken),
            _ => [],
        };
    }

    /// <summary>First instant of the current calendar month, in the units of a timestamp without time zone.</summary>
    public static DateTime CurrentMonthStart() =>
        new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>First instant of the current calendar month, in the units of a timestamp with time zone.</summary>
    private static DateTimeOffset CurrentMonthStartOffset() =>
        new(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Score, split by game mode. The per-mode score lives in the statistics blob of
    /// that mode, which is read as a single column and deserialized in memory; the
    /// mode itself selects the blob.
    /// </summary>
    private static async Task<List<RankingBoardRow>> PlayerScoreBoardAsync(
        Mgo2DatabaseContext context,
        int rule,
        CancellationToken cancellationToken)
    {
        var property = ModeStatisticsCodec.BlobNameForMode(rule);

        var rows = await context.CharacterStatistics
            .AsNoTracking()
            .Join(
                context.Characters.AsNoTracking().Where(character => character.Active),
                statistics => statistics.CharacterIdentifier,
                character => character.Identifier,
                (statistics, character) => new
                {
                    character.Identifier,
                    character.Name,
                    Blob = EF.Property<string?>(statistics, property),
                })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new RankingBoardRow(
                row.Identifier,
                row.Name,
                ModeStatisticsCodec.Deserialize(row.Blob).Score)),
        ];
    }

    /// <summary>Grade points, the character's accumulated experience.</summary>
    private static async Task<List<RankingBoardRow>> PlayerGradePointBoardAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken)
    {
        var rows = await context.Characters
            .AsNoTracking()
            .Where(character => character.Active)
            .Select(character => new { character.Identifier, character.Name, character.Experience })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.Experience))];
    }

    /// <summary>Time played: the lifetime total, or the seconds reported this month.</summary>
    private static async Task<List<RankingBoardRow>> PlayerActivenessBoardAsync(
        Mgo2DatabaseContext context,
        bool currentMonth,
        CancellationToken cancellationToken)
    {
        if (!currentMonth)
        {
            var lifetime = await context.CharacterStatistics
                .AsNoTracking()
                .Join(
                    context.Characters.AsNoTracking().Where(character => character.Active),
                    statistics => statistics.CharacterIdentifier,
                    character => character.Identifier,
                    (statistics, character) => new { character.Identifier, character.Name, statistics.TotalTime })
                .ToListAsync(cancellationToken);

            return [.. lifetime.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.TotalTime))];
        }

        var since = CurrentMonthStart();
        var monthly = await (
            from report in context.RoundReports.AsNoTracking()
            where report.CreatedAt >= since
            group report by report.TargetCharacterIdentifier
            into grouped
            join character in context.Characters.AsNoTracking().Where(character => character.Active)
                on grouped.Key equals character.Identifier
            select new
            {
                character.Identifier,
                character.Name,
                Seconds = grouped.Sum(report => (long)report.Seconds),
            })
            .ToListAsync(cancellationToken);

        return [.. monthly.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.Seconds))];
    }

    /// <summary>Host rating, an average star rating carried as 8.8 fixed point.</summary>
    private static async Task<List<RankingBoardRow>> HostRatingBoardAsync(
        Mgo2DatabaseContext context,
        bool currentMonth,
        CancellationToken cancellationToken)
    {
        var reviews = context.HostReviews.AsNoTracking().AsQueryable();
        if (currentMonth)
        {
            var since = CurrentMonthStartOffset();
            reviews = reviews.Where(review => review.ReviewedAt >= since);
        }

        var rows = await (
            from review in reviews
            join character in context.Characters.AsNoTracking().Where(character => character.Active)
                on review.HostCharacterIdentifier equals character.Identifier
            group review by new { character.Identifier, character.Name }
            into grouped
            select new
            {
                grouped.Key.Identifier,
                grouped.Key.Name,
                Average = grouped.Average(review => (double)review.Rating),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new RankingBoardRow(
                row.Identifier,
                row.Name,
                (long)Math.Round(row.Average * FixedPointScale))),
        ];
    }

    /// <summary>
    /// Instructor rating: the average of the star ratings a character's students gave
    /// them, carried as 8.8 fixed point like the host board beside it.
    /// <para>
    /// Sourced from the reviews rather than from the saved relationships, which hold
    /// one current-state row per student and would therefore lose every review a
    /// student later replaced by re-graduating.
    /// </para>
    /// </summary>
    private static async Task<List<RankingBoardRow>> InstructorRatingBoardAsync(
        Mgo2DatabaseContext context,
        bool currentMonth,
        CancellationToken cancellationToken)
    {
        var reviews = context.InstructorReviews.AsNoTracking().AsQueryable();
        if (currentMonth)
        {
            var since = CurrentMonthStartOffset();
            reviews = reviews.Where(review => review.ReviewedAt >= since);
        }

        var rows = await (
            from review in reviews
            join character in context.Characters.AsNoTracking().Where(character => character.Active)
                on review.InstructorCharacterIdentifier equals character.Identifier
            group review by new { character.Identifier, character.Name }
            into grouped
            select new
            {
                grouped.Key.Identifier,
                grouped.Key.Name,
                Average = grouped.Average(review => (double)review.Rating),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new RankingBoardRow(
                row.Identifier,
                row.Name,
                (long)Math.Round(row.Average * FixedPointScale))),
        ];
    }

    /// <summary>A clan's combined score, summed over its members.</summary>
    private static async Task<List<RankingBoardRow>> ClanScoreBoardAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from member in context.ClanMembers.AsNoTracking()
            join character in context.Characters.AsNoTracking().Where(character => character.Active)
                on member.CharacterIdentifier equals character.Identifier
            join statistics in context.CharacterStatistics.AsNoTracking()
                on character.Identifier equals statistics.CharacterIdentifier
            join clan in context.Clans.AsNoTracking() on member.ClanIdentifier equals clan.Identifier
            group statistics by new { clan.Identifier, clan.Name }
            into grouped
            select new
            {
                grouped.Key.Identifier,
                grouped.Key.Name,
                Value = grouped.Sum(statistics => (long)statistics.Score),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.Value))];
    }

    /// <summary>A clan's combined grade points, summed over its members.</summary>
    private static async Task<List<RankingBoardRow>> ClanGradePointBoardAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from member in context.ClanMembers.AsNoTracking()
            join character in context.Characters.AsNoTracking().Where(character => character.Active)
                on member.CharacterIdentifier equals character.Identifier
            join clan in context.Clans.AsNoTracking() on member.ClanIdentifier equals clan.Identifier
            group character by new { clan.Identifier, clan.Name }
            into grouped
            select new
            {
                grouped.Key.Identifier,
                grouped.Key.Name,
                Value = grouped.Sum(character => (long)character.Experience),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.Value))];
    }

    /// <summary>Time played by a clan's members, lifetime or this month.</summary>
    private static async Task<List<RankingBoardRow>> ClanActivenessBoardAsync(
        Mgo2DatabaseContext context,
        bool currentMonth,
        CancellationToken cancellationToken)
    {
        if (!currentMonth)
        {
            var lifetime = await (
                from member in context.ClanMembers.AsNoTracking()
                join character in context.Characters.AsNoTracking().Where(character => character.Active)
                    on member.CharacterIdentifier equals character.Identifier
                join statistics in context.CharacterStatistics.AsNoTracking()
                    on character.Identifier equals statistics.CharacterIdentifier
                join clan in context.Clans.AsNoTracking() on member.ClanIdentifier equals clan.Identifier
                group statistics by new { clan.Identifier, clan.Name }
                into grouped
                select new
                {
                    grouped.Key.Identifier,
                    grouped.Key.Name,
                    Value = grouped.Sum(statistics => (long)statistics.TotalTime),
                })
                .ToListAsync(cancellationToken);

            return [.. lifetime.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.Value))];
        }

        var since = CurrentMonthStart();
        var monthly = await (
            from report in context.RoundReports.AsNoTracking()
            where report.CreatedAt >= since
            join member in context.ClanMembers.AsNoTracking()
                on report.TargetCharacterIdentifier equals member.CharacterIdentifier
            join clan in context.Clans.AsNoTracking() on member.ClanIdentifier equals clan.Identifier
            group report by new { clan.Identifier, clan.Name }
            into grouped
            select new
            {
                grouped.Key.Identifier,
                grouped.Key.Name,
                Value = grouped.Sum(report => (long)report.Seconds),
            })
            .ToListAsync(cancellationToken);

        return [.. monthly.Select(row => new RankingBoardRow(row.Identifier, row.Name, row.Value))];
    }
}
