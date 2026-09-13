using Mgo2Server.Shared.Domain;
using Mgo2Server.Shared.Errors;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Authentication;

/// <summary>Account created by a registration.</summary>
/// <param name="Identifier">Identifier of the account.</param>
/// <param name="DisplayName">Display name of the account.</param>
public sealed record RegistrationResponse(int Identifier, string DisplayName);

/// <summary>
/// Creates accounts through the public registration endpoint. The password is
/// hashed the same way the client hashes it, so the stored value can be
/// compared against a login later.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="cryptographyService">Service that hashes passwords.</param>
public sealed class RegistrationService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    CryptographyService cryptographyService) : DomainService(contextFactory)
{
    /// <summary>Registers a new account.</summary>
    /// <param name="displayName">Display name of the account.</param>
    /// <param name="password">Password of the account, in clear text.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <exception cref="ServerException">Thrown when the display name is already taken.</exception>
    public async Task<RegistrationResponse> RegisterAsync(
        string displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var taken = await context.Users
            .AsNoTracking()
            .AnyAsync(user => user.DisplayName == displayName, cancellationToken);

        if (taken)
        {
            throw new ServerException("CONFLICT", "Display name is already taken", 409);
        }

        var user = new User
        {
            DisplayName = displayName,
            Password = cryptographyService.ComputeMd5Hex(password),
        };

        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the check-then-insert race against another registration.
            context.ChangeTracker.Clear();
            var takenByRace = await context.Users
                .AsNoTracking()
                .AnyAsync(existing => existing.DisplayName == displayName, cancellationToken);

            if (takenByRace)
            {
                throw new ServerException("CONFLICT", "Display name is already taken", 409);
            }

            throw;
        }

        return new RegistrationResponse(user.Identifier, user.DisplayName);
    }
}
