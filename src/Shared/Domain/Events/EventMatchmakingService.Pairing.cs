using Mgo2Server.Shared.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The pairing half of <see cref="EventMatchmakingService"/>: the sweep that
/// turns the teams waiting in a lobby's field into matches.
/// <para>
/// Pairing is a sweep rather than work the entry request does, so a team's entry
/// and its opponent are two separate moments. The request records the team as
/// entered and tells it so; nothing pairs until a sweep runs, which is what keeps
/// "looking for an opponent" on screen for a real interval instead of being
/// overwritten by the pairing in the same breath. When the sweep does pair two
/// teams it announces the pairing itself — the client's "opponent found" — and
/// leaves the room to the game-search sweep.
/// </para>
/// <para>
/// Candidates are taken out of the queue before they are validated, so a
/// reconcile that runs while the sweep is reading them cannot enqueue a second
/// copy. A candidate that is no longer eligible is dropped; one that is still
/// eligible goes back to the front of its queue, so a field holding a single
/// ready team keeps it for the next team that enters rather than discarding it.
/// </para>
/// </summary>
public sealed partial class EventMatchmakingService
{
    /// <summary>
    /// Pairs the teams waiting in a lobby's field, two at a time.
    /// <para>
    /// Every pair is made from the head of its own queue — one queue per lobby
    /// and match type — and both candidates are re-read before the match is
    /// written, because the queue is this process's ordering while eligibility is
    /// the database's. The sweep only names the two teams; the host is chosen for
    /// them by the slower game-search sweep.
    /// </para>
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby whose field is paired.</param>
    /// <param name="cancellationToken">Token that cancels the sweep.</param>
    /// <returns>The matches the sweep made.</returns>
    public async Task<List<EventMatch>> TryPairWaitingAsync(
        int lobbyIdentifier,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<EventMatch>();

        foreach (var key in QueueKeys(lobbyIdentifier))
        {
            while (TryReserve(key, out var firstIdentifier, out var secondIdentifier))
            {
                var first = await PairableAsync(firstIdentifier, key.Lobby, cancellationToken);
                var second = await PairableAsync(secondIdentifier, key.Lobby, cancellationToken);

                if (first is not null && second is not null)
                {
                    var match = await CreateMatchAsync(first, second, cancellationToken);
                    if (match is not null)
                    {
                        // "Opponent found" is announced here, when the pairing
                        // happens, and not with the room: the record names the two
                        // teams and no room, so the client gets the pairing screen
                        // on this sweep and the room on the game-search sweep.
                        await assignmentPushService.PushPairingAsync(
                            EventTeamService.BuildSnapshot(first),
                            EventTeamService.BuildSnapshot(second),
                            cancellationToken);
                        matches.Add(match);
                        continue;
                    }

                    // The write was refused, which the live-match guard does when
                    // one of the two is already playing. Both are re-read so the
                    // guard's team is dropped rather than handed back, and this
                    // queue is left to the next sweep, so a refusal cannot spin
                    // this one.
                    Return(
                        firstIdentifier,
                        await PairableAsync(firstIdentifier, key.Lobby, cancellationToken),
                        key);
                    Return(
                        secondIdentifier,
                        await PairableAsync(secondIdentifier, key.Lobby, cancellationToken),
                        key);
                    break;
                }

                Return(firstIdentifier, first, key);
                Return(secondIdentifier, second, key);
            }
        }

        return matches;
    }

    /// <summary>Enqueues a team, creating its queue when this is the first of its kind.</summary>
    /// <param name="lobbyIdentifier">Lobby the team is in.</param>
    /// <param name="matchType">Match type of the team.</param>
    /// <param name="teamIdentifier">Team entering.</param>
    private void EnqueueLocked(int lobbyIdentifier, int matchType, int teamIdentifier)
    {
        var key = (lobbyIdentifier, matchType);
        if (!waiting.TryGetValue(key, out var queue))
        {
            queue = new Queue<int>();
            waiting[key] = queue;
        }

        queue.Enqueue(teamIdentifier);
    }

