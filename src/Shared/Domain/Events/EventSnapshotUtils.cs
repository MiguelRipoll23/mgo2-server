using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the active-game snapshot. It writes the compact form the team
/// replies use and the full form that adds the host environment, asserting the
/// exact size of each so a layout regression fails here rather than reaching a
/// client that parses off whatever its receive buffer held.
/// </summary>
public static class EventSnapshotUtils
{
    /// <summary>Writes the compact snapshot, asserting its size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="snapshot">Snapshot to write.</param>
    public static void WriteCompact(PacketWriter writer, EventSnapshot snapshot) =>
        Write(writer, snapshot, includeHostEnvironment: false, snapshot.SnapshotIdentifier);

    /// <summary>Writes the compact snapshot with a substituted correlation identifier.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="snapshot">Snapshot to write.</param>
    /// <param name="responseIdentifier">Identifier to write in place of the team's own.</param>
    public static void WriteCompact(PacketWriter writer, EventSnapshot snapshot, int responseIdentifier) =>
        Write(writer, snapshot, includeHostEnvironment: false, responseIdentifier);

    /// <summary>Writes the full snapshot, including the host environment.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="snapshot">Snapshot to write.</param>
    public static void WriteFull(PacketWriter writer, EventSnapshot snapshot) =>
        Write(writer, snapshot, includeHostEnvironment: true, snapshot.SnapshotIdentifier);

    private static void Write(
        PacketWriter writer,
        EventSnapshot snapshot,
        bool includeHostEnvironment,
        int responseIdentifier)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var start = writer.Size;
        writer.WriteInt32(snapshot.Result);
        writer.WriteInt32(responseIdentifier);
        writer.WriteUInt16(snapshot.Sequence);
        writer.WriteUInt8(snapshot.State);
        writer.WriteFixedString(snapshot.Name, 16);
        writer.WriteFixedString(snapshot.Comment, 128);
        writer.WriteUInt8(snapshot.FlagBits);

        if (includeHostEnvironment)
        {
            EventHostEnvironmentUtils.Write(writer, snapshot.HostEnvironment);
        }

        foreach (var participant in snapshot.Participants)
        {
            writer.WriteInt32(participant.CharacterIdentifier);
            writer.WriteFixedString(participant.Name, 16);
            writer.WriteUInt8(participant.State);
            writer.WriteInt32(participant.Experience);
        }

        writer.WriteInt32(snapshot.LobbyIdentifier);
        writer.WriteUInt8(snapshot.MatchType);
        writer.WriteUInt8(snapshot.PrimaryEquipmentType);
        writer.WriteUInt16(snapshot.Field296);
        writer.WriteUInt16(snapshot.Field298);
        writer.WriteInt32(snapshot.HostIdentifier);
        writer.WriteFixedString(snapshot.HostName, 16);
        writer.WriteInt32(snapshot.Field2B4);
        writer.WriteFixedString(snapshot.SecondaryName, 16);
        writer.WriteUInt16(snapshot.Field2CA);
        writer.WriteInt32(snapshot.EventIdentifier);
        writer.WriteInt32(snapshot.Field2D4);
        writer.WriteInt32(snapshot.Field2D8);
        writer.WriteUInt8(snapshot.Field0A8);
        writer.WriteUInt8(snapshot.Field0A9);
        writer.WriteInt32(snapshot.Field0AC);
        writer.WriteInt32(snapshot.Field2E0);
        writer.WriteUInt16(snapshot.Field2E8);
        writer.WriteBytes(snapshot.ParticipantStates);
        writer.WriteUInt8(snapshot.Field2EA);

        var written = writer.Size - start;
        var expected = includeHostEnvironment
            ? EventConstants.SnapshotWireSize
            : EventConstants.CompactSnapshotWireSize;
        if (written != expected)
        {
            throw new InvalidOperationException(
                $"The active-game snapshot is {written} bytes, expected {expected}.");
        }
    }
}
