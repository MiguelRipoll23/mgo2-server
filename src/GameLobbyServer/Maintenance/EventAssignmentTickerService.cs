using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Looks for a host for the matches that are waiting for one, and tells the two
/// teams when it finds one.
/// <para>
/// This is the second of the two matchmaking sweeps: pairing named the two teams
/// on its own, faster interval, and this one finds them a room. Running them at
/// different intervals is what makes "opponent found" and "game found" two
/// screens instead of one — a match sitting between the two sweeps is a pair the
/// client has been told about but whose room has not been chosen.
/// </para>
/// <para>
/// The sweep is also what covers a host that a finished match has just released:
/// the pool is read fresh on each run, so a room becomes available to the next
/// waiting match without anything having to announce it.
/// </para>
/// </summary>
/// <param name="assignmentService">Service that assigns and publishes a match.</param>
/// <param name="options">Event configuration, which sets the tick interval.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class EventAssignmentTickerService(
    EventAssignmentService assignmentService,
    IOptions<EventOptions> options,
    ILogger<EventAssignmentTickerService> logger)
    : PeriodicWorker(Interval(options), logger, FailureBackoffCeiling)
{
    /// <summary>
    /// Longest a failing sweep waits before trying again. Short, because two
    /// teams are watching a match-found screen that has not been sent yet.
    /// </summary>
    private static readonly TimeSpan FailureBackoffCeiling = TimeSpan.FromMinutes(1);

    private int lobbyIdentifier;

    /// <summary>Starts the sweep for the lobby this instance registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the registered lobby.</param>
    /// <param name="lobbySubtype">Mode of that lobby, which a host has to match.</param>
    public void StartFor(int lobbyIdentifier, int lobbySubtype)
    {
        // A lobby that cannot hold a match has no match to look for a host for,
        // and no room it could qualify as one.
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

        var assignments = await assignmentService.TryAssignWaitingAsync(
            lobbyIdentifier,
            cancellationToken);

        foreach (var assignment in assignments)
        {
            logger.LogInformation(
                "Event match {MatchIdentifier} assigned to room {GameIdentifier} and published",
                assignment.MatchIdentifier,
                assignment.GameIdentifier);
        }
    }

    private static TimeSpan Interval(IOptions<EventOptions> options) =>
        TimeSpan.FromMilliseconds(Math.Clamp(
            options.Value.GameSearchIntervalMilliseconds,
            100,
            600_000));
}
