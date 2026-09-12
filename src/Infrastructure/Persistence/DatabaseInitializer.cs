using Mgo2Server.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Infrastructure.Persistence;

/// <summary>
/// Creates the schema and seeds the rows a fresh database needs to accept a
/// client at all: the game types a lobby can be configured with and the two
/// permanent endpoints, the gate and the account server. Gameplay lobbies are
/// not seeded; a game lobby server publishes its own row when it starts.
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
        (7, 7, "Training"),
        (10, 10, "Tournament Registration"),
    ];

    /// <summary>
    /// The permanent endpoints. The client expects the list index and the lobby
    /// type to coincide and rows are ordered by identifier, so the gate is the
    /// first row and the account server the second; every gameplay lobby that a
    /// server registers afterwards takes a higher identifier.
    /// </summary>
    private static readonly (int Identifier, int Type, int Subtype, string Name, int Port)[] PermanentLobbies =
    [
        (1, 0, 0, "GATE", 5731),
        (2, 1, 0, "ACCOUNT", 5732),
    ];

    /// <summary>Creates the schema and seeds the rows a fresh database needs.</summary>
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
    /// Seeds the game types and the permanent endpoints in a single transaction,
    /// so that no other server instance ever sees a half-seeded database. The
    /// statements are idempotent: they repair a row that was removed without
    /// touching the rows the game lobby servers registered for themselves.
    /// </summary>
    /// <param name="context">Context to seed through.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task SeedAsync(Mgo2DatabaseContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding game types and the permanent lobbies");

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

        foreach (var (identifier, type, subtype, name, port) in PermanentLobbies)
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO lobbies (id, type_id, subtype_id, name, ip_address, port, players_count)
                 VALUES ({identifier}, {type}, {subtype}, {name}, {"0.0.0.0"}, {port}, {0})
                 ON CONFLICT (id) DO NOTHING
                 """,
                cancellationToken);
        }

        await SynchronizeSequenceAsync(context, "lobbies", cancellationToken);

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
        // The table name is a constant of this class, never a value from a caller.
        var statement =
            $"SELECT setval(pg_get_serial_sequence('{table}', 'id'), (SELECT MAX(id) FROM {table}))";

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
