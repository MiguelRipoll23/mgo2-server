using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Infrastructure.Persistence;

/// <summary>
/// Creates the schema and seeds the rows a fresh database needs to accept a
/// client at all: the game types a lobby can be configured with. No lobby is
/// seeded; every lobby server publishes its own row from the environment when it
/// starts, whether it hosts a gameplay lobby or a permanent endpoint.
/// </summary>
/// <param name="contextFactory">Factory used to create database contexts.</param>
/// <param name="logger">Logger of the initializer.</param>
public sealed class DatabaseInitializer(
    IDbContextFactory<Mgo2DatabaseContext> contextFactory,
    ILogger<DatabaseInitializer> logger)
{
    /// <summary>
    /// Identifier of the advisory lock that serialises initialisation. The
    /// deployment starts every container at once against the same database, so
    /// only the holder of the lock may create the schema and insert the seeded
    /// rows; the rest wait here and then find the database ready.
    /// </summary>
    private const long InitializationLockIdentifier = 0x6D676F324C4F434B;

    /// <summary>
    /// Game types, inserted with explicit identifiers so that the identifier
    /// equals the game identifier carried by the wire format. That keeps the
    /// subtype of a lobby consistent with the value the client sends.
    /// </summary>
    private static readonly (int Identifier, int GameIdentifier, string Name)[] GameTypes =
    [
        (0, 0, "None"),
        (1, 1, "Free Battle"),
        (2, 2, "Automatching"),
        (3, 3, "Tournament"),
        (4, 4, "Survival"),
        (5, 5, "Unknown"),
        (6, 6, "Unknown"),
        (7, 7, "Basic Training"),
        (8, 8, "Combat Training"),
        (10, 10, "Tournament Registration"),
    ];

    /// <summary>Creates the schema and seeds the game types a fresh database needs.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // The lock lives as long as the session that took it, so the connection
        // is held open for the whole initialisation.
        await context.Database.OpenConnectionAsync(cancellationToken);
        await AcquireInitializationLockAsync(context, cancellationToken);

        try
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
            await SeedAsync(context, cancellationToken);
        }
        finally
        {
            await ReleaseInitializationLockAsync(context, cancellationToken);
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Seeds the game types in a single transaction, so that no other server
    /// instance ever sees a half-seeded database. The statements are idempotent:
    /// they repair a row that was removed, without touching the lobby rows the
    /// lobby servers registered for themselves.
    /// </summary>
    /// <param name="context">Context to seed through.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task SeedAsync(Mgo2DatabaseContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding the lobby game types");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (identifier, gameIdentifier, name) in GameTypes)
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO lobby_game_types (id, game_id, name)
                 VALUES ({identifier}, {gameIdentifier}, {name})
                 ON CONFLICT (id) DO NOTHING
                 """,
                cancellationToken);
        }

        await SynchronizeSequenceAsync(context, "lobby_game_types", cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Moves the identity sequence of a table past the identifiers that were
    /// inserted explicitly, so the next generated row cannot collide with them.
    /// </summary>
    /// <param name="context">Context to run through.</param>
    /// <param name="table">Name of the table to synchronise.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static Task SynchronizeSequenceAsync(
        Mgo2DatabaseContext context,
        string table,
        CancellationToken cancellationToken)
    {
        // The table name is a constant of this class, never a value from a
        // caller. The coalesce keeps the statement from failing when the table
        // is still empty (MAX(id) is null there).
        var statement =
            $"SELECT setval(pg_get_serial_sequence('{table}', 'id'), (SELECT coalesce(MAX(id), 1) FROM {table}))";

        return context.Database.ExecuteSqlRawAsync(statement, cancellationToken);
    }

    /// <summary>Takes the initialisation lock, waiting for the current holder to release it.</summary>
    /// <param name="context">Context holding the connection.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static Task AcquireInitializationLockAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_lock({InitializationLockIdentifier})",
            cancellationToken);

    /// <summary>Releases the initialisation lock.</summary>
    /// <param name="context">Context holding the connection.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static Task ReleaseInitializationLockAsync(
        Mgo2DatabaseContext context,
        CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_unlock({InitializationLockIdentifier})",
            cancellationToken);
}
