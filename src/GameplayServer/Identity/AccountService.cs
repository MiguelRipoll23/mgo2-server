using System.Security.Cryptography;
using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Identity;

/// <summary>
/// Gets or creates the account and character the gameplay server presents to
/// clients that join its matches.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="cryptographyService">Service that hashes the account password.</param>
/// <param name="options">Options of this instance.</param>
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

    /// <summary>Gets or creates the gameplay server account and character by name.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The identifier of the gameplay server character.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured character is inactive or belongs to another account.
    /// </exception>
    public async Task<int> GetOrCreateCharacterIdentifierAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Ensuring gameplay host account {AccountName} and character {CharacterName}",
            accountName,
            characterName);

        if (!DedicatedHostNameUtils.IsDedicatedHostName(characterName))
        {
            throw new InvalidOperationException(
                $"The gameplay server character name '{characterName}' must start with " +
                $"'{DedicatedHostNameUtils.DedicatedHostNamePrefix}'.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // MD5 is unsalted because the client computes and sends the digest
        // itself; the server only reproduces what the protocol carries, so this
        // must not be "upgraded" to a modern KDF without a client-side change.
        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO accounts (display_name, password)
             VALUES ({accountName}, {cryptographyService.ComputeMd5Hex(accountPassword)})
             ON CONFLICT (display_name) DO NOTHING
             """,
            cancellationToken);

        var accountIdentifier = await context.Accounts
            .Where(account => account.DisplayName == accountName)
            .Select(account => account.Identifier)
            .FirstAsync(cancellationToken);
        logger.LogDebug(
            "Gameplay host account {AccountName} resolved to account {AccountIdentifier}",
            accountName,
            accountIdentifier);

        await context.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO characters (account_id, name, comment, created_at)
             VALUES ({accountIdentifier}, {characterName}, {CharacterComment}, {DateTimeOffset.UtcNow})
             ON CONFLICT (name) DO NOTHING
             """,
            cancellationToken);

        var character = await context.Characters
            .Where(candidate => candidate.Name == characterName)
            .Select(candidate => new
            {
                candidate.Identifier,
                candidate.AccountIdentifier,
                candidate.Active,
            })
            .FirstAsync(cancellationToken);

        if (!character.Active || character.AccountIdentifier != accountIdentifier)
        {
            throw new InvalidOperationException(
                $"The configured gameplay server character '{characterName}' must be active and belong " +
                $"to account '{accountName}'.");
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogDebug(
            "Gameplay host character {CharacterName} resolved to character {CharacterIdentifier}; account transaction committed",
            characterName,
            character.Identifier);
        return character.Identifier;
    }
}
