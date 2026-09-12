namespace Mgo2Server.Http.Endpoints.Public;

/// <summary>The public (unauthenticated) API surface.</summary>
internal static class PublicEndpoints
{
    /// <summary>Maps the public endpoints.</summary>
    /// <param name="app">Application the endpoints are added to.</param>
    public static void MapPublicEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/");

        group.MapLoginEndpoints();
        group.MapAccountEndpoints();
        group.MapPolicyEndpoints();
        group.MapCheckVerEndpoints();
        group.MapDataListEdnpoints();
        group.MapFilesEndpoints();
    }
}
