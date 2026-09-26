using System.Collections.Concurrent;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// The questions the API has asked a lobby and is still waiting for the answer
/// to.
/// <para>
/// Every other message on the coordination stream is a one-way push: the API
/// hands a lobby something to do and the lobby logs what it did. Asking a lobby
/// what it holds in memory needs the other direction, which is why the question
/// carries a correlation and the answer comes back up the request stream. This
/// service is the other end of that: the caller parks a slot here, the gRPC
/// service completes it when the answer arrives, and the caller wakes up.
/// </para>
/// <para>
/// A slot is removed on a timeout as well as on an answer, because a lobby that
/// dropped its stream between the question and the answer would otherwise leave
/// the caller waiting for a reply that is never coming.
/// </para>
/// </summary>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyTeamQueryService(ILogger<LobbyTeamQueryService> logger)
{
    /// <summary>How long a caller waits before it stops waiting.</summary>
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<long, TaskCompletionSource<FakeTeamListing>> pending = new();

    private long nextRequestIdentifier;

    /// <summary>Correlation the next question is sent with.</summary>
    public long NextRequestIdentifier() => Interlocked.Increment(ref nextRequestIdentifier);

    /// <summary>
    /// Parks a slot for one question and reports whether it took. A correlation
    /// is handed out once, so a refusal here would mean the counter wrapped
    /// onto a question that is still parked.
    /// </summary>
    /// <param name="requestIdentifier">Correlation of the question.</param>
    /// <param name="source">Slot the answer completes.</param>
    /// <returns>Whether the slot was parked.</returns>
    public bool Expect(long requestIdentifier, out Task<FakeTeamListing> source)
    {
        var completion = new TaskCompletionSource<FakeTeamListing>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!pending.TryAdd(requestIdentifier, completion))
        {
            source = Task.FromResult(new FakeTeamListing { RequestIdentifier = requestIdentifier });
            return false;
        }

        source = completion.Task;
        return true;
    }

    /// <summary>
    /// Hands an answer to the caller waiting for it, and reports whether one
    /// was waiting. An answer nobody is waiting for is a lobby that answered a
    /// question whose caller had already timed out, which is logged rather than
    /// treated as a failure.
    /// </summary>
    /// <param name="listing">Answer that arrived.</param>
    /// <returns>Whether a caller was waiting for it.</returns>
    public bool Complete(FakeTeamListing listing)
    {
        if (!pending.TryRemove(listing.RequestIdentifier, out var completion))
        {
            logger.LogDebug(
                "A fake team listing for request {RequestIdentifier} arrived with nobody waiting for it",
                listing.RequestIdentifier);
            return false;
        }

        completion.TrySetResult(listing);
        return true;
    }

    /// <summary>
    /// Stops waiting for one answer and reports that the lobby did not reply.
    /// The slot is removed first, so a late answer is recognised as late rather
    /// than completing a caller that has already given up.
    /// </summary>
    /// <param name="requestIdentifier">Correlation of the question.</param>
    public void Abandon(long requestIdentifier)
    {
        if (pending.TryRemove(requestIdentifier, out var completion))
        {
            completion.TrySetCanceled();
        }
    }

    /// <summary>How long a caller waits before it stops waiting.</summary>
    public static TimeSpan Timeout => ReplyTimeout;
}
