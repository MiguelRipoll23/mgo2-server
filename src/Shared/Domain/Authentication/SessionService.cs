using Mgo2Server.Shared.Persistence;
using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Authentication;

/// <summary>
/// Owns the login sessions: an account has at most one session row, which is
/// replaced when the account logs in again.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public sealed class SessionService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
    : DomainService(contextFactory)
{
    /// <summary>Finds the session carrying a token.</summary>
    /// <param name="token">Session field presented by the client.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<UserSession?> FindByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);
        return await context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(session => session.Token == token, cancellationToken);
    }

    /// <summary>Stores the session field for an account, replacing any existing row.</summary>
    /// <param name="userIdentifier">Identifier of the account.</param>
    /// <param name="token">Session field to store.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<UserSession> CreateSessionAsync(
        int userIdentifier,
        string token,
        CancellationToken cancellationToken = default)
    {
        await using var context = await CreateContextAsync(cancellationToken);

        var existing = await context.UserSessions
            .FirstOrDefaultAsync(session => session.UserIdentifier == userIdentifier, cancellationToken);

        if (existing is not null)
        {
            existing.Token = token;
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var created = new UserSession { UserIdentifier = userIdentifier, Token = token };
        context.UserSessions.Add(created);
        await context.SaveChangesAsync(cancellationToken);
        return created;
    }
}
