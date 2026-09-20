using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Removes the presence rows of lobbies that are no longer running, once a day at
/// midnight UTC.
/// <para>
/// Split from <see cref="CharacterPresenceTickerService"/> because the two have
/// nothing in common but the table. The beat is a liveness signal and has to
/// happen on the interval the staleness window is measured in; the delete happens
/// after a process that died, and a process that never comes back does not need
/// its rows gone any particular hour. Doing it in the ticker made every beat a
/// nine-instance cluster-wide <c>DELETE</c> for a case that is rare.
/// </para>
/// <para>
/// The sweep is not scoped to this lobby, and that is the point: cleaning up after
/// a process that is not running is exactly what a surviving process has to do.
/// Every lobby runs it and the delete is idempotent, so several reaping at once is
/// harmless.
/// </para>
/// <para>
/// Nothing is lost by deleting only daily. A row is unlisted as soon as its
/// <c>last_seen</c> leaves the stale window — the friend, search and clan readers
/// drop it — so the window is what keeps a ghost off a screen and this pass is
/// only what gives the space back.
/// </para>
/// </summary>
/// <param name="presenceService">Service that owns the presence rows.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class CharacterPresenceCleanupService(
    CharacterPresenceService presenceService,
    ILogger<CharacterPresenceCleanupService> logger)
    : DailyWorker(DailyWorker.MidnightUtc, logger)
{
    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var reaped = await presenceService.ReapStaleAsync(cancellationToken);
        if (reaped > 0)
        {
            logger.LogInformation("Reaped {PresenceCount} stale presence rows", reaped);
        }
    }
}
