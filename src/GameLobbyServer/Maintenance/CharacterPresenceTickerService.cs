using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Keeps the presence of this lobby's characters alive and clears away the rows
/// of lobbies that are no longer running.
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
    : PeriodicWorker(CharacterPresenceService.HeartbeatInterval, logger)
{
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

        // Fewer rows touched than characters connected means rows went missing
        // under a running process, which is worth surfacing rather than
        // resurrecting: the heartbeat updates what exists and creates nothing,
        // so a sweep that is too aggressive shows up here instead of hiding.
        if (characterIdentifiers.Count > 0 && touched != characterIdentifiers.Count)
        {
            logger.LogWarning(
                "Presence heartbeat touched {TouchedCount} rows for {CharacterCount} connected characters",
                touched,
                characterIdentifiers.Count);
        }

        var reaped = await presenceService.ReapStaleAsync(cancellationToken);
        if (reaped > 0)
        {
            logger.LogInformation("Reaped {PresenceCount} stale presence rows", reaped);
        }
    }
}
