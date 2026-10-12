using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the records the client reads once it has been assigned a game: the
/// room's detail, the confirmation it sends back while it joins, and the member
/// information the assignment screen draws.
/// <para>
/// Split out of <see cref="EventAssignmentUtils"/> so neither file exceeds the
/// repository's size limit. The pairing records stay there — they are written to
/// the whole team, while these three are the assigned game's own screen — and
/// each writer here asserts its exact size for the same reason the others do: the
/// client reads them off one receive buffer, so a short write is not a truncated
/// display but the next record being read from the previous one's bytes.
/// </para>
/// </summary>
public static class EventAssignedGameUtils
{
    /// <summary>
    /// Writes the assigned game detail. The client opens this from an assigned
    /// entry, so it carries the room name, its lobby and match type, and the
    /// environment the game will run with.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="eventIdentifier">Event the game belongs to; never zero.</param>
    /// <param name="lobbyIdentifier">Lobby the room belongs to.</param>
    /// <param name="matchType">Match type of the room.</param>
    /// <param name="activationTimeSeconds">Absolute time the event activates at.</param>
    /// <param name="gameName">Name of the room.</param>
    /// <param name="hostEnvironment">Environment the game runs with.</param>
    public static void WriteAssignedGameDetail(
        PacketWriter writer,
        int eventIdentifier,
        int lobbyIdentifier,
        int matchType,
        int activationTimeSeconds,
        string gameName,
        EventHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        if (eventIdentifier == 0)
        {
            throw new ArgumentException(
                "An assigned game detail requires a nonzero event identifier.",
                nameof(eventIdentifier));
        }

        var start = writer.Size;
        writer.WriteInt32(0);
        writer.WriteInt32(eventIdentifier);

        // A separate tournament UI state precedes the roster size; zero is the
        // neutral value for a Survival assignment.
        writer.WriteUInt8(0);
        writer.WriteUInt16(EventConstants.SnapshotParticipantCount);
        for (var index = 0; index < EventConstants.SnapshotParticipantCount; index++)
        {
            writer.WriteUInt16(0);
        }

        writer.WriteInt32(activationTimeSeconds);
        writer.WriteInt32(lobbyIdentifier);
        writer.WriteUInt8(matchType);
        writer.WriteUInt8(0);
        writer.WriteFixedString(gameName ?? string.Empty, 64);
        writer.WritePadding(32);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        EventHostEnvironmentUtils.Write(writer, hostEnvironment);
        writer.WritePadding(62);

        AssertSize(writer, start, EventConstants.AssignedGameDetailWireSize, "assigned game detail");
    }

    /// <summary>Writes the successful assignment-confirmation reply.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="recipientCharacterIdentifier">Character confirming the assignment.</param>
    /// <param name="team">Team that character belongs to.</param>
    public static void WriteConfirmationResponse(
        PacketWriter writer,
        int recipientCharacterIdentifier,
        EventSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(team);
        if (team.IndexOfParticipant(recipientCharacterIdentifier) < 0)
        {
            throw new InvalidOperationException(
                "The confirmation reply requires a character in the confirmed team.");
        }

        var start = writer.Size;
        writer.WriteInt32(0);
        writer.WriteInt32(recipientCharacterIdentifier);
        writer.WriteInt32(Math.Max(0, team.PaidReward));
        writer.WriteInt32(Math.Max(0, team.ConsecutiveWins));

        // The three remaining cache values have no established producer in
        // Survival, so they stay neutral rather than carrying invented ones.
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);

        AssertSize(writer, start, EventConstants.ConfirmationResponseWireSize, "confirmation response");
    }

    /// <summary>Writes the assigned-member-information reply: one record per roster slot.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="team">Team the member information belongs to.</param>
    public static void WriteAssignedMemberInformation(PacketWriter writer, EventSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(team);

        var start = writer.Size;
        writer.WriteInt32(0);
        foreach (var participant in team.Participants)
        {
            writer.WriteInt32(participant.CharacterIdentifier);
            writer.WriteInt32(participant.CharacterIdentifier == 0 ? 0 : participant.Experience);
            writer.WriteFixedString(
                participant.CharacterIdentifier == 0 ? string.Empty : participant.Name,
                16);
        }

        AssertSize(
            writer,
            start,
            EventConstants.AssignedMemberInformationWireSize,
            "assigned member information");
    }

    private static void AssertSize(PacketWriter writer, int start, int expected, string name)
    {
        var written = writer.Size - start;
        if (written != expected)
        {
            throw new InvalidOperationException(
                $"The {name} record is {written} bytes, expected {expected}.");
        }
    }
}
