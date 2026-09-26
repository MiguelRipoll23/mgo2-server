using Mgo2Server.Http.Options;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The public entry pages of the event testing tools. They are served without a
/// token because the page itself carries no data: it asks the caller for a
/// bearer token and sends it on the authenticated fake-event endpoints, so the
/// token never has to sit in a query string or a bookmark.
/// </summary>
internal static class EventToolEndpoints
{
    /// <summary>File the tool pages are served from.</summary>
    private const string ToolFileName = "event-tools.html";

    /// <summary>Maps the event tool page endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    /// <remarks>
    /// Both routes answer with one page: it reads the mode from the path it was
    /// requested at, so the tournament and survival tools are the same code
    /// looking at a different lobby rather than two copies that drift apart.
    /// </remarks>
    public static void MapEventToolEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tournament", GetToolPageAsync);
        group.MapGet("/survival", GetToolPageAsync);
    }

    private static async Task<IResult> GetToolPageAsync(
        IOptions<HttpApiOptions> options,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(options.Value.StaticDirectory, ToolFileName);
        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        return Results.File(await File.ReadAllBytesAsync(path, cancellationToken), "text/html; charset=utf-8");
    }
}
