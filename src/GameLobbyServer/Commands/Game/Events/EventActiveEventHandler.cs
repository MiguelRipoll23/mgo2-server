using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Lists the events the connected lobby is publishing. The client opens the
/// browse list with <c>0x4A40</c>, receives one <c>0x4A42</c> row per event and
/// closes it with <c>0x4A43</c>.
/// <para>
/// The whole stream is built before the first packet is written, and the two
/// markers are always sent: a lobby with nothing scheduled answers with an
/// empty list rather than with no packets at all, which the client renders as a
/// screen that never finishes loading.
/// </para>
/// </summary>
public sealed class GetEventListHandler(
    EventScheduleService scheduleService,
    LobbyService lobbyService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The list belongs to a lobby, so a connection in none has nothing to
        // stream.
        if (session.LobbyIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobbyIdentifier = session.LobbyIdentifier.Value;

        // The command carries no body; one that does is asking for something the
        // list does not answer.
        if (packet.Payload.Length != 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The events are the lobby's own, so the mode comes from the lobby row
        // rather than from the connection, which only says where it is.
        var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier, cancellationToken);
        if (!EventConstants.IsEventSelector(lobby.SubtypeIdentifier))
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var schedules = await scheduleService.ListPublishedAsync(
            lobby.SubtypeIdentifier,
            cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(
            session,
            CommandConstants.GetEventListStart,
            cancellationToken);

        foreach (var schedule in schedules)
        {
            var writer = new PacketWriter();
            EventScheduleListUtils.WriteItem(writer, schedule);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetEventListPage,
                writer.Build(),
                cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(
            session,
            CommandConstants.GetEventListEnd,
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetEventListStart,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>
/// Answers the event detail. The client reads the item-state count out of the
/// third u16 field and then reads exactly that many state bytes, so the count
/// and the bytes are written together rather than independently.
/// </summary>
public sealed class GetEventDetailHandler(
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length < sizeof(int))
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // Zero is the client's name for "whichever event is current", and it is
        // answered with the same event the screens advertise rather than
        // refused: it is a request the client makes routinely, not an
        // identifier it invented.
        var requested = new PacketReader(packet.Payload).ReadInt32();
        var eventIdentifier = requested == 0
            ? EventConstants.TransientEventIdentifier
            : requested;
        if (eventIdentifier != EventConstants.TransientEventIdentifier)
        {
            // The routing context is cleared on a refusal so the commands that
            // follow cannot act on an event this screen never served.
            session.SelectedEventIdentifier = null;
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // Opening a detail screen is what makes the event current for this
        // connection, which is how the entry command that carries no event
        // identifier of its own knows which event it is entering.
        session.SelectedEventIdentifier = eventIdentifier;

        // The nine u16 fields, eleven signed values and ten metadata bytes that
        // carry the event's own presentation have no recovered meaning here, so
        // they are neutral and the detail describes no list items. Filling them
        // with invented values would read as a correct event with wrong data.
        var itemStates = Array.Empty<byte>();
        var detailFields = new ushort[EventConstants.EventDetailU16Count];
        detailFields[2] = (ushort)itemStates.Length;

        var writer = new PacketWriter();
        EventActiveEventUtils.WriteEventDetail(
            writer,
            activeStateIdentifier: eventIdentifier,
            sequence: 1,
            eventIdentifier,
            globalState: EventConstants.ActiveEventAssignedState,
            uiContext: 0,
            detailFields,
            detailValue: 0,
            itemStates,
            flagBits: 0,
            detailByte: 0,
            trailingValue: 0,
            new int[EventConstants.EventDetailValueCount],
            new byte[EventConstants.EventDetailMetadataCount],
            trailingU16: 0);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetEventDetailResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetEventDetailResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}

/// <summary>
/// Answers the detail of the game the caller was assigned, or of the Tournament
/// place it holds. The request names an event or a room, so a client cannot open
/// the detail of one it is not in, and the detail is read from the caller's own
/// assignment or reservation rather than from the request.
/// </summary>
public sealed class GetAssignedGameDetailHandler(
    EventAssignmentService assignmentService,
    TournamentRegistrationService registrationService,
    EventInformationService informationService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // Without a character the detail has no owner to be read from.
        if (session.CharacterIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // The request names an event or a room, so a body too short to hold that
        // identifier cannot be one.
        if (packet.Payload.Length < sizeof(int))
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var requested = new PacketReader(packet.Payload).ReadInt32();

        // A pending place is answered before an assignment is looked for: the
        // client opens the same screen for either, and a reserved player has no
        // assignment yet.
        var reservation = await registrationService.FindLiveAsync(characterIdentifier, cancellationToken);
        if (reservation is not null && reservation.EventIdentifier == requested)
        {
            var (start, _) = informationService.ScheduleFor(DateTimeOffset.UtcNow);
            var reservationWriter = new PacketWriter();
            EventAssignmentUtils.WriteAssignedGameDetail(
                reservationWriter,
                requested,
                session.LobbyIdentifier ?? EventConstants.TournamentRegistrationSelector,
                EventConstants.TournamentRegistrationSelector,
                start,
                options.Value.Title,
                EventHostEnvironment.CreateDefault());

            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetAssignedGameDetailResult,
                reservationWriter.Build(),
                cancellationToken);

            // The entrant table around the detail is opened and closed with the
            // event identifier, which is what tells the client the detail is
            // complete. This is the per-event list, not the browse list.
            var boundaryWriter = new PacketWriter();
            EventActiveEventUtils.WriteEventListBoundary(boundaryWriter, requested);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventEntrantListStart,
                boundaryWriter.Build(),
                cancellationToken);
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.EventEntrantListEnd,
                boundaryWriter.Build(),
                cancellationToken);
            return;
        }

        var assignment = await EventAssignmentSupport.FindForSessionAsync(
            session,
            characterIdentifier,
            assignmentService,
            cancellationToken);
        var team = assignment?.TeamOfCharacter(characterIdentifier);
        if (assignment is null || team is null || requested != assignment.GameIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventAssignmentUtils.WriteAssignedGameDetail(
            writer,
            team.EventIdentifier,
            assignment.LobbyIdentifier,
            assignment.LobbySubtype,
            assignment.ActivationTimeSeconds,
            gameName: team.Name,
            team.HostEnvironment);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetAssignedGameDetailResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetAssignedGameDetailResult,
            EventConstants.ResultActiveStateMismatch,
            cancellationToken);
}
