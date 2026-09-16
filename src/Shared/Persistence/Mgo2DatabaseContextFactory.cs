using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mgo2Server.Shared.Persistence;

/// <summary>
/// Builds the context for the Entity Framework command-line tools, which run
/// outside the server and so have no dependency injection container to read the
/// options from.
/// <para>
/// Only the connection string is resolved here, and only because some commands
/// connect. Scaffolding a migration reads the model alone, so the fallback below
/// never has to point at a real server.
/// </para>
/// </summary>
public sealed class Mgo2DatabaseContextFactory : IDesignTimeDbContextFactory<Mgo2DatabaseContext>
{
    /// <summary>Connection string used when the environment names no database.</summary>
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=mgo2;Username=postgres";

    /// <inheritdoc />
    /// <param name="args">Arguments the tools were invoked with; unused.</param>
    public Mgo2DatabaseContext CreateDbContext(string[] args)
    {
        // Deliberately the same order as the servers resolve it, so a migration
        // tooled against the environment targets the database the server would.
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<Mgo2DatabaseContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new Mgo2DatabaseContext(options);
    }
}
