using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim a match holds on a host room that exists only in this process.
/// <para>
/// A real room's claim is an <see cref="EventHostLease"/> row, and it is a row
/// because the lobby that pairs the teams and the gameplay server that hosts the
/// game are separate processes: the room identifier is the serialization point,
/// and the unique index is what makes two lobbies racing for one room a refusal
/// rather than a shared host. An in-memory room has neither process to
/// reconcile with nor a row to point at — a foreign key names a row, and this
/// room is not one — so its claim is held here instead, under the same lock the
/// in-memory rooms themselves are held under.
/// </para>
/// <para>
/// It is the same claim with the same rules, which is the point: the assignment
/// sweep, the lease release and the outcome all ask the same questions of either
/// kind, and a pairing made from the testing tools is announced on exactly the
/// terms a player's own room is. What is lost is durability, deliberately and in
/// keeping with the room itself — a lobby restart forgets the rooms it held, and
/// forgetting a claim on a room that no longer exists loses nothing, because the
/// match it belonged to is found waiting again and re-claimed.
/// </para>
/// </summary>
public sealed class FakeHostClaimService
{
    private readonly Lock gate = new();
    private readonly Dictionary<int, int> matchByRoom = [];
    private readonly Dictionary<int, FakeHostClaim> claimByMatch = [];

    /// <summary>
    /// Claims a room for a match.
    /// </summary>
    /// <param name="matchIdentifier">Match the claim belongs to.</param>
    /// <param name="roomIdentifier">In-memory room being claimed.</param>
    /// <param name="activeStateIdentifier">Active state the client will correlate with.</param>
    /// <param name="sequence">Initial sequence of the active state.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The claim, or null when the room or the match is already claimed.</returns>
    public FakeHostClaim? TryClaimAsync(
        int matchIdentifier,
        int roomIdentifier,
        int activeStateIdentifier,
        int sequence)
    {
        if (matchIdentifier <= 0 || roomIdentifier <= 0)
        {
            throw new ArgumentException("A claim requires both a match and a room.");
        }

        lock (gate)
        {
            // The same two refusals the unique indexes settle for a real room,
            // settled here by the same lock: one room plays one match, and one
            // match plays in one room.
            if (claimByMatch.ContainsKey(matchIdentifier) || matchByRoom.ContainsKey(roomIdentifier))
            {
                return null;
            }

            var claim = new FakeHostClaim
            {
                MatchIdentifier = matchIdentifier,
                RoomIdentifier = roomIdentifier,
                ActiveStateIdentifier = activeStateIdentifier,
                ActiveStateSequence = sequence,
                LeasedAt = DateTimeOffset.UtcNow,
            };

            matchByRoom[roomIdentifier] = matchIdentifier;
            claimByMatch[matchIdentifier] = claim;
            return claim;
        }
    }

    /// <summary>Finds the claim a match holds.</summary>
    /// <param name="matchIdentifier">Match to look for.</param>
    public FakeHostClaim? FindByMatch(int matchIdentifier)
    {
        lock (gate)
        {
            return claimByMatch.TryGetValue(matchIdentifier, out var claim) ? claim : null;
        }
    }

    /// <summary>Finds the claim a room is under.</summary>
    /// <param name="roomIdentifier">Room to look for.</param>
    public FakeHostClaim? FindByRoom(int roomIdentifier)
    {
        lock (gate)
        {
            return matchByRoom.TryGetValue(roomIdentifier, out var matchIdentifier)
                ? claimByMatch.GetValueOrDefault(matchIdentifier)
                : null;
        }
    }

    /// <summary>Returns a room to the pool by releasing its claim.</summary>
    /// <param name="roomIdentifier">Room being released.</param>
    /// <returns>Whether a claim was released.</returns>
    public bool Release(int roomIdentifier)
    {
        lock (gate)
        {
            if (!matchByRoom.Remove(roomIdentifier, out var matchIdentifier))
            {
                return false;
            }

            claimByMatch.Remove(matchIdentifier);
            return true;
        }
    }

    /// <summary>Forgets every claim. For tests only.</summary>
    public void Reset()
    {
        lock (gate)
        {
            matchByRoom.Clear();
            claimByMatch.Clear();
        }
    }
}

/// <summary>
/// One match's claim on an in-memory host room. It carries what the assignment
/// and the outcome need from a lease, and nothing else, so the two kinds of claim
/// can be read the same way.
/// </summary>
public sealed class FakeHostClaim
{
    /// <summary>Match holding the claim.</summary>
    public required int MatchIdentifier { get; init; }

    /// <summary>Room the claim is on, from the fake range.</summary>
    public required int RoomIdentifier { get; init; }

    /// <summary>Active-state identifier the client correlates its cache with.</summary>
    public required int ActiveStateIdentifier { get; init; }

    /// <summary>Sequence the client echoes back on state mutations.</summary>
    public int ActiveStateSequence { get; set; }

    /// <summary>When the claim was taken, which is what the activation time is read from.</summary>
    public DateTimeOffset LeasedAt { get; init; }
}
