using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Games;

/// <summary>
/// The connection half of the game service: the peer-to-peer endpoints the
/// players register and the roster of each room.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Stores the peer-to-peer endpoint of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="information">Endpoint to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SaveConnectionInformationAsync(
        int characterIdentifier,
        ConnectionInformation information,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var updatedAt = DateTimeOffset.UtcNow;

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO character_connections
                 (character_id, public_ip, public_port, private_ip, private_port, updated_at)
             VALUES
                 ({characterIdentifier}, {information.PublicIpAddress}, {information.PublicPort},
                  {information.PrivateIpAddress}, {information.PrivatePort}, {updatedAt})
             ON CONFLICT (character_id)
             DO UPDATE SET public_ip = {information.PublicIpAddress},
                           public_port = {information.PublicPort},
                           private_ip = {information.PrivateIpAddress},
                           private_port = {information.PrivatePort},
                           updated_at = {updatedAt}
             """,
            cancellationToken);
    }

    /// <summary>Returns the peer-to-peer endpoint of a character.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<ConnectionInformation?> GetConnectionInformationAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var connection = await context.CharacterConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.CharacterIdentifier == characterIdentifier, cancellationToken);

        return connection is null
            ? null
            : new ConnectionInformation(
                connection.PublicIpAddress,
                connection.PublicPort,
                connection.PrivateIpAddress,
                connection.PrivatePort);
    }

    /// <summary>Adds a character to the roster of a room; the host is always a member of its own room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task AddPlayerAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (characterIdentifier <= 0)
        {
            return;
        }

        await using var context = await CreateContextAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO game_players (game_id, character_id)
             VALUES ({gameIdentifier}, {characterIdentifier})
             ON CONFLICT (game_id, character_id) DO NOTHING
             """,
            cancellationToken);
    }

    /// <summary>
    /// Removes a character from the roster of a room, crediting the presence the
    /// removal ends.
    /// <para>
    /// The credit and the delete share a transaction because the roster row carries
    /// the only record of when the player arrived: once it is gone the interval is
    /// unrecoverable, so the two must not come apart. Every path that removes a
    /// player goes through here, which is what makes the training totals complete.
    /// </para>
    /// </summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task RemovePlayerAsync(
        int gameIdentifier,
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await CreditTrainingTimeAsync(context, [gameIdentifier], characterIdentifier, cancellationToken);

        await context.GamePlayers
            .Where(player => player.GameIdentifier == gameIdentifier && player.CharacterIdentifier == characterIdentifier)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Credits presence in the training rooms of the given games to the totals of
    /// the characters it covered, then forgets it.
    /// <para>
    /// The split is the hosting lobby's game type: a training lobby credits
    /// training-mode time, and a combat training lobby credits instructor time for
    /// whoever hosted it and student time for everyone else. A room in any other
    /// lobby credits nothing and leaves no row, because there the play time is
    /// derived from the round reports instead — the more complete figure for a lobby
    /// whose sessions report, and no figure at all for one whose sessions do not.
    /// </para>
    /// <para>
    /// Every path that ends presence calls this before the roster goes: a player
    /// leaving, a room being torn down, a lobby going away, and the reaper collecting
    /// rooms whose server died. Missing any one of them loses the interval for good,
    /// because the roster row's join time is the only record of it.
    /// </para>
    /// </summary>
    /// <param name="context">Context holding the open transaction.</param>
    /// <param name="gameIdentifiers">Rooms whose rosters are credited.</param>
    /// <param name="characterIdentifier">Character to credit, or zero for everyone present.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task CreditTrainingTimeAsync(
        Mgo2DatabaseContext context,
        IReadOnlyCollection<int> gameIdentifiers,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        // One statement per room: the filter stays a plain integer parameter, which
        // keeps the statement free of a nullable parameter whose type the provider
        // would have to guess at.
        foreach (var gameIdentifier in gameIdentifiers)
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO character_training_times AS totals
                     (character_id, training_mode_seconds, instructor_seconds, student_seconds)
                 SELECT player.character_id,
                     CASE WHEN lobby.subtype_id = {LobbySubtypeConstants.Training}
                         THEN presence.seconds ELSE 0 END,
                     CASE WHEN lobby.subtype_id = {LobbySubtypeConstants.CombatTraining}
                             AND room.host_id = player.character_id
                         THEN presence.seconds ELSE 0 END,
                     CASE WHEN lobby.subtype_id = {LobbySubtypeConstants.CombatTraining}
                             AND room.host_id <> player.character_id
                         THEN presence.seconds ELSE 0 END
                 FROM game_players player
                 JOIN games room ON room.id = player.game_id
                 JOIN lobbies lobby ON lobby.id = room.lobby_id
                 CROSS JOIN LATERAL (
                     SELECT greatest(0,
                         floor(extract(epoch from (now() - player.joined_at))))::bigint AS seconds
                 ) AS presence
                 WHERE lobby.subtype_id IN ({LobbySubtypeConstants.Training},
                         {LobbySubtypeConstants.CombatTraining})
                     AND player.game_id = {gameIdentifier}
                     AND ({characterIdentifier} = 0
                         OR player.character_id = {characterIdentifier})
                 ON CONFLICT (character_id) DO UPDATE SET
                     training_mode_seconds =
                         totals.training_mode_seconds + excluded.training_mode_seconds,
                     instructor_seconds = totals.instructor_seconds + excluded.instructor_seconds,
                     student_seconds = totals.student_seconds + excluded.student_seconds
                 """,
                cancellationToken);
        }
    }

    /// <summary>Returns the identifiers of the rooms of a lobby.</summary>
    /// <param name="context">Context to read through.</param>
    /// <param name="lobbyIdentifier">Identifier of the lobby.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static Task<List<int>> GameIdentifiersOfLobbyAsync(
        Mgo2DatabaseContext context,
        int lobbyIdentifier,
        CancellationToken cancellationToken) =>
        context.Games
            .AsNoTracking()
            .Where(game => game.LobbyIdentifier == lobbyIdentifier)
            .Select(game => game.Identifier)
            .ToListAsync(cancellationToken);

    /// <summary>Returns the training totals of a character, zeros when they have none.</summary>
    /// <param name="characterIdentifier">Identifier of the character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<TrainingSeconds> GetTrainingSecondsAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var totals = await context.TrainingTimes
            .AsNoTracking()
            .Where(row => row.CharacterIdentifier == characterIdentifier)
            .Select(row => new
            {
                row.TrainingModeSeconds,
                row.InstructorSeconds,
                row.StudentSeconds,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return totals is null
            ? default
            : new TrainingSeconds(
                totals.TrainingModeSeconds,
                totals.InstructorSeconds,
                totals.StudentSeconds);
    }

    /// <summary>Returns the roster of a room, host first, which is the order the player list expects.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="hostCharacterIdentifier">Identifier of the hosting character.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<List<int>> GetPlayersAsync(
        int gameIdentifier,
        int hostCharacterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var players = await context.GamePlayers
            .AsNoTracking()
            .Where(player => player.GameIdentifier == gameIdentifier)
            .Select(player => player.CharacterIdentifier)
            .ToListAsync(cancellationToken);

        var others = players.Where(identifier => identifier != hostCharacterIdentifier).ToList();
        return hostCharacterIdentifier > 0 ? [hostCharacterIdentifier, .. others] : others;
    }

    /// <summary>Counts the players in a room.</summary>
    /// <param name="gameIdentifier">Identifier of the room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<int> CountPlayersAsync(int gameIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.GamePlayers
            .AsNoTracking()
            .CountAsync(player => player.GameIdentifier == gameIdentifier, cancellationToken);
    }
}
