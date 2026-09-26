using System.Data.Common;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the listing the event testing tools read the other half of their teams
/// from. A formed team is a row, so the tool that shows it has to be able to ask
/// the database for it, and the query has to compile against the provider even
/// when no row can be read: an untranslatable projection leaves the page
/// reporting nothing at all rather than the teams that are there.
/// </summary>
[Trait("Category", "Shared")]
public sealed class EventTeamListingTests
{
    [Fact]
    public async Task The_listing_compiles_against_the_provider()
    {
        var service = new EventTeamListingService(new UnreachableDbContextFactory());

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => service.ListAsync(9, CancellationToken.None));

        // The database is not there, so the query cannot return rows. It is
        // compiled before anything is read, so a connection failure is the whole
        // proof that the projection is expressible.
        Assert.IsAssignableFrom<DbException>(exception.InnerException);
    }

    [Fact]
    public async Task A_lobby_with_no_identifier_lists_nothing_without_touching_the_database()
    {
        // Zero is not a lobby any row can name, so the answer is empty rather
        // than a query that would return rows belonging to some other lobby.
        var factory = new CountingDbContextFactory();
        var service = new EventTeamListingService(factory);

        var teams = await service.ListAsync(0, CancellationToken.None);

        Assert.Empty(teams);
        Assert.Equal(0, factory.Created);
    }

    /// <summary>Fails the way a database that cannot be reached fails.</summary>
    private sealed class UnreachableDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        public Mgo2DatabaseContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                .Options);
    }

    /// <summary>
    /// Counts the contexts it hands out and hands out nothing useful, so a test
    /// can prove a path did not reach the database at all.
    /// </summary>
    private sealed class CountingDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        /// <summary>How many contexts were asked for.</summary>
        public int Created { get; private set; }

        public Mgo2DatabaseContext CreateDbContext()
        {
            Created++;
            return new Mgo2DatabaseContext(
                new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                    .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                    .Options);
        }
    }
}
