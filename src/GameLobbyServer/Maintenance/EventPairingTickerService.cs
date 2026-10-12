using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Pairs the teams waiting in this lobby's field, two at a time, on its own
/// interval.
/// <para>
/// Pairing is the first of the two matchmaking sweeps, and it is a sweep rather
/// than work the entry request does: a team that enters is told it is waiting,
/// and the two teams are made a match on this cadence. Matching the two also
/// announces them to each other — the client's "opponent found" — in the pairing
/// record, which names the pair and no room. The second sweep, the game search,
/// finds the pair a room on its own, slower interval and re-writes that record
/// with the room; keeping the announcement here is what puts "opponent found" on
/// screen a sweep before "game found".
/// </para>
/// <para>
/// The cadence is configuration rather than a literal, so a deployment can pace
/// its field; the same interval is the delay the client spends on each of its
/// two screens.
/// </para>
/// </summary>
/// <param name="matchmakingService">Service that owns the queue and the pairing.</param>
/// <param name="options">Event configuration, which sets the tick interval.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class EventPairingTickerService(
    EventMatchmakingService matchmakingService,
    IOptions<EventOptions> options,
    ILogger<EventPairingTickerService> logger)
    : PeriodicWorker(Interval(options), logger, FailureBackoffCeiling)
{
    /// <summary>
    /// Longest a failing sweep waits before trying again. Short, because two
    /// teams are sitting on a waiting screen that has not moved.
    /// </summary>
    private static readonly TimeSpan FailureBackoffCeiling = TimeSpan.FromMinutes(1);

    private int lobbyIdentifier;

    /// <summary>Starts the sweep for the lobby this instance registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the registered lobby.</param>
    /// <param name="lobbySubtype">Mode of that lobby; only an event lobby is paired.</param>
    public void StartFor(int lobbyIdentifier, int lobbySubtype)
    {
        // A lobby that cannot hold a match has no field to pair, so it never
        // starts the sweep.
        var lobbyHoldsMatches = lobbyIdentifier > 0
            && EventConstants.IsEventSelector(lobbySubtype);

        if (!lobbyHoldsMatches)
        {
            return;
        }

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

        foreach (EventMatch match in await matchmakingService.TryPairWaitingAsync(
            lobbyIdentifier,
            cancellationToken))
        {
            logger.LogInformation(
                "Event teams {FirstTeamIdentifier} and {SecondTeamIdentifier} paired into match {MatchIdentifier}",
                match.FirstTeamIdentifier,
                match.SecondTeamIdentifier,
                match.Identifier);
        }
    }

    private static TimeSpan Interval(IOptions<EventOptions> options) =>
        TimeSpan.FromMilliseconds(Math.Clamp(
            options.Value.PairingIntervalMilliseconds,
            100,
            600_000));
}
