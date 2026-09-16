using Mgo2Server.Http.Discord;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The endpoint Discord delivers interactions to. It is public because Discord
/// authenticates itself with the signature of the request rather than with a
/// token of the API.
/// </summary>
internal static class DiscordEndpoints
{
    /// <summary>Header that carries the signature of the request.</summary>
    private const string SignatureHeader = "X-Signature-Ed25519";

    /// <summary>Header that carries the timestamp the signature covers.</summary>
    private const string TimestampHeader = "X-Signature-Timestamp";

    /// <summary>Largest interaction body that is read, which is far above a command.</summary>
    private const int MaximumBodyLength = 64 * 1024;

    /// <summary>Maps the Discord endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapDiscordEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/discord/interactions", HandleInteractionAsync)
            .WithTags("Discord")
            .WithSummary("Discord interactions")
            .WithDescription(
                "Receives the interactions Discord delivers, verifies their signature and answers them. " +
                "The flash command relays its message to every connected game lobby.");
    }

    /// <summary>Reads one interaction and answers it.</summary>
    /// <param name="context">Request being handled.</param>
    /// <param name="interactionService">Service that answers interactions.</param>
    private static async Task<IResult> HandleInteractionAsync(
        HttpContext context,
        DiscordInteractionService interactionService)
    {
        if (context.Request.ContentLength is > MaximumBodyLength)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = await ReadBodyAsync(context.Request, context.RequestAborted);
        var result = interactionService.Handle(
            body,
            context.Request.Headers[SignatureHeader].ToString(),
            context.Request.Headers[TimestampHeader].ToString());

        return result.Response is null
            ? Results.StatusCode(result.StatusCode)
            : Results.Json(result.Response, statusCode: result.StatusCode);
    }

    /// <summary>
    /// Reads the raw bytes of the body. The signature covers the request as it
    /// arrived, so nothing may re-encode or reformat it before it is verified.
    /// </summary>
    /// <param name="request">Request whose body is read.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    private static async Task<byte[]> ReadBodyAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }
}
