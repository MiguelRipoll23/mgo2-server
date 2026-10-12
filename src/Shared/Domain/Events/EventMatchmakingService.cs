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
/// Registration and pairing are two moments. <see cref="ReconcileAsync"/> records
/// a team as entered and tells it so; the pairing itself is
/// <see cref="TryPairWaitingAsync"/>, which the lobby runs on its own scheduled
/// interval. That is what gives an entry and its opponent separate moments rather
/// than pairing a team in the request that entered it.
/// </para>
/// <para>
/// The queue is instance state guarded by one lock. The pairing it produces is
/// written to the database, so the ordering is the only thing that is
/// per-process and the result is visible to every process.
/// </para>
/// </summary>
/// <param name="matchService">Service that persists pairings.</param>
/// <param name="teamService">Service that owns the teams.</param>
/// <param name="teamStateService">Service that marks a team as queued or joinable.</param>
/// <param name="assignmentService">Service that assigns a host to a new pairing.</param>
/// <param name="pushService">Service that tells the team its lifecycle state changed.</param>
/// <param name="assignmentPushService">Service that announces a pairing to both teams.</param>
public sealed partial class EventMatchmakingService(
    EventMatchService matchService,
    EventTeamService teamService,
    EventTeamStateService teamStateService,
    EventAssignmentService assignmentService,
    EventTeamPushService pushService,
    EventAssignmentPushService assignmentPushService)
{
    private readonly Lock gate = new();
    private readonly Dictionary<(int Lobby, int MatchType), Queue<int>> waiting = [];

    // Teams a pairing sweep has taken out of the queue and is validating. They
    // are still this process's to pair, so a reconcile that runs while they are
    // reserved must not enqueue a second copy of them.
    private readonly HashSet<int> pairing = [];

    private readonly Dictionary<int, (int MatchIdentifier, int Opponent)> pairedByTeam = [];

    /// <summary>
    /// Registers a team after an entry decision or a roster change, or removes it
    /// when it is no longer eligible. A team that registers is told it is waiting;
    /// the opponent itself is found by <see cref="TryPairWaitingAsync"/>, not here.
    /// </summary>
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

            EnqueueLocked(team.LobbyIdentifier, team.MatchType, teamIdentifier);
        }

        // Entering is the whole of this call: the team is marked and told it is
        // waiting, and the opponent is found by the pairing sweep rather than
        // here. The push carries the entered state, which is the one state change
        // the entry request makes — the client is moved off its formation screen
        // and onto the field exactly once. The earlier attempt at this push
        // carried the client's auto-formation byte (9) and stranded the team; the
        // entered state is 4, which no client reader except the auto-formation
        // case reads, so the push is safe. It is written and pushed outside the
        // lock because a database round trip is not something to hold a queue
        // lock across.
        if (await teamStateService.SetAsync(
                teamIdentifier,
                EventTeamRegistrationUtils.EnteredState,
                cancellationToken))
        {
            await pushService.PushActiveEventStateAsync(
                EventTeamService.BuildSnapshot(team),
                EventTeamRegistrationUtils.EnteredState,
                cancellationToken);
        }

        return new MatchmakingResult(MatchmakingStatus.Waiting, 0, 0);
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

            if (pairedByTeam.Remove(teamIdentifier, out var livePairing))
            {
                matchIdentifier = livePairing.MatchIdentifier;

                // The opponent is un-paired here but not silently re-queued: the
                // caller reconciles it, so readiness is re-checked with the same
                // rule that queued it the first time.
                pairedByTeam.Remove(livePairing.Opponent);
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

            // Leaving the field is the same kind of change as entering it: the
            // members have to be told the team is joinable again, or they stay
            // on a waiting screen that no longer matches the server. Only the
            // Survival screens read this state vector; a Tournament team's
            // lifecycle belongs to its bracket.
            if (team!.MatchType == EventConstants.SurvivalSelector)
            {
                await pushService.PushActiveEventStateAsync(
                    EventTeamService.BuildSnapshot(team),
                    state,
                    cancellationToken);
            }
        }

        if (matchIdentifier > 0)
        {
            // The match leaving the queue is where its room is handed back. The
            // assignment is what holds the room, so the cancellation goes through
            // it rather than moving the match alone: it releases the claim and
            // tears the live screens down. Cancelling here without releasing
            // would keep the room leased to a match that is over, and the next
            // pairing would be offered a room that refuses its claim.
            await assignmentService.CancelAsync(matchIdentifier, cancellationToken);
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
            pairing.Clear();
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
        pairing.Contains(teamIdentifier)
        || waiting.Values.Any(queue => queue.Contains(teamIdentifier));

    private bool RemoveWaitingLocked(int teamIdentifier)
    {
        var removed = pairing.Remove(teamIdentifier);
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
