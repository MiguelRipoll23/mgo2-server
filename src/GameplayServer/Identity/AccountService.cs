using System.Security.Cryptography;
using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Identity;

/// <summary>
/// Creates the account and character the gameplay server presents to the clients
/// that join its matches. The row carries an explicit identifier, because the
/// peer identifier announced on the peer-to-peer channel is the character
/// identifier and must not depend on how many characters exist already.
/// The credentials come from configuration; when no password is configured a
/// random one is generated per process, so the account cannot be logged into
/// with a known default.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="cryptographyService">Service that hashes the account password.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the service.</param>
public sealed class AccountService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    CryptographyService cryptographyService,
    IOptions<ServerOptions> options,
    ILogger<AccountService> logger)
{
    /// <summary>Comment shown for the gameplay server character.</summary>
    public const string CharacterComment = "Gameplay server";

    private readonly string accountName = options.Value.GameplayServerAccountName;
    private readonly string accountPassword = string.IsNullOrEmpty(options.Value.GameplayServerAccountPassword)
        ? Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32))
        : options.Value.GameplayServerAccountPassword;
    private readonly string characterName = options.Value.GameplayServerCharacterName;

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

        // MD5 is unsalted because the client computes and sends the digest
        // itself; the server only reproduces what the protocol carries, so this
        // must not be "upgraded" to a modern KDF without a client-side change.
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO users (display_name, password)
             VALUES ({accountName}, {cryptographyService.ComputeMd5Hex(accountPassword)})
             ON CONFLICT (display_name) DO UPDATE SET password = EXCLUDED.password
             """,
            cancellationToken);

        var userIdentifier = await context.Users
            .Where(user => user.DisplayName == accountName)
            .Select(user => user.Identifier)
            .FirstAsync(cancellationToken);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO characters (id, user_id, name, comment)
             VALUES ({characterIdentifier}, {userIdentifier}, {characterName}, {CharacterComment})
             ON CONFLICT DO NOTHING
             """,
            cancellationToken);

        // The character was inserted with an explicit identifier, so the
        // sequence has to move past it, or the next account would collide. The
        // coalesce keeps this from failing on an empty table.
        await context.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('characters', 'id'), (SELECT coalesce(MAX(id), 1) FROM characters))",
            cancellationToken);

        var actualIdentifier = await context.Characters
            .Where(character => character.Name == characterName)
            .Select(character => character.Identifier)
            .FirstAsync(cancellationToken);

        if (actualIdentifier != characterIdentifier)
        {
            throw new InvalidOperationException(
                $"The character '{characterName}' carries identifier {actualIdentifier} instead of " +
                $"{characterIdentifier}. The Gameplay server announces its character identifier as its " +
                "peer identifier, so the account must own that identifier.");
        }

        logger.LogInformation(
            "Gameplay server account ready: {AccountName}, character {CharacterName} ({Identifier})",
            accountName,
            characterName,
            actualIdentifier);

        return actualIdentifier;
    }
}
