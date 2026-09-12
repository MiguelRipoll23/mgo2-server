using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain;

/// <summary>
/// Base class of the domain services. It creates one short-lived database
/// context per operation, which keeps long-lived connection handlers free of
/// shared context state.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
public abstract class DomainService(IDbContextFactory<Mgo2DatabaseContext> contextFactory)
{
    /// <summary>Factory used to create database contexts.</summary>
    protected IDbContextFactory<Mgo2DatabaseContext> ContextFactory { get; } = contextFactory;

    /// <summary>Creates a database context for the duration of one operation.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    protected Task<Mgo2DatabaseContext> CreateContextAsync(CancellationToken cancellationToken = default) =>
        ContextFactory.CreateDbContextAsync(cancellationToken);
}
