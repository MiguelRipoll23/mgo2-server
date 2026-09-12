using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Identity;

/// <summary>
/// Creates the account and character the gameplay server presents to the clients
/// that join its matches. The row carries an explicit identifier, because the
/// peer identifier announced on the peer-to-peer channel is the character
/// identifier and must not depend on how many characters exist already.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="cryptographyService">Service that hashes the account password.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class GameplayServerAccountService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    CryptographyService cryptographyService,
    ILogger<GameplayServerAccountService> logger)
{
    /// <summary>Display name of the account the gameplay server logs in with.</summary>
    public const string DisplayName = "server";

    /// <summary>Password of the gameplay server account.</summary>
    public const string Password = "server";

    /// <summary>Name of the character the gameplay server plays as.</summary>
    public const string CharacterName = "server";

    /// <summary>Comment shown for the gameplay server character.</summary>
    public const string CharacterComment = "Gameplay server";

    /// <summary>Creates the account and its character when they are missing.</summary>
    /// <param name="characterIdentifier">Identifier the character must carry.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when another character already holds the identifier.</exception>
    /// <returns>The identifier of the gameplay server character.</returns>
    public async Task<int> EnsureAccountAsync(
        int characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO users (display_name, password)
             VALUES ({DisplayName}, {cryptographyService.ComputeMd5Hex(Password)})
             ON CONFLICT (display_name) DO NOTHING
             """,
            cancellationToken);

        var userIdentifier = await context.Users
            .Where(user => user.DisplayName == DisplayName)
            .Select(user => user.Identifier)
            .FirstAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO characters (id, user_id, name, comment)
             VALUES ({characterIdentifier}, {userIdentifier}, {CharacterName}, {CharacterComment})
             ON CONFLICT DO NOTHING
             """,
            cancellationToken);

        // The character was inserted with an explicit identifier, so the
        // sequence has to move past it or the next account would collide.
        await context.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('characters', 'id'), (SELECT MAX(id) FROM characters))",
            cancellationToken);

        var actualIdentifier = await context.Characters
            .Where(character => character.Name == CharacterName)
            .Select(character => character.Identifier)
            .FirstAsync(cancellationToken);

        if (actualIdentifier != characterIdentifier)
        {
            throw new InvalidOperationException(
                $"The character '{CharacterName}' carries identifier {actualIdentifier} instead of " +
                $"{characterIdentifier}. The Gameplay server announces its character identifier as its " +
                "peer identifier, so the account must own that identifier.");
        }

        logger.LogInformation(
            "Gameplay server account ready: {DisplayName}, character {CharacterName} ({Identifier})",
            DisplayName,
            CharacterName,
            actualIdentifier);

        return actualIdentifier;
    }
}
