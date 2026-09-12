using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Http.Endpoints;

/// <summary>The news endpoints of the authenticated API surface.</summary>
internal static class NewsEndpoints
{
    /// <summary>Maps the news endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapNewsEndpoints(this RouteGroupBuilder group)
    {
        var news = group.MapGroup("/news")
            .WithTags("News")
            .RequireAuthorization();

        news.MapGet("/", ListNewsAsync)
            .WithSummary("List news entries")
            .WithDescription("Returns a list of all news entries ordered by most recent first");

        news.MapGet("/{id:int}", GetNewsAsync)
            .WithSummary("Get news entry")
            .WithDescription("Returns a single news entry by its numeric identifier");

        news.MapPost("/", CreateNewsAsync)
            .WithSummary("Create news entry")
            .WithDescription("Creates a new news entry and returns the created resource");

        news.MapPatch("/{id:int}", PatchNewsAsync)
            .WithSummary("Update news entry")
            .WithDescription("Partially updates an existing news entry by its numeric identifier");

        news.MapDelete("/{id:int}", DeleteNewsAsync)
            .WithSummary("Delete news entry")
            .WithDescription("Permanently removes a news entry by its numeric identifier");
    }

    private static async Task<IResult> ListNewsAsync(NewsService newsService, CancellationToken cancellationToken)
    {
        var articles = await newsService.FindAllAsync(cancellationToken);
        return Results.Ok(articles.Select(article => article.ToContract()));
    }

    private static async Task<IResult> GetNewsAsync(
        NewsService newsService,
        int id,
        CancellationToken cancellationToken)
    {
        var article = await newsService.FindByIdAsync(id, cancellationToken);
        return Results.Ok(article.ToContract());
    }

    private static async Task<IResult> CreateNewsAsync(
        NewsService newsService,
        NewsRequest request,
        CancellationToken cancellationToken)
    {
        var article = await newsService.CreateAsync(
            new NewsArticle
            {
                Important = request.Important,
                Time = request.Time,
                Topic = request.Topic,
                Message = request.Message,
            },
            cancellationToken);

        return Results.Json(article.ToContract(), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> PatchNewsAsync(
        NewsService newsService,
        int id,
        NewsPatchRequest request,
        CancellationToken cancellationToken)
    {
        var article = await newsService.UpdateAsync(
            id,
            item =>
            {
                item.Important = request.Important ?? item.Important;
                item.Time = request.Time ?? item.Time;
                item.Topic = request.Topic ?? item.Topic;
                item.Message = request.Message ?? item.Message;
            },
            cancellationToken);

        return Results.Ok(article.ToContract());
    }

    private static async Task<IResult> DeleteNewsAsync(
        NewsService newsService,
        int id,
        CancellationToken cancellationToken)
    {
        await newsService.RemoveAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
