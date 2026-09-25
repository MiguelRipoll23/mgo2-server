using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of one row of the joinable-team list the client opens with
/// <c>0x4980</c>. The row is a cut-down view of the active-game snapshot rather
/// than a second model, so it is written from the same projection.
/// </summary>
public static class EventTeamListUtils
{
    /// <summary>Writes one row, asserting its exact size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="snapshot">Team the row describes.</param>
    public static void WriteItem(PacketWriter writer, EventSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var start = writer.Size;
        writer.WriteInt32(snapshot.SnapshotIdentifier);
        writer.WriteFixedString(snapshot.Name, 16);
        writer.WriteUInt8(snapshot.State);
        writer.WriteUInt8(snapshot.FlagBits);
        writer.WriteFixedString(snapshot.HostName, 16);
        writer.WriteInt32(0);
        writer.WriteUInt8(EventConstants.TeamRosterSize);
        writer.WriteUInt8(snapshot.OccupiedParticipantCount());
        writer.WriteUInt8(snapshot.State);
        writer.WriteInt32(snapshot.HostIdentifier);
        writer.WriteUInt8(snapshot.Field0A8);
        writer.WriteUInt8(snapshot.Field0A9);
        writer.WriteInt32(snapshot.Field0AC);
        writer.WritePadding(2);
        writer.WriteInt32(snapshot.LobbyIdentifier);
        writer.WriteInt32(snapshot.EventIdentifier);
        writer.WriteInt32(snapshot.Field2E0);
        writer.WriteUInt16(snapshot.Sequence);

        var written = writer.Size - start;
        if (written != EventConstants.TeamListItemWireSize)
        {
            throw new InvalidOperationException(
                $"A joinable-team row is {written} bytes, expected {EventConstants.TeamListItemWireSize}.");
        }
    }
}
