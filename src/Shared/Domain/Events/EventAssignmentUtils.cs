using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the assignment packets: the match-found notification, the state
/// update that carries an outcome, the two event-game caches the assigned game
/// consumes, and the two replies the client sends back while it confirms.
/// <para>
/// Each writer asserts its exact size. These records are parsed off the receive
/// buffer by the client, so a short write is not a truncated display — it is the
/// next record being read from the previous one's bytes.
/// </para>
/// </summary>
public static class EventAssignmentUtils
{
    /// <summary>Writes the match-found notification.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the assignment established.</param>
    /// <param name="sequence">Sequence the recipient's snapshot carries.</param>
    /// <param name="ownTeam">Recipient's team.</param>
    /// <param name="opponentTeam">Opposing team.</param>
    /// <param name="recipientCharacterIdentifier">Character the record is written for.</param>
    public static void WriteMatchFound(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        EventSnapshot ownTeam,
        EventSnapshot opponentTeam,
        int recipientCharacterIdentifier)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(ownTeam);
        ArgumentNullException.ThrowIfNull(opponentTeam);

        // The first identity is recipient-relative: the client compares it with
        // the local character to decide which of the two cached names is the
        // opponent, so it must be the character even for a non-leader member.
        var ownTeamIdentity = recipientCharacterIdentifier;
        var opponentTeamIdentity = opponentTeam.Participants[0].CharacterIdentifier;
        if (ownTeamIdentity == 0 || opponentTeamIdentity == 0
            || ownTeamIdentity == opponentTeamIdentity)
        {
            throw new InvalidOperationException(
                "A match-found notification requires two distinct team identities.");
        }

        var start = writer.Size;
        writer.WriteInt32(activeStateIdentifier);
        writer.WriteUInt16(sequence);
        writer.WriteInt32(ownTeam.EventIdentifier);
        writer.WriteUInt8(EventConstants.ActiveEventAssignedState);
        writer.WriteInt32(ownTeamIdentity);
        writer.WriteFixedString(ownTeam.Name, 16);
        writer.WriteUInt8(Clamped(ownTeam.ConsecutiveWins));
        writer.WriteInt32(opponentTeamIdentity);
        writer.WriteFixedString(opponentTeam.Name, 16);
        writer.WriteUInt8(Clamped(opponentTeam.ConsecutiveWins));

        AssertSize(writer, start, EventConstants.MatchFoundWireSize, "match found");
    }

    /// <summary>
    /// Writes the state-update notification. It is the body of both outcomes:
    /// the winner's continue view and the loser's return to team creation differ
    /// only in the command they are sent under.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="activeStateIdentifier">Active state the recipient holds.</param>
    /// <param name="sequence">Sequence the recipient's snapshot carries.</param>
    /// <param name="team">Team the recipient is in.</param>
    public static void WriteStateUpdate(
        PacketWriter writer,
        int activeStateIdentifier,
        int sequence,
        EventSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(team);

        var start = writer.Size;
        writer.WriteInt32(activeStateIdentifier);
        writer.WriteUInt16(sequence);
        writer.WriteUInt8(team.State);
        foreach (var participant in team.Participants)
        {
            var state = participant.CharacterIdentifier == 0
                ? 0
                : participant.State;
            writer.WriteUInt8(state);
        }

        AssertSize(writer, start, EventConstants.StateUpdateWireSize, "state update");
    }

    /// <summary>
    /// Writes the event-game cache. The client clears the whole cache before
    /// reading this common two-team prefix, so it has to arrive before the
    /// name-bearing notification rather than after it.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="ownTeam">Recipient's team.</param>
    /// <param name="opponentTeam">Opposing team.</param>
    public static void WriteEventGameInitialize(
        PacketWriter writer,
        EventSnapshot ownTeam,
        EventSnapshot opponentTeam)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(ownTeam);
        ArgumentNullException.ThrowIfNull(opponentTeam);
        ValidateSameMatch("event game initialize", ownTeam, opponentTeam);

        var start = writer.Size;
        writer.WriteInt32(ownTeam.LobbyIdentifier);
        writer.WriteUInt8(ownTeam.MatchType);
        writer.WriteUInt8(ownTeam.PrimaryEquipmentType);
        writer.WriteInt32(NonNegative(ownTeam.ConsecutiveWins));
        writer.WriteInt32(NonNegative(opponentTeam.ConsecutiveWins));
        WriteParticipantIdentifiers(writer, ownTeam);
        WriteParticipantIdentifiers(writer, opponentTeam);

        AssertSize(writer, start, EventConstants.EventGameInitializeWireSize, "event game initialize");
    }

    /// <summary>Writes the host's event-game cache, which also installs the environment.</summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="hostCharacterIdentifier">Character hosting the assigned game; never zero.</param>
    /// <param name="ownTeam">Recipient's team.</param>
    /// <param name="opponentTeam">Opposing team.</param>
    /// <param name="hostEnvironment">Environment the assigned game starts from.</param>
    public static void WriteEventGameHostInitialize(
        PacketWriter writer,
        int hostCharacterIdentifier,
        EventSnapshot ownTeam,
        EventSnapshot opponentTeam,
        EventHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(ownTeam);
        ArgumentNullException.ThrowIfNull(opponentTeam);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        if (hostCharacterIdentifier == 0)
        {
            throw new ArgumentException(
                "The host cache requires a dedicated host identity.",
                nameof(hostCharacterIdentifier));
        }

        ValidateSameMatch("event game host initialize", ownTeam, opponentTeam);

        var start = writer.Size;
        writer.WriteInt32(hostCharacterIdentifier);
        writer.WriteInt32(ownTeam.LobbyIdentifier);
        writer.WriteUInt8(ownTeam.MatchType);
        writer.WriteUInt8(ownTeam.PrimaryEquipmentType);
        writer.WriteInt32(NonNegative(ownTeam.ConsecutiveWins));
        writer.WriteInt32(NonNegative(opponentTeam.ConsecutiveWins));
        writer.WriteUInt8(EventConstants.EventGameRotationIndex);
        EventHostEnvironmentUtils.Write(writer, hostEnvironment);
        writer.WriteUInt8(0);
        writer.WriteUInt16(0);
        writer.WriteUInt8(0);

        AssertSize(
            writer,
            start,
            EventConstants.EventGameHostInitializeWireSize,
            "event game host initialize");
    }

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

    private static void WriteParticipantIdentifiers(PacketWriter writer, EventSnapshot team)
    {
        foreach (var participant in team.Participants)
        {
            writer.WriteInt32(participant.CharacterIdentifier);
        }
    }

    private static void ValidateSameMatch(
        string command,
        EventSnapshot ownTeam,
        EventSnapshot opponentTeam)
    {
        if (ownTeam.LobbyIdentifier != opponentTeam.LobbyIdentifier
            || ownTeam.MatchType != opponentTeam.MatchType)
        {
            throw new InvalidOperationException(
                $"The {command} cache requires two teams from the same lobby and mode.");
        }
    }

    private static int Clamped(int value) => Math.Clamp(value, 0, 255);

    private static int NonNegative(int value) => Math.Max(0, value);

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
