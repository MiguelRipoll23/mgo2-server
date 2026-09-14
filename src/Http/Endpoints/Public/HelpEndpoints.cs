using Mgo2Server.Http.Services;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The game client help/tip file endpoints.</summary>
internal static class HelpEndpoints
{
    /// <summary>Maps the help file endpoints.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapHelpEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/jp/mgo2/help/{*path}", GetHelpAsync)
            .WithTags("Game")
            .WithSummary("Help file")
            .WithDescription("Returns a help/tip text file requested by the game client");
    }

    private static async Task<IResult> GetHelpAsync(
        HttpRequest request,
        HelpService helpService,
        string path,
        CancellationToken cancellationToken)
    {
        var body = await helpService.GetHelpTextAsync(path, request, cancellationToken);
        if (string.IsNullOrEmpty(body))
        {
            return Results.NotFound();
        }

        return Results.Text(body, "text/plain; charset=utf-8");
    }
}
