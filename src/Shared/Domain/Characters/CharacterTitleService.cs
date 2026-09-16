using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Characters;

/// <summary>
/// Awards titles and keeps the worn one on the character record.
/// <para>
/// The requirement table is <see cref="AnimalRankService"/>: it derives the best
/// rank a character's lifetime statistics qualify for, and this service turns that
/// derivation into a latch. The client does not choose a title — it has no command
/// for it — so the worn title is simply the best one unlocked, which is the same
/// rule the reference server applies and the reason the value is stored rather
/// than recomputed per read.
/// </para>
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="statisticsService">Service that owns the lifetime statistics.</param>
/// <param name="characterService">Service that owns the character records.</param>
public sealed class CharacterTitleService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    CharacterStatisticsService statisticsService,
    CharacterService characterService)
    : DomainService(contextFactory)
{
    /// <summary>
    /// Number of titles the client's badge table carries. Rank identifiers are
    /// one-based positions in that table, so only ranks up to this one have a bit
    /// in the collection the client renders.
    /// </summary>
    public const int ClientTitleCount = 22;

    /// <summary>
    /// Packs latched ranks into the collection the personal-stats screen renders:
    /// bit zero is the best rank. Ranks past the client's table are skipped rather
    /// than shifted — its popcount loop walks the table once per title, so a bit
    /// above the last one makes it read past the end.
    /// </summary>
    /// <param name="ranks">Ranks the character has latched, in any order.</param>
    /// <returns>The mask the client renders, zero when nothing is latched.</returns>
    public static int BuildTitleMask(IEnumerable<int> ranks)
    {
        var mask = 0;
        foreach (var rank in ranks)
        {
            if (rank is >= 1 and <= ClientTitleCount)
            {
                mask |= 1 << (rank - 1);
            }
        }

        return mask;
    }

    /// <summary>
    /// Unlocks every title a character now qualifies for and refreshes the worn one.
    /// Idempotent, so it is safe to call after every round and on every lobby entry.
    /// </summary>
    /// <param name="characterIdentifier">Character to evaluate.</param>
    /// <param name="daysSinceLastLogin">Days since the character last logged in, which one rank family measures.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The ranks unlocked by this call, which is empty on the common no-change path.</returns>
    public async Task<List<int>> EvaluateAsync(
        int characterIdentifier,
        int daysSinceLastLogin = 0,
        CancellationToken cancellationToken = default)
    {
        var statistics = await statisticsService.FindByCharacterIdentifierAsync(
            characterIdentifier,
            cancellationToken);
        if (statistics is null)
        {
            return [];
        }

        var qualified = AnimalRankService.CalculateRank(statistics, daysSinceLastLogin);
        if (qualified <= 0)
        {
            return [];
        }

        var unlocked = await UnlockAsync(characterIdentifier, qualified, cancellationToken);
        var worn = await WornRankAsync(characterIdentifier, cancellationToken);

        if (worn > 0)
        {
            await characterService.SetRankAsync(characterIdentifier, worn, cancellationToken);
        }

        return unlocked ? [qualified] : [];
    }

    /// <summary>Returns the ranks a character has latched, best first.</summary>
    /// <param name="characterIdentifier">Character to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<int>> FindUnlockedRanksAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.CharacterTitles
            .AsNoTracking()
            .Where(title => title.CharacterIdentifier == characterIdentifier)
            .OrderBy(title => title.Rank)
            .Select(title => title.Rank)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The title collection of a character, as the client's bit mask.</summary>
    /// <param name="characterIdentifier">Character to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> FindTitleMaskAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default) =>
        BuildTitleMask(await FindUnlockedRanksAsync(characterIdentifier, cancellationToken));

    /// <summary>The best rank a character has latched, or zero when it has none.</summary>
    /// <param name="characterIdentifier">Character to read.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<int> WornRankAsync(int characterIdentifier, CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var ranks = await context.CharacterTitles
            .AsNoTracking()
            .Where(title => title.CharacterIdentifier == characterIdentifier)
            .Select(title => title.Rank)
            .ToListAsync(cancellationToken);

        return ranks.Count == 0 ? 0 : ranks.Min();
    }

    /// <summary>
    /// Inserts a title if it is not latched already. Returns whether this call is
    /// the one that unlocked it, which is what makes the log line announce each
    /// title once rather than on every round.
    /// </summary>
    /// <param name="characterIdentifier">Character that earned it.</param>
    /// <param name="rank">Rank to latch.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<bool> UnlockAsync(int characterIdentifier, int rank, CancellationToken cancellationToken)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var latched = await context.CharacterTitles
            .AnyAsync(
                title => title.CharacterIdentifier == characterIdentifier && title.Rank == rank,
                cancellationToken);

        if (latched)
        {
            return false;
        }

        context.CharacterTitles.Add(new CharacterTitle
        {
            CharacterIdentifier = characterIdentifier,
            Rank = rank,
            UnlockedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
        });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
