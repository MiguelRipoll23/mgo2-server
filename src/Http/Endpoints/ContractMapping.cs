using Mgo2Server.Http.Contracts;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Http.Endpoints;

/// <summary>Maps the persistence and domain shapes onto the API contracts.</summary>
internal static class ContractMapping
{
    /// <summary>Maps a news article.</summary>
    /// <param name="article">Article to map.</param>
    public static NewsItemContract ToContract(this NewsArticle article) =>
        new(article.Identifier, article.Important, article.Time, article.Topic, article.Message);

    /// <summary>Maps a game room.</summary>
    /// <param name="game">Room to map.</param>
    public static GameContract ToContract(this Game game) =>
        new(
            game.Identifier,
            game.HostIdentifier,
            game.LobbyIdentifier,
            game.Name,
            game.Password,
            game.Comment,
            game.MaximumPlayers,
            game.CurrentGame,
            game.Games,
            game.Stance,
            game.Ping,
            game.Common,
            game.Rules,
            game.Status,
            game.CreatedAt?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

    /// <summary>Maps a lobby.</summary>
    /// <param name="lobby">Lobby to map.</param>
    public static LobbyContract ToContract(this Lobby lobby) =>
        new(
            lobby.Identifier,
            (int)lobby.Type,
            lobby.SubtypeIdentifier,
            lobby.Name,
            lobby.IpAddress,
            lobby.Port,
            lobby.PlayersCount,
            lobby.BeginnerOnly,
            lobby.ExpansionOnly,
            lobby.NoHeadshot,
            lobby.ReplaysOnly);

    /// <summary>Maps a lobby of the published cache.</summary>
    /// <param name="lobby">Lobby to map.</param>
    public static LobbyContract ToContract(this LobbyResponse lobby) =>
        new(
            lobby.Identifier,
            (int)lobby.Type,
            lobby.SubtypeIdentifier,
            lobby.Name,
            lobby.IpAddress,
            lobby.Port,
            lobby.PlayersCount,
            lobby.BeginnerOnly,
            lobby.ExpansionOnly,
            lobby.NoHeadshot,
            lobby.ReplaysOnly);
}
