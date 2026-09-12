using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Mgo2Server.Http.Errors;

/// <summary>Envelope every failure is reported with.</summary>
/// <param name="Code">Machine-readable failure code.</param>
/// <param name="Message">Human-readable failure description.</param>
public sealed record FailureResponse(string Code, string Message);

/// <summary>
/// Maps a failure raised by a domain service to the JSON envelope the API
/// reports, so a handler can raise an error without knowing the transport.
/// </summary>
/// <param name="logger">Logger of this handler.</param>
public sealed class ServerExceptionHandler(ILogger<ServerExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            // A body that cannot be read is refused before the handler runs,
            // with the same envelope as a body that fails its field rules.
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(
                new ValidationFailureResponse(
                    false,
                    new ValidationError("ValidationError", badRequest.Message)),
                cancellationToken);
            return true;
        }

        if (exception is ServerException serverException)
        {
            httpContext.Response.StatusCode = serverException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(
                new FailureResponse(serverException.Code, serverException.Message),
                cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled failure");

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new FailureResponse("FATAL_ERROR", "Internal server error"),
            cancellationToken);
        return true;
    }
}
