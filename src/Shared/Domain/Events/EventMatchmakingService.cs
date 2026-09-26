using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>What reconciling a team did to the queue.</summary>
public enum MatchmakingStatus
{
    /// <summary>The team is waiting for an opponent.</summary>
    Waiting,

    /// <summary>The team was paired and is waiting for a host.</summary>
    Paired,

    /// <summary>Nothing changed: the team was already queued or already paired.</summary>
    Unchanged,
}

/// <summary>Outcome of reconciling one team.</summary>
/// <param name="Status">What happened.</param>
/// <param name="MatchIdentifier">Match the team is in, when it is paired.</param>
/// <param name="OpponentTeamIdentifier">Opponent, when the team is paired.</param>
public readonly record struct MatchmakingResult(
    MatchmakingStatus Status,
    int MatchIdentifier,
    int OpponentTeamIdentifier);

/// <summary>
/// The Survival registration and pairing coordinator. Teams are paired only
/// inside the same lobby and match type, and a pairing is deliberately left
/// waiting for a real host: it is never promoted to a live match by inventing
/// host state.
/// <para>
/// The queue is instance state guarded by one lock. The pairing it produces is
/// written to the database, so the ordering is the only thing that is
/// per-process and the result is visible to every process.
/// </para>
/// </summary>
/// <param name="matchService">Service that persists pairings.</param>
/// <param name="teamService">Service that owns the teams.</param>
/// <param name="teamStateService">Service that marks a team as queued or joinable.</param>
public sealed class EventMatchmakingService(
    EventMatchService matchService,
    EventTeamService teamService,
    EventTeamStateService teamStateService)
{
    private readonly Lock gate = new();
    private readonly Dictionary<(int Lobby, int MatchType), Queue<int>> waiting = [];
    private readonly Dictionary<int, (int MatchIdentifier, int Opponent)> pairedByTeam = [];

    /// <summary>Reconciles a team after an entry decision or a roster change.</summary>
    /// <param name="teamIdentifier">Team to reconcile.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<MatchmakingResult> ReconcileAsync(
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (teamIdentifier <= 0)
        {
            return new MatchmakingResult(MatchmakingStatus.Unchanged, 0, 0);
        }

        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is null)
        {
            await CancelAsync(teamIdentifier, cancellationToken);
            return new MatchmakingResult(MatchmakingStatus.Unchanged, 0, 0);
        }

        if (team.MatchType != EventConstants.SurvivalSelector)
        {
            // This queue pairs Survival teams. A Tournament team is paired by its
            // bracket's own draw, so queueing it here would hand it an opponent
            // the draw never named — and neither is a team from any other lobby
            // one of these matches.
            return new MatchmakingResult(MatchmakingStatus.Unchanged, 0, 0);
        }

        // A roster that is not fully ready cannot be queued, and one that was
        // queued and has since changed must leave.
        if (!IsReady(team))
        {
            return await CancelAsync(teamIdentifier, cancellationToken);
        }

        // The pairing is durable, so it is read before a new one is made: the
        // in-memory map below is only this process's ordering, and a restart must
        // not queue a team that is already playing.
        var live = await matchService.FindActiveByTeamAsync(teamIdentifier, cancellationToken);
        if (live is not null)
        {
            var liveOpponent = live.FirstTeamIdentifier == teamIdentifier
                ? live.SecondTeamIdentifier
                : live.FirstTeamIdentifier;
            lock (gate)
            {
                pairedByTeam[teamIdentifier] = (live.Identifier, liveOpponent);
            }

            return new MatchmakingResult(MatchmakingStatus.Paired, live.Identifier, liveOpponent);
        }

        int opponentIdentifier;
        lock (gate)
        {
            if (pairedByTeam.TryGetValue(teamIdentifier, out var existing))
            {
                return new MatchmakingResult(
                    MatchmakingStatus.Paired,
                    existing.MatchIdentifier,
                    existing.Opponent);
            }

            if (IsWaitingLocked(teamIdentifier))
            {
                return new MatchmakingResult(MatchmakingStatus.Unchanged, 0, 0);
            }

            var key = (team.LobbyIdentifier, team.MatchType);
            if (!waiting.TryGetValue(key, out var queue))
            {
                queue = new Queue<int>();
                waiting[key] = queue;
            }

            opponentIdentifier = queue.Count > 0 ? queue.Dequeue() : 0;
            queue.Enqueue(teamIdentifier);
        }

        // The team is marked before the pairing is attempted, because a team that
        // finds no opponent is still waiting rather than joinable, and the
        // joinable list is served from another process. It is written outside the
        // lock for the same reason the pairing is: a database round trip is not
        // something to hold a queue lock across.
        await teamStateService.SetAsync(
            teamIdentifier,
            EventTeamRegistrationUtils.QueuedState,
            cancellationToken);

        if (opponentIdentifier == 0)
        {
            return new MatchmakingResult(MatchmakingStatus.Waiting, 0, 0);
        }

        // Pairing is persisted outside the lock, because a database round trip is
        // not something to hold a queue lock across. If the candidate is no longer
        // eligible the team simply stays queued.
        var opponent = await teamService.FindAsync(opponentIdentifier, cancellationToken);
        if (opponent is null
            || !EventTeamRegistrationUtils.IsPairableCandidate(
                opponent.State,
                IsReady(opponent),
                opponent.LobbyIdentifier,
                team.LobbyIdentifier))
        {
            return new MatchmakingResult(MatchmakingStatus.Waiting, 0, 0);
        }

        EventMatch match;
        try
        {
            match = await matchService.CreateAsync(
                team.LobbyIdentifier,
                team.MatchType,
                opponentIdentifier,
                teamIdentifier,
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The live-match guard refused the pairing, which means one of the two
            // teams is already playing something. Nothing is owed to a second
            // match, so this team stays queued.
            return new MatchmakingResult(MatchmakingStatus.Waiting, 0, 0);
        }

        lock (gate)
        {
            RemoveWaitingLocked(teamIdentifier);
            pairedByTeam[teamIdentifier] = (match.Identifier, opponentIdentifier);
            pairedByTeam[opponentIdentifier] = (match.Identifier, teamIdentifier);
        }

        return new MatchmakingResult(MatchmakingStatus.Paired, match.Identifier, opponentIdentifier);
    }

    /// <summary>Cancels a team's registration and safely returns it to the joinable state.</summary>
    /// <param name="teamIdentifier">Team to cancel.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<MatchmakingResult> CancelAsync(
        int teamIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (teamIdentifier <= 0)
        {
            return new MatchmakingResult(MatchmakingStatus.Unchanged, 0, 0);
        }

        int matchIdentifier;
        lock (gate)
        {
            RemoveWaitingLocked(teamIdentifier);

            if (pairedByTeam.Remove(teamIdentifier, out var pairing))
            {
                matchIdentifier = pairing.MatchIdentifier;

                // The opponent is un-paired here but not silently re-queued: the
                // caller reconciles it, so readiness is re-checked with the same
                // rule that queued it the first time.
                pairedByTeam.Remove(pairing.Opponent);
            }
            else
            {
                matchIdentifier = 0;
            }
        }

        // A team that leaves the queue is joinable again, so it stops being hidden
        // from the list the client offers to join. Only a team this queue actually
        // marked is written back, so a team that was never queued is left alone.
        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        var releasedState = team is null ? null : EventTeamRegistrationUtils.ReleasedState(team.State);
        if (releasedState is int state)
        {
            await teamStateService.SetAsync(teamIdentifier, state, cancellationToken);
        }

        if (matchIdentifier > 0)
        {
            await matchService.SetStateAsync(
                matchIdentifier,
                EventConstants.MatchCancelledState,
                cancellationToken: cancellationToken);
            return new MatchmakingResult(MatchmakingStatus.Unchanged, matchIdentifier, 0);
        }

        return new MatchmakingResult(MatchmakingStatus.Unchanged, 0, 0);
    }

    /// <summary>Forgets every pairing and queued team. For tests only.</summary>
    public void Reset()
    {
        lock (gate)
        {
            waiting.Clear();
            pairedByTeam.Clear();
        }
    }

    /// <summary>
    /// A team is ready when it holds at least one occupied slot and every
    /// occupied slot has decided to play.
    /// </summary>
    /// <param name="team">Team to test.</param>
    private static bool IsReady(Persistence.Entities.EventTeam team) =>
        EventTeamRegistrationUtils.IsReady(team.Members);

    private bool IsWaitingLocked(int teamIdentifier) =>
        waiting.Values.Any(queue => queue.Contains(teamIdentifier));

    private bool RemoveWaitingLocked(int teamIdentifier)
    {
        var removed = false;
        foreach (var key in waiting.Keys.ToList())
        {
            var queue = waiting[key];
            if (!queue.Contains(teamIdentifier))
            {
                continue;
            }

            var remaining = new Queue<int>(queue.Where(identifier => identifier != teamIdentifier));
            if (remaining.Count == 0)
            {
                waiting.Remove(key);
            }
            else
            {
                waiting[key] = remaining;
            }

            removed = true;
        }

        return removed;
    }
}
