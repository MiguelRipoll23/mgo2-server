using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Users;

/// <summary>Owns the account records.</summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="metricsService">Service the new account total is reported to.</param>
public sealed class UserService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    ServerMetricsService metricsService)
    : DomainService(contextFactory)
{
    /// <summary>Finds an account by its login name.</summary>
    /// <param name="displayName">Login name of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Account?> FindByDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.DisplayName == displayName, cancellationToken);
    }

    /// <summary>Finds an account by its identifier.</summary>
    /// <param name="accountIdentifier">Identifier of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Account?> FindByIdAsync(long accountIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Identifier == accountIdentifier, cancellationToken);
    }

    /// <summary>Creates an account.</summary>
    /// <param name="displayName">Login name of the account.</param>
    /// <param name="passwordHash">Hashed password of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<Account> CreateAsync(
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var created = new Account { DisplayName = displayName, Password = passwordHash };
        context.Accounts.Add(created);
        await context.SaveChangesAsync(cancellationToken);

        // The account count changed, so the new total is published rather than
        // polled for on a timer.
        await metricsService.ReportTotalUsersAsync(cancellationToken);
        return created;
    }

    /// <summary>Stores the character an account most recently selected.</summary>
    /// <param name="accountIdentifier">Identifier of the account.</param>
    /// <param name="characterIdentifier">Identifier of the character, or <c>null</c> to clear it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetCurrentCharacterAsync(
        long accountIdentifier,
        long? characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Accounts
            .Where(user => user.Identifier == accountIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.CurrentCharacterIdentifier, characterIdentifier),
                cancellationToken);
    }

    /// <summary>Stores the character an account designates as its main.</summary>
    /// <param name="accountIdentifier">Identifier of the account.</param>
    /// <param name="characterIdentifier">Identifier of the character, or <c>null</c> to clear it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetMainCharacterAsync(
        long accountIdentifier,
        long? characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Accounts
            .Where(user => user.Identifier == accountIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.MainCharacterIdentifier, characterIdentifier),
                cancellationToken);
    }
}
