using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Codec of the assignment packets: the match-found notification, the state
/// update that carries an outcome, the two event-game caches the assigned game
/// consumes, and the next-match card. The records the client reads once it holds
/// an assigned game — the room detail, the confirmation reply and the member
/// information — are <see cref="EventAssignedGameUtils"/>'.
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
    /// <param name="teamIdentifier">
    /// Identity of the recipient's own team record. The client checks this word
    /// against its open team record and discards the record on a mismatch, so an
    /// assignment's own selector here loses the notification.
    /// </param>
    /// <param name="teamSerial">Serial of that same record, checked with it.</param>
    /// <param name="ownTeam">Recipient's team.</param>
    /// <param name="opponentTeam">Opposing team.</param>
    /// <param name="teamState">
    /// Participation state this record carries. It is the caller's, never the
    /// team row's: echoing the row is the one thing `BUILD_1_36.md` forbids
    /// here, because a row that holds the auto-formation byte strands the pair
    /// on the formation screen with strings 716/717. See
    /// <see cref="EventConstants.TeamAutoFormationState"/>.
    /// </param>
    public static void WriteMatchFound(
        PacketWriter writer,
        int teamIdentifier,
        int teamSerial,
        EventSnapshot ownTeam,
        EventSnapshot opponentTeam,
        int teamState)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(ownTeam);
        ArgumentNullException.ThrowIfNull(opponentTeam);

        // The two identities inside the body are TEAM record ids, not character
        // ids. The client reads the first of them to decide which of the two
        // cached names is the opponent: `0x8CDFA8`, the renderer of lobby string
        // 761 ("Your next opponent has been determined... Next match: vs. %s"),
        // reads rec+0x14, compares it against the player's own team id at
        // team+0x00, and copies the OTHER block's name — rec+0x50 when it is you,
        // rec+0x18 when it is not.
        //
        // A character id there is never equal to the player's own team id, so the
        // client concludes neither block is its own and prints the first block —
        // the player's own team — as the opponent: "X vs X", on both sides.
        //
        // `0x4A13`'s team_block names these slots `team_id`/`team_name` and says
        // the comparison is "only meaningful if this is a team id"; `0x4E11`'s
        // `entrant_id` is compared against the same team+0x00 ("a team id being
        // compared to a team id"). The header's own identity is already the team
        // record id, so the first block quotes it verbatim.
        var ownTeamIdentity = teamIdentifier;
        var opponentTeamIdentity = opponentTeam.SnapshotIdentifier;
        if (ownTeamIdentity == 0 || opponentTeamIdentity == 0
            || ownTeamIdentity == opponentTeamIdentity)
        {
            throw new InvalidOperationException(
                "A match-found notification requires two distinct team identities.");
        }

        var start = writer.Size;
        writer.WriteInt32(teamIdentifier);
        writer.WriteUInt16(teamSerial);
        writer.WriteInt32(ownTeam.EventIdentifier);

        // The byte here is the team's participation state, and the value written
        // is the caller's: the pairing announcement carries the entered phase and
        // the assignment burst's last word carries the assigned one. Neither is
        // the byte the team row happens to hold — a waiting team is stored in the
        // entered state, but the row is data and `BUILD_1_36.md` is explicit that
        // no packet may echo it into this slot, because a row holding the
        // auto-formation byte strands the pair on the formation screen. See
        // `EventConstants.TeamAutoFormationState` for the one value that must not
        // be written.
        writer.WriteUInt8(teamState);
        writer.WriteInt32(ownTeamIdentity);
        writer.WriteFixedString(ownTeam.Name, 16);
        writer.WriteUInt8(Clamped(ownTeam.ConsecutiveWins));
        writer.WriteInt32(opponentTeamIdentity);
        writer.WriteFixedString(opponentTeam.Name, 16);
        writer.WriteUInt8(Clamped(opponentTeam.ConsecutiveWins));

        AssertSize(writer, start, EventConstants.MatchFoundWireSize, "match found");
    }

    /// <summary>
    /// Writes the next-match card. It is the wider sibling of the match-found
    /// notification: the same two identities and names, each padded out to a
    /// full team block, and the lobby triple that lets the client route the
    /// record to the Survival or Tournament screen family.
    /// <para>
    /// The card identity must echo the active-event start's identity, because
    /// the client drops a card whose identifier does not match the one it has
    /// been holding open.
    /// </para>
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="cardIdentifier">Entry word the active-event record was opened with; never zero.</param>
    /// <param name="ownTeam">Recipient's team.</param>
    /// <param name="opponentTeam">Opposing team.</param>
    /// <param name="ownRoster">Frozen roster of the recipient's team, when the mode has one.</param>
    /// <param name="opponentRoster">Frozen roster of the opposing team, when the mode has one.</param>
    public static void WriteNextMatchCard(
        PacketWriter writer,
        int cardIdentifier,
        EventSnapshot ownTeam,
        EventSnapshot opponentTeam,
        EventRoster? ownRoster = null,
        EventRoster? opponentRoster = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(ownTeam);
        ArgumentNullException.ThrowIfNull(opponentTeam);
        if (cardIdentifier == 0)
        {
            throw new ArgumentException(
                "A next-match card requires the identity the event opened with.",
                nameof(cardIdentifier));
        }

        // The first identity is a TEAM record id, exactly as in the match-found
        // notification: both fill the same shared record and the client picks
        // the block that is not its own to render.
        var ownTeamIdentity = ownTeam.SnapshotIdentifier;
        var opponentTeamIdentity = opponentTeam.SnapshotIdentifier;
        var bothIdentitiesAreKnown = ownTeamIdentity != 0 && opponentTeamIdentity != 0;
        var identitiesAreDistinct = ownTeamIdentity != opponentTeamIdentity;

        if (!bothIdentitiesAreKnown || !identitiesAreDistinct)
        {
            throw new InvalidOperationException(
                "A next-match card requires two distinct team identities.");
        }

        // A Tournament card names the roster the draw was made with, not the
        // live team: the pairings are settled, and a card naming whoever has
        // joined or left since would describe a team the bracket never matched.
        // A Survival match has no frozen roster and is written from the snapshot
        // it was paired from, which is the only record of it there is.
        var ownBlock = ownRoster is { } frozen
            ? (Name: frozen.Name, Members: frozen.MemberIdentifiers)
            : (Name: ownTeam.Name, Members: EventTeamBlockUtils.Participants(ownTeam));
        var opponentBlock = opponentRoster is { } frozenOpponent
            ? (Name: frozenOpponent.Name, Members: frozenOpponent.MemberIdentifiers)
            : (Name: opponentTeam.Name, Members: EventTeamBlockUtils.Participants(opponentTeam));

        var start = writer.Size;
        writer.WriteInt32(cardIdentifier);
        writer.WriteInt32(ownTeam.LobbyIdentifier);
        writer.WriteUInt8(ownTeam.MatchType);
        writer.WriteUInt8(ownTeam.PrimaryEquipmentType);
        writer.WriteInt32(Clamped(ownTeam.ConsecutiveWins));
        writer.WriteInt32(Clamped(opponentTeam.ConsecutiveWins));
        EventTeamBlockUtils.WriteTeamBlock(writer, ownTeamIdentity, ownBlock.Name, ownBlock.Members);
        EventTeamBlockUtils.WriteTeamBlock(writer, opponentTeamIdentity, opponentBlock.Name, opponentBlock.Members);
        writer.WriteUInt8(EventConstants.EventGameRotationIndex);

        AssertSize(writer, start, EventConstants.NextMatchCardWireSize, "next match card");
    }

    /// <summary>
    /// Writes the state-update notification. It is the body of both outcomes:
    /// the winner's continue view and the loser's return to team creation differ
    /// only in the command they are sent under.
    /// </summary>
    /// <param name="writer">Writer to append to.</param>
    /// <param name="teamIdentifier">Identity of the recipient's own team record.</param>
    /// <param name="teamSerial">Serial of that same team record.</param>
    /// <param name="team">Team the recipient is in.</param>
    public static void WriteStateUpdate(
        PacketWriter writer,
        int teamIdentifier,
        int teamSerial,
        EventSnapshot team)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(team);

        var start = writer.Size;
        writer.WriteInt32(teamIdentifier);
        writer.WriteUInt16(teamSerial);
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
        EventTeamBlockUtils.WriteParticipantIdentifiers(writer, ownTeam);
        EventTeamBlockUtils.WriteParticipantIdentifiers(writer, opponentTeam);

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
