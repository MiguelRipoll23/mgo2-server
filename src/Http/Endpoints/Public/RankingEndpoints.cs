using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Services;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The Rankings screens. These are not lobby commands: the screen posts to the two
/// endpoints and parses a binary reply, so the answer is served as an
/// opaque body rather than JSON.
/// </summary>
internal static class RankingEndpoints
{
    /// <summary>Maps the ranking endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapRankingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/jp/mgo2/rank/mgogetrank.html", GetPlayerRankingAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Player rankings")
            .WithDescription("Window of a player board, as the Rankings screens read it");

        group.MapPost("/jp/mgo2/rank/mgogetrank_clan.html", GetClanRankingAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Clan rankings")
            .WithDescription("Window of a clan board, as the clan Rankings screen reads it");
    }

    private static async Task<IResult> GetPlayerRankingAsync(
        HttpContext context,
        RankingResponseService rankingResponseService,
        CancellationToken cancellationToken)
    {
        var form = RankingForm.Read(
            await RequestBodyValidation.ReadFormAsync(context, cancellationToken),
            "pid");

        var body = await rankingResponseService.GetPlayerRankingAsync(form, cancellationToken);
        return Results.Bytes(body, "application/octet-stream");
    }

    private static async Task<IResult> GetClanRankingAsync(
        HttpContext context,
        RankingResponseService rankingResponseService,
        CancellationToken cancellationToken)
    {
        var form = RankingForm.Read(
            await RequestBodyValidation.ReadFormAsync(context, cancellationToken),
            "cid");

        var body = await rankingResponseService.GetClanRankingAsync(form, cancellationToken);
        return Results.Bytes(body, "application/octet-stream");
    }
}
