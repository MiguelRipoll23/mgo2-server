using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Keeps the presence of this lobby's characters alive, records again the ones
/// whose row went missing, and clears away the rows of lobbies that are no
/// longer running.
/// <para>
/// The heartbeat covers the one case the boot clear cannot: a process that dies
/// and never comes back, so nobody is left to drop its rows. That is why the
/// sweep is not scoped to this lobby — cleaning up after a process that is not
/// running is exactly what a surviving process has to do — and why it is
/// idempotent, so several lobbies reaping at once is harmless.
/// </para>
/// <para>
/// Whatever the heartbeat touches is a live channel of this process, so a
/// character who left without a disconnect ever arriving is not kept alive by
/// it; the sweep takes that row once the stamp ages out.
/// </para>
/// </summary>
/// <param name="presenceService">Service that owns the presence rows.</param>
/// <param name="activeGameSessions">Sessions this process is serving.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class CharacterPresenceTickerService(
    CharacterPresenceService presenceService,
    ActiveGameSessionsService activeGameSessions,
    ILogger<CharacterPresenceTickerService> logger)
    : PeriodicWorker(CharacterPresenceService.HeartbeatInterval, logger, FailureBackoffCeiling)
{
    /// <summary>
    /// Longest a failing heartbeat waits before trying again: two beats, which is
    /// half the window after which the rows it stamps are taken for stale. A
    /// longer backoff would let this worker's own absence evict the live players
    /// it is stamping, so the wait is bounded by the meaning of the stamp rather
    /// than by what would keep the database quietest.
    /// </summary>
    private static readonly TimeSpan FailureBackoffCeiling =
        CharacterPresenceService.HeartbeatInterval * 2;

    private int lobbyIdentifier;

    /// <summary>Starts the ticker for the lobby this instance registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the registered lobby.</param>
    public void StartFor(int lobbyIdentifier)
    {
        this.lobbyIdentifier = lobbyIdentifier;
        Start();
    }

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var characterIdentifiers = activeGameSessions.List()
            .Select(session => session.CharacterIdentifier)
            .Where(characterIdentifier => characterIdentifier is not null)
            .Select(characterIdentifier => characterIdentifier!.Value)
            .Distinct()
            .ToList();

        var touched = await presenceService.HeartbeatAsync(characterIdentifiers, cancellationToken);

        // Fewer rows touched than characters connected means a row went missing under
        // a running process: an enter whose write never landed, or a row removed with
        // the lobby row it pointed at, which the foreign key cascades.
        if (characterIdentifiers.Count > 0 && touched != characterIdentifiers.Count)
        {
            var repaired = await RepairMissingRowsAsync(characterIdentifiers, cancellationToken);

            // Said out loud rather than only repaired: a sweep that is too aggressive
            // has to stay visible, and so does a write path that keeps failing.
            logger.LogWarning(
                "Presence heartbeat touched {TouchedCount} of {CharacterCount} rows for lobby {LobbyIdentifier}; recorded {RepairedCount} again",
                touched,
                characterIdentifiers.Count,
                lobbyIdentifier,
                repaired);
        }

        var reaped = await presenceService.ReapStaleAsync(cancellationToken);
        if (reaped > 0)
        {
            logger.LogInformation("Reaped {PresenceCount} stale presence rows", reaped);
        }
    }

    /// <summary>
    /// Records the connected characters that have no row, logging a failure
    /// rather than ending the tick with it: the sweep below is what cleans up
    /// after a process that is not running, and a repair that cannot land must
    /// not cost the tick its sweep.
    /// </summary>
    /// <param name="characterIdentifiers">Characters that are connected.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<int> RepairMissingRowsAsync(
        IReadOnlyCollection<int> characterIdentifiers,
        CancellationToken cancellationToken)
    {
        if (lobbyIdentifier <= 0)
        {
            return 0;
        }

        try
        {
            return await presenceService.RepairAsync(characterIdentifiers, lobbyIdentifier, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Presence repair for lobby {LobbyIdentifier} failed", lobbyIdentifier);
            return 0;
        }
    }
}
