using System.Data.Common;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Tests;

/// <summary>
/// The applicant list is what the leader's Clan Affiliation screen waits on. The
/// query therefore has to compile against the provider even when no row can be
/// read: an untranslatable projection stalls the screen instead of reporting
/// anything, which is a failure the client can only show as a timeout.
/// </summary>
[Trait("Category", "Shared")]
public sealed class ClanApplicantQueryTests
{
    [Fact]
    public async Task ApplicantListCompilesAgainstTheProvider()
    {
        var service = new ClanService(new UnreachableDbContextFactory());

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => service.GetApplicantsAsync(1, CancellationToken.None));

        // The database is not there, so the query cannot return rows. It is
        // compiled before anything is read, so a connection failure is the whole
        // proof that the projection is expressible: an untranslatable one fails
        // before a socket is opened, and never reaches the provider at all.
        Assert.IsAssignableFrom<DbException>(exception.InnerException);
    }

    /// <summary>Fails the way a database that cannot be reached fails.</summary>
    private sealed class UnreachableDbContextFactory : IDbContextFactory<Mgo2DatabaseContext>
    {
        public Mgo2DatabaseContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<Mgo2DatabaseContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=mgo2;Username=postgres;Timeout=1")
                .Options);
    }
}
