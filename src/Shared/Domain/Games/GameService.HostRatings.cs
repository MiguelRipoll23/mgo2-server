using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The rating half of the game service: the votes a host receives and the
/// lifetime aggregate the room list shows.
/// </summary>
public sealed partial class GameService
{
    /// <summary>
    /// Stores a host-rating vote. A host cannot vote for itself, and the
    /// one-vote-per-player-per-room constraint absorbs a retry silently.
    /// </summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="hostCharacterIdentifier">Identifier of the hosting character.</param>
    /// <param name="voterCharacterIdentifier">Identifier of the voting character.</param>
    /// <param name="rating">Star rating awarded.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><c>true</c> when the vote was stored, <c>false</c> when it was refused.</returns>
    public async Task<bool> RecordHostVoteAsync(
        int gameIdentifier,
        int hostCharacterIdentifier,
        int voterCharacterIdentifier,
        int rating,
        CancellationToken cancellationToken = default)
    {
        if (hostCharacterIdentifier == voterCharacterIdentifier)
        {
            return false;
        }

        if (rating is < MinimumHostRating or > MaximumHostRating)
        {
            return false;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var inserted = await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO host_reviews (game_id, host_character_id, voter_character_id, rating)
             VALUES ({gameIdentifier}, {hostCharacterIdentifier}, {voterCharacterIdentifier}, {(short)rating})
             ON CONFLICT (game_id, voter_character_id) DO NOTHING
             """,
            cancellationToken);

        return inserted > 0;
    }

    /// <summary>Returns whether a player has already rated the host of a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="voterCharacterIdentifier">Identifier of the voting character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> HasRatedHostOfAsync(
        int gameIdentifier,
        int voterCharacterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.HostReviews
            .AsNoTracking()
            .AnyAsync(
                review => review.GameIdentifier == gameIdentifier &&
                    review.VoterCharacterIdentifier == voterCharacterIdentifier,
                cancellationToken);
    }

    /// <summary>Returns the lifetime rating aggregate of each host.</summary>
    /// <param name="hostCharacterIdentifiers">Identifiers of the hosts to summarize.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Dictionary<int, HostRatingSummary>> GetHostRatingSummariesAsync(
        IReadOnlyCollection<int> hostCharacterIdentifiers,
        CancellationToken cancellationToken = default)
    {
        var summaries = new Dictionary<int, HostRatingSummary>();
        if (hostCharacterIdentifiers.Count == 0)
        {
            return summaries;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        var grouped = await context.HostReviews
            .AsNoTracking()
            .Where(review => hostCharacterIdentifiers.Contains(review.HostCharacterIdentifier))
            .GroupBy(review => review.HostCharacterIdentifier)
            .Select(group => new
            {
                HostIdentifier = group.Key,
                RatingSum = group.Sum(review => (int)review.Rating),
                Votes = group.Count(),
            })
            .ToListAsync(cancellationToken);

        foreach (var row in grouped)
        {
            summaries[row.HostIdentifier] = new HostRatingSummary(row.RatingSum, row.Votes);
        }

        return summaries;
    }
}
