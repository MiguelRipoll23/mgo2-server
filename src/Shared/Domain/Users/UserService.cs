using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Users;

/// <summary>Owns the account records.</summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class UserService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Finds an account by its login name.</summary>
    /// <param name="displayName">Login name of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<User?> FindByDisplayNameAsync(
        string displayName,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.DisplayName == displayName, cancellationToken);
    }

    /// <summary>Finds an account by its identifier.</summary>
    /// <param name="userIdentifier">Identifier of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<User?> FindByIdAsync(int userIdentifier, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Identifier == userIdentifier, cancellationToken);
    }

    /// <summary>Creates an account.</summary>
    /// <param name="displayName">Login name of the account.</param>
    /// <param name="passwordHash">Hashed password of the account.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<User> CreateAsync(
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        var created = new User { DisplayName = displayName, Password = passwordHash };
        context.Users.Add(created);
        await context.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>Stores the character an account most recently selected.</summary>
    /// <param name="userIdentifier">Identifier of the account.</param>
    /// <param name="characterIdentifier">Identifier of the character, or <c>null</c> to clear it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetCurrentCharacterAsync(
        int userIdentifier,
        int? characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Users
            .Where(user => user.Identifier == userIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.CurrentCharacterIdentifier, characterIdentifier),
                cancellationToken);
    }

    /// <summary>Stores the character an account designates as its main.</summary>
    /// <param name="userIdentifier">Identifier of the account.</param>
    /// <param name="characterIdentifier">Identifier of the character, or <c>null</c> to clear it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task SetMainCharacterAsync(
        int userIdentifier,
        int? characterIdentifier,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        await context.Users
            .Where(user => user.Identifier == userIdentifier)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.MainCharacterIdentifier, characterIdentifier),
                cancellationToken);
    }
}
