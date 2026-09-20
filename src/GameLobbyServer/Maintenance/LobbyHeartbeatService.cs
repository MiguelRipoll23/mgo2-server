using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Keeps the lobby this instance hosts alive. Every tick refreshes the heartbeat
/// of its row, so the gate keeps serving it.
/// <para>
/// It writes nothing else. The published population used to be republished here,
/// and it is not any more: a count changes when a session joins or leaves, and
/// that is where it is written, so a beat that writes it too is writing the value
/// it last saw rather than the value that is true. Removing it also took the last
/// scheduled statement out of a worker whose whole job is a stamp, which is the
/// point of a list that nobody can see being written more often than it is read.
/// </para>
/// </summary>
/// <param name="lobbyService">Service that owns the lobby rows.</param>
/// <param name="options">Options of this instance.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class LobbyHeartbeatService(
    LobbyService lobbyService,
    IOptions<ServerOptions> options,
    ILogger<LobbyHeartbeatService> logger)
    : PeriodicWorker(
        TimeSpan.FromSeconds(options.Value.LobbyHeartbeatIntervalSeconds),
        logger,
        PeriodicWorker.NoBackoff)
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
    }
}
