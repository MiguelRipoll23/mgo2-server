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
/// The sweep is what covers a match that pairs after its host is already sitting
/// there, and a host that a finished match has just released: a room is announced
/// the moment a client creates it, but neither of those two has anything left to
/// announce it, and both are found the same way by looking again.
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
        TimeSpan.FromMilliseconds(Math.Clamp(options.Value.OutcomeSweepMilliseconds, 100, 60_000));
}
