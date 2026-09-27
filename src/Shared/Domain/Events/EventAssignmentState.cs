using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The claim a match holds on its room.
/// <para>
/// A lease row answers the questions the assignment and the outcome ask, and
/// this is what it is read as. It is deliberately the small part of a lease: the
/// active state the client correlates with, the sequence it echoes back, when
/// the claim was taken, and the room it is on.
/// </para>
/// </summary>
/// <param name="ActiveStateIdentifier">Active state the client correlates its cache with.</param>
/// <param name="ActiveStateSequence">Sequence the client echoes back on state mutations.</param>
/// <param name="LeasedAt">When the claim was taken, which the activation time is read from.</param>
/// <param name="GameIdentifier">Room the claim is on.</param>
public readonly record struct EventAssignmentState(
    int ActiveStateIdentifier,
    int ActiveStateSequence,
    DateTimeOffset LeasedAt,
    int GameIdentifier)
{
    /// <summary>
    /// The activation time the client is given, which is when the claim was
    /// taken. It is read from the claim rather than from the clock so that a
    /// replayed read reports the same instant the assignment did.
    /// </summary>
    public int BaseTimeSeconds() => (int)LeasedAt.ToUnixTimeSeconds();

    /// <summary>Reads a lease as the claim it represents.</summary>
    /// <param name="lease">Lease to read.</param>
    public static EventAssignmentState From(EventHostLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        return new EventAssignmentState(
            lease.ActiveStateIdentifier,
            lease.ActiveStateSequence,
            lease.LeasedAt,
            lease.GameIdentifier);
    }
}
