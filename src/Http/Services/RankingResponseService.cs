using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Rankings;
using Mgo2Server.Shared.Utils;
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
/// The request is logged at information level with the window it was answered
/// with, because the client reports every failure with the same code and no
/// detail: a board that came back empty and a board that was never asked about
/// look identical on screen, and this line is what tells them apart. At debug
/// level the posted parameters are logged and the reply is dumped in hex both
/// before and after the scramble, so the one transform the body carries can be
/// checked against the key without reversing it by hand.
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
        logger.LogDebug(
            "Player ranking request term {Term} rule {Rule} skey {SortKey} from {From} records {Records} " +
            "pid {Subject}.",
            form.Term,
            form.Rule,
            form.SortKey,
            form.From,
            form.Records,
            form.Subject);

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

        return EncodeReply(page, "Player");
    }

    /// <summary>Builds the reply of the clan endpoint.</summary>
    /// <param name="form">Request parameters.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<byte[]> GetClanRankingAsync(
        RankingForm form,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Clan ranking request term {Term} skey {SortKey} from {From} records {Records} cid {Subject}.",
            form.Term,
            form.SortKey,
            form.From,
            form.Records,
            form.Subject);

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

        return EncodeReply(page, "Clan");
    }

    /// <summary>
    /// Serialises a board window, logs both stages of the reply in hex at debug
    /// level and returns the scrambled bytes the client receives.
    /// </summary>
    /// <param name="page">Window to serialise.</param>
    /// <param name="board">Board the window belongs to, for the log lines.</param>
    private byte[] EncodeReply(RankingPage page, string board)
    {
        var body = RankingBodyUtils.EncodeClear(page);

        logger.LogDebug(
            "{Board} ranking reply before the scramble, {Length} bytes: {Bytes}",
            board,
            body.Length,
            TrafficLogger.FormatHex(body));

        RankingScrambleUtils.Apply(body);

        logger.LogDebug(
            "{Board} ranking reply after the scramble, {Length} bytes: {Bytes}",
            board,
            body.Length,
            TrafficLogger.FormatHex(body));

        return body;
    }
}
