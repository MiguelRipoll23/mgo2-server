using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Rankings;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Services;

/// <summary>
/// Builds the binary replies of the two Rankings endpoints.
/// <para>
/// These are not lobby commands: the screen posts six form fields and parses a
/// scrambled binary body, so the answer is served as an opaque blob rather than
/// JSON. The body itself is assembled by <see cref="RankingBodyUtils"/>.
/// </para>
/// <para>
/// Each request is logged with the window it was answered with, because the client
/// reports every failure with the same code and no detail: a board that came back
/// empty and a board that was never asked about look identical on screen, and this
/// line is what tells them apart.
/// </para>
/// </summary>
/// <param name="rankingService">Service that sources and ranks the boards.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class RankingResponseService(
    RankingService rankingService,
    ILogger<RankingResponseService> logger)
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

        logger.LogInformation(
            "Player ranking skey {SortKey} rule {Rule} term {Term} from {From} records {Records} " +
            "pid {Subject} -> {Entries} of {Total}.",
            form.SortKey,
            form.Rule,
            form.Term,
            form.From,
            form.Records,
            form.Subject,
            page.Entries.Count,
            page.Total);

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

        logger.LogInformation(
            "Clan ranking skey {SortKey} term {Term} from {From} records {Records} " +
            "cid {Subject} -> {Entries} of {Total}.",
            form.SortKey,
            form.Term,
            form.From,
            form.Records,
            form.Subject,
            page.Entries.Count,
            page.Total);

        return RankingBodyUtils.Encode(page);
    }
}
