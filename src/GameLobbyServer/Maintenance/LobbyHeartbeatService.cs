using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Keeps the lobby this instance hosts alive. Every tick refreshes the
/// heartbeat of its row, so the gate keeps serving it, and republishes the
/// player counts of this instance, so a lobby that nobody joins or leaves for a
/// while does not lose its population.
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="lobbyTracker">Service that counts the sessions of this instance.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class LobbyHeartbeatService(
    LobbyService lobbyService,
    LobbyTrackerService lobbyTracker,
    IOptions<ServerOptions> options,
    ILogger<LobbyHeartbeatService> logger)
    : PeriodicWorker(TimeSpan.FromSeconds(options.Value.LobbyHeartbeatIntervalSeconds), logger)
{
    private int lobbyIdentifier;

    /// <summary>Starts the heartbeat for the lobby this instance registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the registered lobby.</param>
    public void StartFor(int lobbyIdentifier)
    {
        this.lobbyIdentifier = lobbyIdentifier;
        Start();
    }

    /// <inheritdoc />
    protected override async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (lobbyIdentifier <= 0)
        {
            return;
        }

        await lobbyService.HeartbeatAsync(lobbyIdentifier, cancellationToken);
        await lobbyTracker.SynchronizeAllLobbyCountsAsync(cancellationToken);
    }
}
