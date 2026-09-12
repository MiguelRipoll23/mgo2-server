using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Errors;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Http.Endpoints;

/// <summary>The lobby endpoints of the authenticated API surface.</summary>
internal static class LobbyEndpoints
{
    /// <summary>Maps the lobby endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapLobbyEndpoints(this RouteGroupBuilder group)
    {
        var lobbies = group.MapGroup("/lobbies")
            .WithTags("Lobbies")
            .RequireAuthorization();

        lobbies.MapGet("/", ListLobbiesAsync)
            .WithSummary("List lobbies")
            .WithDescription("Returns a list of all registered lobbies ordered by name");

        lobbies.MapGet("/{id:int}", GetLobbyAsync)
            .WithSummary("Get lobby")
            .WithDescription("Returns a single lobby by its numeric identifier");

        lobbies.MapPost("/", CreateLobbyAsync)
            .WithSummary("Create lobby")
            .WithDescription("Creates a new lobby and returns the created resource");

        lobbies.MapPatch("/{id:int}", PatchLobbyAsync)
            .WithSummary("Update lobby")
            .WithDescription("Partially updates an existing lobby by its numeric identifier");

        lobbies.MapDelete("/{id:int}", DeleteLobbyAsync)
            .WithSummary("Delete lobby")
            .WithDescription("Permanently removes a lobby by its numeric identifier");
    }

    private static async Task<IResult> ListLobbiesAsync(LobbyService lobbyService, CancellationToken cancellationToken)
    {
        var lobbies = await lobbyService.FindAllAsync(cancellationToken);
        return Results.Ok(lobbies.Select(lobby => lobby.ToContract()));
    }

    private static async Task<IResult> GetLobbyAsync(
        LobbyService lobbyService,
        int id,
        CancellationToken cancellationToken)
    {
        var lobby = await lobbyService.FindByIdAsync(id, cancellationToken);
        return Results.Ok(lobby.ToContract());
    }

    private static async Task<IResult> CreateLobbyAsync(
        LobbyService lobbyService,
        LobbyRequest request,
        CancellationToken cancellationToken)
    {
        var created = await lobbyService.CreateAsync(
            new LobbyCreateInput(
                (LobbyType)request.TypeId,
                request.Name,
                request.IpAddress,
                request.Port,
                request.SubtypeId,
                request.PlayersCount),
            cancellationToken);

        return Results.Json(created.ToContract(), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> PatchLobbyAsync(
        LobbyService lobbyService,
        int id,
        LobbyPatchRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await lobbyService.UpdateAsync(
            id,
            lobby =>
            {
                lobby.Type = request.TypeId is { } typeId ? (LobbyType)typeId : lobby.Type;
                lobby.SubtypeIdentifier = request.SubtypeId ?? lobby.SubtypeIdentifier;
                lobby.Name = request.Name ?? lobby.Name;
                lobby.IpAddress = request.IpAddress ?? lobby.IpAddress;
                lobby.Port = request.Port ?? lobby.Port;
                lobby.PlayersCount = request.PlayersCount ?? lobby.PlayersCount;
            },
            cancellationToken);

        return Results.Ok(updated.ToContract());
    }

    private static async Task<IResult> DeleteLobbyAsync(
        LobbyService lobbyService,
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            await lobbyService.RemoveAsync(id, cancellationToken);
        }
        catch (ServerException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            return Results.NotFound(new { exception.Code, exception.Message });
        }

        return Results.NoContent();
    }
}
