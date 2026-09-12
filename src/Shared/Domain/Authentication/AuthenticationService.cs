using System.Security.Cryptography;
using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Authentication;

/// <summary>
/// Validates credentials and issues the login reply the gate server returns.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="sessionService">Service that stores the issued login session.</param>
public sealed class AuthenticationService(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    SessionService sessionService) : DomainService(contextFactory)
{
    /// <summary>
    /// Third field of the login reply. Its grammar differs between client
    /// builds and no single value is valid for both: the 1.0 client requires
    /// exactly one integer followed by three commas, while the 1.36 client
    /// requires ten integers separated by nine underscores, or an empty field.
    /// The values below mirror the table the 1.36 binary itself ships.
    /// </summary>
    private const string LoginPerks = "1000_1000_5000_10000_1000_3000_1000_1000_2000_1000";

    /// <summary>Length of the login token, in characters and bytes.</summary>
    private const int LoginTokenLength = 16;

    /// <summary>Reply sent when the credentials do not match an account.</summary>
    private const string FailedLoginReply = "1,0,0,0000000000000000";

    /// <summary>Finds the account matching a login name and password hash.</summary>
    /// <param name="displayName">Login name of the account.</param>
    /// <param name="passwordHash">Password hash presented by the client.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<User?> FindByCredentialsAsync(
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.DisplayName == displayName && user.Password == passwordHash,
                cancellationToken);
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

    /// <summary>
    /// Validates credentials and returns the login reply. The client keeps the
    /// sixteen characters of the token and derives the value it presents on
    /// check-session from them, so the derived value is what the session row
    /// stores.
    /// </summary>
    /// <param name="displayName">Login name of the account.</param>
    /// <param name="passwordHash">Password hash presented by the client.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<string> LoginAsync(
        string displayName,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        if (displayName.Equals("server", StringComparison.OrdinalIgnoreCase))
        {
            return FailedLoginReply;
        }

        var user = await FindByCredentialsAsync(displayName, passwordHash, cancellationToken);
        if (user is null)
        {
            return FailedLoginReply;
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(LoginTokenLength / 2);
        var sessionToken = Convert.ToHexStringLower(tokenBytes);
        var storedField = CryptoUtility.StoreSessionField(sessionToken);

        await sessionService.CreateSessionAsync(user.Identifier, storedField, cancellationToken);

        return $"0,{user.Identifier},{LoginPerks},{sessionToken}";
    }
}
