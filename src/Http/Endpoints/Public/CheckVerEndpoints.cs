using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Services;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The version check endpoint.</summary>
internal static class CheckVerEndpoints
{
    /// <summary>Maps the version check endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapCheckVerEndpoints(this RouteGroupBuilder group)
    {
        // The client posts to /jp/mgo2//patch/checkver.html and
        // /jp/mgo2//patch//checkver.html; the path normaliser collapses the
        // repeated slashes before routing, so one route serves both.
        group.MapPost("/jp/mgo2/patch/checkver.html", CheckVersionAsync)
            .DisableAntiforgery()
            .WithTags("Game")
            .WithSummary("Check version")
            .WithDescription("Returns the result of the version check");
    }

    private static async Task CheckVersionAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var form = CheckVersionForm.From(await RequestBodyValidation.ReadFormAsync(context, cancellationToken));
        var issues = RequestBodyValidation.Validate(form);
        if (issues.Count > 0)
        {
            await RequestBodyValidation.Reject([.. issues]).ExecuteAsync(context);
            return;
        }

        await VersionService.WriteCheckVersionResponseAsync(context, cancellationToken);
    }
}
