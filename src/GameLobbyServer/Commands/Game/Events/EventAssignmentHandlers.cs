using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Answers the snapshot selector the client sends when it opens an assigned
/// game. The reply is the full active-game snapshot, host environment included,
/// because the client rebuilds its event cache from it.
/// </summary>
public sealed class GetAssignedGameSnapshotHandler(
    EventAssignmentService assignmentService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier
            || packet.Payload.Length != EventConstants.SnapshotSelectorRequestWireSize)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var selector = new PacketReader(packet.Payload).ReadInt32();
        var assignment = await EventAssignmentSupport.FindForSessionAsync(
            session,
            characterIdentifier,
            assignmentService,
            cancellationToken);
        var team = assignment?.TeamOfCharacter(characterIdentifier);
        if (assignment is null || team is null || selector != assignment.ActiveStateIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteFull(writer, team);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetActiveGameSnapshotResult,
            writer.Build(),
            cancellationToken);

        // The snapshot reply clears the client's event caches, so the live
        // event state is restored immediately after it rather than on the next
        // screen the player opens.
        var activeEventWriter = new PacketWriter();
        EventActiveEventUtils.WriteActiveEventState(
            activeEventWriter,
            assignment.ActiveStateIdentifier,
            assignment.Sequence,
            team.EventIdentifier,
            EventConstants.ActiveEventAssignedState,
            EventActiveEventService.BuildParticipantStates(team),
            team.HostEnvironment,
            assignment.ActivationTimeSeconds,
            flagByte: 0,
            detailByte: 0);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.ActiveEventStart,
            activeEventWriter.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetActiveGameSnapshotResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}

/// <summary>
/// Confirms the assignment the client selected. A confirmation is only accepted
/// while the lease it names is still active and still holds the confirming
/// character, so a confirmation that lost a race is answered as a mismatch
/// rather than silently replacing a newer assignment.
/// </summary>
public sealed class ConfirmEventAssignmentHandler(
    EventAssignmentService assignmentService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier
            || packet.Payload.Length != EventConstants.ConfirmationRequestWireSize)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var assignment = await EventAssignmentSupport.FindForSessionAsync(
            session,
            characterIdentifier,
            assignmentService,
            cancellationToken);
        var team = assignment?.TeamOfCharacter(characterIdentifier);
        if (assignment is null || team is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteConfirmationResponse(writer, characterIdentifier, team);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.ConfirmActiveGameAssignmentResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.ConfirmActiveGameAssignmentResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}

/// <summary>
/// Removes the caller from an assigned game, or releases the Tournament place
/// it holds. One command carries both because the client sends the same request
/// for either, and the reservation is tried first so a pending place is released
/// before a game entry is looked for.
/// </summary>
public sealed class RemoveEventGameEntryHandler(
    EventAssignmentService assignmentService,
    TournamentRegistrationService registrationService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier
            || packet.Payload.Length != EventConstants.GameEntryRemoveRequestWireSize)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var requested = new PacketReader(packet.Payload).ReadInt32();

        // The selector is either zero or the caller's own character; it is a
        // selector rather than permission to release someone else's place.
        if (requested is 0 || requested == characterIdentifier)
        {
            var reservation = await registrationService.FindLiveAsync(characterIdentifier, cancellationToken);
            if (reservation is not null)
            {
                await registrationService.CancelAsync(characterIdentifier, cancellationToken);

                // The reply names the lobby directory key the client matches its
                // cached rows against, so the released place is cleared by the
                // client rather than re-read.
                var releaseWriter = new PacketWriter();
                releaseWriter.WriteInt32(0);
                releaseWriter.WriteInt32(
                    session.LobbyIdentifier ?? EventConstants.TournamentRegistrationSelector);
                await sessionHelper.SendPacketAsync(
                    session,
                    CommandConstants.RemoveEventGameEntryResult,
                    releaseWriter.Build(),
                    cancellationToken);
                return;
            }
        }

        if (requested != characterIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var assignment = await EventAssignmentSupport.FindForSessionAsync(
            session,
            characterIdentifier,
            assignmentService,
            cancellationToken);
        if (assignment is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        await assignmentService.CancelAsync(assignment.MatchIdentifier, cancellationToken);

        // The removal is acknowledged with the character it removed, so the
        // client can drop the cached entry without re-reading the list.
        var writer = new PacketWriter();
        writer.WriteInt32(0);
        writer.WriteInt32(characterIdentifier);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.RemoveEventGameEntryResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.RemoveEventGameEntryResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}

/// <summary>
/// Serves the roster behind an assigned game. Both selectors must name the
/// assignment the caller holds, which is what keeps a cached detail screen from
/// being answered with a newer match's roster.
/// </summary>
public sealed class GetAssignedMemberInformationHandler(
    EventAssignmentService assignmentService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier
            || packet.Payload.Length != EventConstants.AssignedMemberInformationRequestWireSize)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var detailSelector = reader.ReadInt32();
        var snapshotSelector = reader.ReadInt32();
        var assignment = await EventAssignmentSupport.FindForSessionAsync(
            session,
            characterIdentifier,
            assignmentService,
            cancellationToken);
        var team = assignment?.TeamOfCharacter(characterIdentifier);
        if (assignment is null
            || team is null
            || detailSelector != assignment.ActiveStateIdentifier
            || snapshotSelector != assignment.ActiveStateIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteAssignedMemberInformation(writer, team);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetAssignedMemberInformationResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetAssignedMemberInformationResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}

/// <summary>
/// Shared lookup of the assignment a session holds. The team the session is
/// attached to is routing context, so it selects which match to read; the
/// match itself is still read from storage and never inferred from it.
/// </summary>
internal static class EventAssignmentSupport
{
    /// <summary>Finds the assignment of the team a session is attached to.</summary>
    /// <param name="session">Session asking.</param>
    /// <param name="characterIdentifier">Character the session belongs to.</param>
    /// <param name="assignmentService">Service that owns the assignments.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The assignment, or null when the character is in none.</returns>
    public static async Task<EventAssignment?> FindForSessionAsync(
        TcpSession session,
        int characterIdentifier,
        EventAssignmentService assignmentService,
        CancellationToken cancellationToken)
    {
        if (session.EventTeamIdentifier is not { } teamIdentifier)
        {
            return null;
        }

        var assignment = await assignmentService.FindByTeamAsync(teamIdentifier, cancellationToken);
        return assignment?.TeamOfCharacter(characterIdentifier) is null ? null : assignment;
    }
}
