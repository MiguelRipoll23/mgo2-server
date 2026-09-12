using Mgo2Server.Http.Services;

namespace Mgo2Server.Http.Endpoints;

/// <summary>The patch file download endpoints.</summary>
internal static class FilesEndpoints
{
    /// <summary>Maps the file endpoints.</summary>
    /// <param name="app">Application the endpoint is added to.</param>
    public static void MapFilesEndpoints(this WebApplication app)
    {
        // The patch files are mirrored straight from the launcher, so they have
        // no request or response contract of their own and stay out of the API
        // reference; documenting them would only add an empty group.
        app.MapMethods("/files/{*path}", ["GET", "HEAD"], GetFileAsync)
            .ExcludeFromDescription();
    }

    private static Task<IResult> GetFileAsync(
        FileService fileService,
        HttpContext context,
        string path,
        CancellationToken cancellationToken) =>
        fileService.GetFileAsync(
            path,
            HttpMethods.IsHead(context.Request.Method),
            context.Request.Headers.Range.Count > 0 ? context.Request.Headers.Range.ToString() : null,
            cancellationToken);
}
