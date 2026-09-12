namespace Mgo2Server.Http.Services;

/// <summary>
/// Answers the client's version check. The reply is the single byte the client
/// reads as "up to date", served with the header set the original file server
/// sent, which the client's patcher inspects.
/// </summary>
public sealed class VersionService
{
    /// <summary>Response headers of the mirrored file server.</summary>
    private static readonly (string Name, string Value)[] Headers =
    [
        ("Server", "nginx/1.18.0"),
        ("Date", "Tue, 17 Mar 2026 18:12:52 GMT"),
        ("Last-Modified", "Mon, 21 Feb 2022 06:54:43 GMT"),
        ("Connection", "keep-alive"),
        ("ETag", "\"62133733-1\""),
        ("Accept-Ranges", "bytes"),
    ];

    /// <summary>Writes the version check reply.</summary>
    /// <param name="context">Response the reply is written to.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public static async Task WriteCheckVersionResponseAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html";

        foreach (var (name, value) in Headers)
        {
            context.Response.Headers[name] = value;
        }

        context.Response.ContentLength = 1;
        await context.Response.Body.WriteAsync(new byte[] { 0x00 }, cancellationToken);
    }
}
