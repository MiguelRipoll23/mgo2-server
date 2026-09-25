using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Maintenance;

/// <summary>
/// Decides the event matches whose players have all reported and whose reports
/// have settled, then tells both teams how the match ended.
/// <para>
/// The decision is a sweep rather than a timer per match, because the reports
/// they are made from arrive across two processes and outlive any one of them. A
/// timer set by whichever process saw the last report would be lost by a restart
/// at the exact moment it was needed, and a match nobody is left to decide stays
/// unplayed.
/// </para>
/// </summary>
/// <param name="matchService">Service that lists the lobby's live matches.</param>
/// <param name="outcomeService">Service that decides, completes and reports a match.</param>
/// <param name="tournamentMatchService">Service that pairs a bracket's ready fixtures.</param>
/// <param name="options">Event configuration, which sets the tick interval and the sweep.</param>
/// <param name="logger">Logger of the worker.</param>
public sealed class EventOutcomeTickerService(
    EventMatchService matchService,
    EventOutcomeService outcomeService,
    TournamentMatchService tournamentMatchService,
    IOptions<EventOptions> options,
    ILogger<EventOutcomeTickerService> logger)
    : PeriodicWorker(Interval(options), logger, FailureBackoffCeiling)
{
    /// <summary>
    /// Longest a failing sweep waits before trying again. Short, because a match
    /// that has been played but not decided is two teams sitting on a result.
    /// </summary>
    private static readonly TimeSpan FailureBackoffCeiling = TimeSpan.FromMinutes(1);

    private int lobbyIdentifier;

    /// <summary>Starts the sweep for the lobby this instance registered.</summary>
    /// <param name="lobbyIdentifier">Identifier of the registered lobby.</param>
    /// <param name="lobbySubtype">
    /// Mode of that lobby. Only an event lobby starts the sweep; the tournament
    /// draw it runs walks every field, so a deployment running any event lobby
    /// keeps every bracket advancing.
    /// </param>
    public void StartFor(int lobbyIdentifier, int lobbySubtype)
    {
        // A lobby that cannot hold a match has no match to decide, and a
        // non-event lobby nothing to draw for.
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

        // The draw is paired before the sweep decides anything: a match that was
        // created on this tick is not one this tick has reports for, and one
        // played since the last tick is still decided below. Doing it in one
        // worker keeps "the round that just opened is playable" and "the round
        // that just finished is decided" from depending on each other's timing.
        var paired = await tournamentMatchService.PairReadyFixturesAsync(cancellationToken);
        if (paired > 0)
        {
            logger.LogInformation("Tournament draw paired {PairedCount} fixtures", paired);
        }

        foreach (var match in await matchService.FindActiveByLobbyAsync(lobbyIdentifier, cancellationToken))
        {
            if (match.State != EventConstants.MatchAssignedState)
            {
                // Only a match that is in play can be decided; one still waiting
                // for a host has nothing to infer from.
                continue;
            }

            var decision = await outcomeService.TryCompleteAsync(match.Identifier, cancellationToken);
            if (decision is null)
            {
                continue;
            }

            var finished = decision.Value;

            logger.LogInformation(
                "Event match {MatchIdentifier} decided from reports: team {WinnerIdentifier} won, team "
                + "{LoserIdentifier} lost, {Delivered} sessions notified, bracket {BracketOutcome} "
                + "delivered to {BracketDelivered} sessions",
                match.Identifier,
                finished.Winner,
                finished.Loser,
                finished.Delivered,
                finished.Bracket?.Outcome.ToString() ?? "not a tournament match",
                finished.BracketDelivered);
        }
    }

    private static TimeSpan Interval(IOptions<EventOptions> options) =>
        TimeSpan.FromMilliseconds(Math.Clamp(options.Value.OutcomeSweepMilliseconds, 100, 60_000));
}
