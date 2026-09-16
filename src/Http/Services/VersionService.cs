namespace Mgo2Server.Http.Services;

/// <summary>
/// Answers the client's version check. The reply is the single byte the client
/// reads as "up to date".
/// </summary>
public sealed class VersionService
{
    /// <summary>Writes the version check reply.</summary>
    /// <param name="context">Response the reply is written to.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public static async Task WriteCheckVersionResponseAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html";
        context.Response.ContentLength = 1;
        await context.Response.Body.WriteAsync(new byte[] { 0x00 }, cancellationToken);
    }
}
