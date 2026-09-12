using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Domain.Lobbies;

/// <summary>
/// Rebuilds the in-memory lobby cache on a fixed interval, so a lobby that
/// registers or stops being heartbeated appears or disappears from the served
/// list without a restart. The interval is a fraction of the heartbeat, because
/// a deployment starts every game lobby server at the same moment and the gate
/// must publish the complete list as soon as they have registered.
/// </summary>
/// <param name="lobbyService">Service that owns the cache.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class LobbyCacheRefreshService(
    LobbyService lobbyService,
    IOptions<ServerOptions> options,
    ILogger<LobbyCacheRefreshService> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.LobbyCacheRefreshIntervalSeconds), logger)
{
    /// <inheritdoc />
    protected override Task RunOnceAsync(CancellationToken cancellationToken) =>
        lobbyService.LoadCacheAsync(cancellationToken);
}
