using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Builds the active-event records from an assignment. It exists so the two
/// places that need them — the snapshot reply and the assigned-game detail —
/// derive the participant states and the base time the same way, and so the
/// fields whose meaning is not established are neutral in exactly one place.
/// <para>
/// Those fields are passed as zero on purpose. The record has a global state,
/// a flag byte and a detail byte whose meanings this server has not recovered,
/// and writing an invented value would be indistinguishable from a correct one
/// at the client.
/// </para>
/// </summary>
public sealed class EventActiveEventService
{
    /// <summary>Extracts the eight participant states of a team.</summary>
    /// <param name="team">Team to read.</param>
    public static byte[] BuildParticipantStates(EventSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(team);

        var states = new byte[EventConstants.SnapshotParticipantCount];
        for (var index = 0; index < states.Length; index++)
        {
            var participant = team.Participants[index];

            // An empty slot carries a neutral zero rather than the state a
            // departed member left behind, because the start record's guard
            // refuses a nonzero state on an empty slot.
            states[index] = participant.CharacterIdentifier == 0
                ? (byte)0
                : (byte)participant.State;
        }

        return states;
    }

    /// <summary>Returns the absolute base time an assignment advertises.</summary>
    /// <param name="lease">Lease the assignment was taken under.</param>
    public static int BaseTimeSeconds(EventHostLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        return (int)lease.LeasedAt.ToUnixTimeSeconds();
    }
}
