using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Errors;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Http.Endpoints;

/// <summary>The game room endpoints of the authenticated API surface.</summary>
internal static class GameEndpoints
{
    /// <summary>Maps the game room endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapGameEndpoints(this RouteGroupBuilder group)
    {
        var games = group.MapGroup("/games")
            .WithTags("Games")
            .RequireAuthorization();

        games.MapGet("/", ListGamesAsync)
            .WithSummary("List all games")
            .WithDescription("Returns a list of all active game rooms ordered by ID");

        games.MapGet("/{id:int}", GetGameAsync)
            .WithSummary("Get game")
            .WithDescription("Returns a single game room by its numeric identifier");

        games.MapPost("/", CreateGameAsync)
            .WithSummary("Create a game")
            .WithDescription("Creates a new game room and returns the created resource");

        games.MapPatch("/{id:int}", PatchGameAsync)
            .WithSummary("Update a game")
            .WithDescription("Partially updates an existing game room by its numeric identifier");

        games.MapDelete("/{id:int}", DeleteGameAsync)
            .WithSummary("Delete a game")
            .WithDescription("Permanently removes a game room by its numeric identifier");
    }

    private static async Task<IResult> ListGamesAsync(GameService gameService, CancellationToken cancellationToken)
    {
        var games = await gameService.FindAllAsync(cancellationToken);
        return Results.Ok(games.Select(game => game.ToContract()));
    }

    private static async Task<IResult> GetGameAsync(
        GameService gameService,
        int id,
        CancellationToken cancellationToken)
    {
        var game = await gameService.FindByIdAsync(id, cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "Game not found", StatusCodes.Status404NotFound);

        return Results.Ok(game.ToContract());
    }

    private static async Task<IResult> CreateGameAsync(
        GameService gameService,
        GameRequest request,
        CancellationToken cancellationToken)
    {
        var created = await gameService.CreateAsync(
            game =>
            {
                game.HostIdentifier = request.HostId;
                game.LobbyIdentifier = request.LobbyId;
                game.Name = request.Name;
                game.Password = request.Password;
                game.Comment = request.Comment;
                game.MaximumPlayers = request.MaxPlayers;
                game.CurrentGame = request.CurrentGame;
                game.Games = request.Games;
                game.Stance = request.Stance;
                game.Ping = request.Ping;
                game.Common = request.Common;
                game.Rules = request.Rules;
                game.Status = request.Status;
            },
            cancellationToken);

        return Results.Json(created.ToContract(), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> PatchGameAsync(
        GameService gameService,
        int id,
        GamePatchRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await gameService.UpdateAsync(
            id,
            game =>
            {
                game.HostIdentifier = request.HostId ?? game.HostIdentifier;
                game.LobbyIdentifier = request.LobbyId ?? game.LobbyIdentifier;
                game.Name = request.Name ?? game.Name;
                game.Password = request.Password ?? game.Password;
                game.Comment = request.Comment ?? game.Comment;
                game.MaximumPlayers = request.MaxPlayers ?? game.MaximumPlayers;
                game.CurrentGame = request.CurrentGame ?? game.CurrentGame;
                game.Games = request.Games ?? game.Games;
                game.Stance = request.Stance ?? game.Stance;
                game.Ping = request.Ping ?? game.Ping;
                game.Common = request.Common ?? game.Common;
                game.Rules = request.Rules ?? game.Rules;
                game.Status = request.Status ?? game.Status;
            },
            cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "Game not found", StatusCodes.Status404NotFound);

        return Results.Ok(updated.ToContract());
    }

    private static async Task<IResult> DeleteGameAsync(
        GameService gameService,
        int id,
        CancellationToken cancellationToken)
    {
        var game = await gameService.FindByIdAsync(id, cancellationToken)
            ?? throw new ServerException("NOT_FOUND", "Game not found", StatusCodes.Status404NotFound);

        await gameService.DeleteAsync(game.Identifier, cancellationToken);
        return Results.NoContent();
    }
}
