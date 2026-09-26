using Mgo2Server.Http.Options;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The public entry pages of the event testing tools, and the script they run.
/// They are served without a token because the tools themselves are public: the
/// page asks a lobby for teams that exist only in that lobby's memory, and a
/// page that asked for a bearer token would only move the token from the URL
/// bar into a form.
/// </summary>
internal static class EventToolEndpoints
{
    /// <summary>File the tool page is served from.</summary>
    private const string ToolFileName = "event-tools.html";

    /// <summary>File the page's script is served from.</summary>
    private const string ToolScriptFileName = "event-tools.js";

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
        group.MapGet("/event-tools.js", GetToolScriptAsync);
    }

    private static Task<IResult> GetToolPageAsync(
        IOptions<HttpApiOptions> options,
        CancellationToken cancellationToken) =>
        GetToolFileAsync(options, ToolFileName, "text/html; charset=utf-8", cancellationToken);

    private static Task<IResult> GetToolScriptAsync(
        IOptions<HttpApiOptions> options,
        CancellationToken cancellationToken) =>
        GetToolFileAsync(options, ToolScriptFileName, "text/javascript; charset=utf-8", cancellationToken);

    /// <summary>Reads one file of the tools out of the static directory.</summary>
    /// <param name="options">Options that name the static directory.</param>
    /// <param name="fileName">File to serve.</param>
    /// <param name="contentType">Content type the browser is given.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> GetToolFileAsync(
        IOptions<HttpApiOptions> options,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(options.Value.StaticDirectory, fileName);
        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        return Results.File(await File.ReadAllBytesAsync(path, cancellationToken), contentType);
    }
}
