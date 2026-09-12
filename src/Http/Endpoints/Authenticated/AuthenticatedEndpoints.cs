namespace Mgo2Server.Http.Endpoints.Authenticated;

/// <summary>The authenticated API surface.</summary>
internal static class AuthenticatedEndpoints
{
    /// <summary>Maps the authenticated endpoints.</summary>
    /// <param name="app">Application the endpoints are added to.</param>
    public static void MapAuthenticatedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/").RequireAuthorization();

        group.MapNewsEndpoints();
        group.MapLobbyEndpoints();
        group.MapFlashNewsEndpoints();
        group.MapGameEndpoints();
    }
}
