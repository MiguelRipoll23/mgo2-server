using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Rankings;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Builds the binary replies of the two Rankings endpoints.
/// <para>
/// These are not lobby commands: the screen posts six form fields and parses a
/// scrambled binary body, so the answer is served as an opaque blob rather than
/// JSON. The body itself is assembled by <see cref="RankingBodyUtils"/>.
/// </para>
/// </summary>
/// <param name="rankingService">Service that sources and ranks the boards.</param>
public sealed class RankingResponseService(RankingService rankingService)
{
    /// <summary>Builds the reply of the player endpoint.</summary>
    /// <param name="form">Request parameters.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<byte[]> GetPlayerRankingAsync(
        RankingForm form,
        CancellationToken cancellationToken = default)
    {
        var page = await rankingService.PlayersAsync(
            form.Term,
            form.Rule,
            form.SortKey,
            form.From,
            form.Records,
            form.Subject,
            cancellationToken);

        return RankingBodyUtils.Encode(page);
    }

    /// <summary>Builds the reply of the clan endpoint.</summary>
    /// <param name="form">Request parameters.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<byte[]> GetClanRankingAsync(
        RankingForm form,
        CancellationToken cancellationToken = default)
    {
        var page = await rankingService.ClansAsync(
            form.Term,
            form.SortKey,
            form.From,
            form.Records,
            form.Subject,
            cancellationToken);

        return RankingBodyUtils.Encode(page);
    }
}
