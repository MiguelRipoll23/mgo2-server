using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the team block: the fixed-width name-and-members shape that several
/// assignment records share, so they describe a team the same way whatever
/// record carries it.
/// <para>
/// A block always writes the full eight members, padding the empty slots with
/// zero rather than shortening the record. The client reads the full width
/// regardless of how many members there are.
/// </para>
/// </summary>
public static class EventTeamBlockUtils
{
    /// <summary>Writes the member list of a live team snapshot, in slot order.</summary>
    public static void WriteParticipantIdentifiers(PacketWriter writer, EventSnapshot team)
    {
        WriteTeamMembers(writer, Participants(team));
    }

    /// <summary>
    /// Writes one team's identity, its padded name, and its member list.
    /// </summary>
    public static void WriteTeamBlock(
        PacketWriter writer,
        int teamIdentity,
        string name,
        IReadOnlyList<int> members)
    {
        writer.WriteInt32(teamIdentity);
        writer.WriteFixedString(name, 16);
        WriteTeamMembers(writer, members);
    }

    /// <summary>Writes a member list as the fixed eight identifiers a team block carries.</summary>
    public static void WriteTeamMembers(PacketWriter writer, IReadOnlyList<int> members)
    {
        for (var slot = 0; slot < EventConstants.SnapshotParticipantCount; slot++)
        {
            writer.WriteInt32(slot < members.Count ? members[slot] : 0);
        }
    }

    /// <summary>Reads the members of a live team snapshot, in slot order.</summary>
    public static IReadOnlyList<int> Participants(EventSnapshot team) =>
        [.. team.Participants.Select(participant => participant.CharacterIdentifier)];
}
