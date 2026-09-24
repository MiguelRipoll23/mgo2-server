using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codecs of the Survival battle list. The stream is a start marker, two rows
/// per battle, and an end marker; the rows and the start marker are fixed size,
/// so each is asserted. The start marker repeats the event settings and rewards
/// the information screen showed, because the client copies them into the same
/// overview storage when the team screen opens.
/// </summary>
public static class EventBattleListUtils
{
    /// <summary>Writes the start marker, asserting its size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="eventIdentifier">Event correlation identifier.</param>
    /// <param name="environment">Host environment to advertise.</param>
    /// <param name="rewards">Ten consecutive-win rewards.</param>
    /// <param name="participationReward">Participation reward.</param>
    public static void WriteStart(
        PacketWriter writer,
        int eventIdentifier,
        EventHostEnvironment environment,
        IReadOnlyList<int> rewards,
        int participationReward)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(rewards);

        var start = writer.Size;
        writer.WriteInt32(eventIdentifier);
        writer.WriteUInt16(0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        EventHostEnvironmentUtils.Write(writer, environment);
        writer.WriteInt32(0);

        for (var index = 0; index < EventOptions.WinRewardSlots; index++)
        {
            writer.WriteInt32(index < rewards.Count ? rewards[index] : 0);
        }

        writer.WriteInt32(participationReward);
        writer.WritePadding(10);

        var written = writer.Size - start;
        if (written != EventConstants.BattleListStartWireSize)
        {
            throw new InvalidOperationException(
                $"The battle-list start marker is {written} bytes, expected {EventConstants.BattleListStartWireSize}.");
        }
    }

    /// <summary>Writes one team row, asserting its size.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="rowIndex">Index of the row in the stream.</param>
    /// <param name="team">Team the row describes.</param>
    /// <param name="averageExperience">Average experience the overview converts to a level.</param>
    public static void WriteItem(
        PacketWriter writer,
        int rowIndex,
        EventSnapshot team,
        int averageExperience)
    {
        ArgumentNullException.ThrowIfNull(team);

        var start = writer.Size;
        var leader = team.Participants[0];

        writer.WriteUInt16(rowIndex);
        writer.WriteInt32(team.SnapshotIdentifier);
        writer.WriteFixedString(team.Name, 16);
        // Wire +0x16 is an opaque per-row event state. Its nonzero mapping is not
        // recovered, and the client clears it to zero, so zero is the proven value.
        writer.WritePadding(3);
        writer.WriteFixedString(leader.Name, 16);
        // Wire +0x2A is the numeric member count the list UI consumes.
        writer.WriteUInt8(0);
        writer.WriteUInt8(Math.Min(0xff, team.OccupiedParticipantCount()));
        writer.WriteUInt8(0);
        writer.WriteInt32(averageExperience);
        writer.WriteUInt8(Math.Clamp(team.ConsecutiveWins, 0, 0xff));
        writer.WriteInt32(Math.Max(0, team.PaidReward));

        var written = writer.Size - start;
        if (written != EventConstants.BattleListItemWireSize)
        {
            throw new InvalidOperationException(
                $"A battle-list row is {written} bytes, expected {EventConstants.BattleListItemWireSize}.");
        }
    }

    /// <summary>Averages the experience of the occupied roster slots.</summary>
    /// <param name="team">Team to measure.</param>
    /// <returns>The average, or zero when no slot is occupied.</returns>
    public static int AverageParticipantExperience(EventSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(team);

        long total = 0;
        var count = 0;
        foreach (var participant in team.Participants)
        {
            if (participant.CharacterIdentifier != 0)
            {
                total += Math.Max(0, participant.Experience);
                count++;
            }
        }

        return count == 0 ? 0 : (int)Math.Min(int.MaxValue, total / count);
    }
}
