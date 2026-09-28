using Mgo2Server.Http.Contracts;

namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>
/// The liveness endpoint the companion site asks before it offers anything.
/// It reads no store and writes nothing, so it answers whether the API process
/// is up and nothing more — a page that wants to say "the server is reachable"
/// should not be able to say it on the strength of a query this route made.
/// </summary>
internal static class HealthEndpoints
{
    /// <summary>
    /// CORS policy the health route is answered under. It allows every origin
    /// because the page that asks is deployed somewhere this service cannot know
    /// in advance, and the answer holds nothing of the caller's to protect.
    /// </summary>
    public const string CorsPolicyName = "PublicHealth";

    /// <summary>Maps the health endpoint.</summary>
    /// <param name="group">Group the endpoint is added to.</param>
    public static void MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", GetHealth)
            .RequireCors(CorsPolicyName)
            .WithDescription("Returns 200 with a status word while the API is up.");
    }

    private static IResult GetHealth() =>
        Results.Json(new HealthContract("ok", DateTimeOffset.UtcNow));
}
