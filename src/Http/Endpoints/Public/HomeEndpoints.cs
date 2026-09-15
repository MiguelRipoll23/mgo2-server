using Mgo2Server.Http.Options;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The home page endpoint.</summary>
internal static class HomeEndpoints
{
    /// <summary>File name of the home page inside the static directory.</summary>
    private const string HomeFileName = "index.html";

    /// <summary>Maps the home page endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapHomeEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetHomePageAsync);
    }

    private static async Task<IResult> GetHomePageAsync(
        IOptions<HttpApiOptions> options,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(options.Value.StaticDirectory, HomeFileName);
        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        return Results.File(await File.ReadAllBytesAsync(path, cancellationToken), "text/html; charset=utf-8");
    }
}