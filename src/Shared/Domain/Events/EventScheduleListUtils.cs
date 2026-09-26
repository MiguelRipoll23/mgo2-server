using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of one row of the Tournament/Survival browse list the client opens
/// with <c>0x4A40</c> and receives as <c>0x4A42</c>.
/// <para>
/// The row describes an event rather than a team: the identifier the client
/// sends back on <c>0x4A30</c>, the name it lists, the capacity its detail
/// screen shows, and the window it runs in. Fields whose meaning is not
/// established are written as zero, because an invented capacity reads as a
/// correct event with the wrong numbers.
/// </para>
/// </summary>
public static class EventScheduleListUtils
{
    /// <summary>Longest name the row can carry; the field is 64 bytes.</summary>
    public const int MaximumNameLength = 64;

    /// <summary>Writes one row, asserting its exact size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="schedule">Event the row describes.</param>
    public static void WriteItem(PacketWriter writer, EventSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(schedule);

        var capacity = EventScheduleService.TeamCapacityOf(schedule);
        var maximumPlayers = MaximumPlayers(capacity);

        var start = writer.Size;
        writer.WriteInt32(schedule.Identifier);
        // Kind byte the client reads but this server never discriminates. Zero is
        // the neutral value rather than a claim about which kind of event it is.
        writer.WriteUInt8(0);
        writer.WriteFixedString(schedule.Name, MaximumNameLength);

        writer.WriteUInt16((ushort)capacity);
        writer.WriteUInt16((ushort)maximumPlayers);
        writer.WriteUInt16(0);
        writer.WriteUInt16(0);
        writer.WriteUInt16(0);
        writer.WriteUInt16(0);
        writer.WriteUInt16((ushort)maximumPlayers);
        writer.WriteUInt16((ushort)capacity);

        writer.WriteInt32(EpochSecond(schedule.PublishStart));
        writer.WriteInt32(EpochSecond(schedule.PublishEnd));
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        // The lobby the event is played in. The schedule row does not name one,
        // so zero is written rather than a lobby the event might not run in.
        writer.WriteUInt16(0);

        var written = writer.Size - start;
        if (written != EventConstants.EventScheduleListItemWireSize)
        {
            throw new InvalidOperationException(
                $"An event-list row is {written} bytes, expected {EventConstants.EventScheduleListItemWireSize}.");
        }
    }

    private static int MaximumPlayers(int teamCapacity)
    {
        var players = (long)teamCapacity * EventConstants.TeamMemberLimit;
        return (int)Math.Clamp(players, 0, ushort.MaxValue);
    }

    private static int EpochSecond(long value) =>
        (int)Math.Clamp(value, 0, int.MaxValue);
}