    /// <summary>Snapshots the queues a lobby's sweep walks, so the walk does not hold the lock.</summary>
    /// <param name="lobbyIdentifier">Lobby to list the queues of.</param>
    /// <returns>The lobby's queue keys.</returns>
    private List<(int Lobby, int MatchType)> QueueKeys(int lobbyIdentifier)
    {
        lock (gate)
        {
            return [.. waiting.Keys.Where(key => key.Lobby == lobbyIdentifier)];
        }
    }

    /// <summary>Takes the next two candidates of a queue and reserves both.</summary>
    /// <param name="key">Queue to take from.</param>
    /// <param name="firstIdentifier">Candidate taken first.</param>
    /// <param name="secondIdentifier">Candidate taken second.</param>
    /// <returns>Whether two candidates were available.</returns>
    private bool TryReserve(
        (int Lobby, int MatchType) key,
        out int firstIdentifier,
        out int secondIdentifier)
    {
        lock (gate)
        {
            if (!waiting.TryGetValue(key, out var queue) || queue.Count < 2)
            {
                firstIdentifier = 0;
                secondIdentifier = 0;
                return false;
            }

            firstIdentifier = queue.Dequeue();
            secondIdentifier = queue.Dequeue();
            pairing.Add(firstIdentifier);
            pairing.Add(secondIdentifier);
            if (queue.Count == 0)
            {
                waiting.Remove(key);
            }

            return true;
        }
    }

    /// <summary>
    /// Reads one reserved candidate and says whether it may still be paired. The
    /// entry state and a ready roster are required, and the team must hold no live
    /// match: a team another process paired while it sat in this queue is already
    /// playing and is dropped rather than paired again.
    /// </summary>
    /// <param name="teamIdentifier">Candidate to read.</param>
    /// <param name="lobbyIdentifier">Lobby the pairing is happening in.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>The team when it may be paired, otherwise <c>null</c>.</returns>
    private async Task<EventTeam?> PairableAsync(
        int teamIdentifier,
        int lobbyIdentifier,
        CancellationToken cancellationToken)
    {
        var team = await teamService.FindAsync(teamIdentifier, cancellationToken);
        if (team is null
            || !EventTeamRegistrationUtils.IsPairableCandidate(
                team.State,
                IsReady(team),
                team.LobbyIdentifier,
                lobbyIdentifier))
        {
            return null;
        }

        return await matchService.FindActiveByTeamAsync(teamIdentifier, cancellationToken) is null
            ? team
            : null;
    }

    /// <summary>Writes the pairing for two validated candidates and records it against both.</summary>
    /// <param name="first">First team.</param>
    /// <param name="second">Second team.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    /// <returns>The match, or <c>null</c> when the live-match guard refused it.</returns>
    private async Task<EventMatch?> CreateMatchAsync(
        EventTeam first,
        EventTeam second,
        CancellationToken cancellationToken)
    {
        EventMatch match;
        try
        {
            match = await matchService.CreateAsync(
                first.LobbyIdentifier,
                first.MatchType,
                first.Identifier,
                second.Identifier,
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The live-match guard refused the pairing, so this is not a match at
            // all: the caller re-reads both teams and drops the one holding it.
            return null;
        }

        lock (gate)
        {
            pairing.Remove(first.Identifier);
            pairing.Remove(second.Identifier);
            pairedByTeam[first.Identifier] = (match.Identifier, second.Identifier);
            pairedByTeam[second.Identifier] = (match.Identifier, first.Identifier);
        }

        return match;
    }

    /// <summary>
    /// Releases a reserved candidate: an eligible one goes back to the front of
    /// its queue, and one that is gone or no longer eligible is dropped.
    /// </summary>
    /// <param name="teamIdentifier">Candidate reserved by the sweep.</param>
    /// <param name="team">The team when it may still be paired, otherwise <c>null</c>.</param>
    /// <param name="key">Queue the candidate came from.</param>
    private void Return(int teamIdentifier, EventTeam? team, (int Lobby, int MatchType) key)
    {
        lock (gate)
        {
            pairing.Remove(teamIdentifier);
            if (team is null)
            {
                return;
            }

            var rebuilt = new Queue<int>();
            rebuilt.Enqueue(teamIdentifier);
            if (waiting.TryGetValue(key, out var queue))
            {
                foreach (var identifier in queue)
                {
                    rebuilt.Enqueue(identifier);
                }
            }

            waiting[key] = rebuilt;
        }
    }
}
