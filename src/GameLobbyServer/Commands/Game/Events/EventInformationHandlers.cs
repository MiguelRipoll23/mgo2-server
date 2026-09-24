using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>Serves the event information record for the connected lobby's selector.</summary>
/// <param name="informationService">Service that builds the record.</param>
/// <param name="lobbyService">Service that owns the lobby metadata.</param>
/// <param name="options">Event configuration, for the enabled flag.</param>
/// <param name="sessionHelper">Helper used to write the reply.</param>
public sealed class GetEventInformationHandler(
    EventInformationService informationService,
    LobbyService lobbyService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || packet.Payload.Length != 1)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var selector = packet.Payload[0];
        if (!EventConstants.IsEventSelector(selector) || session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier, cancellationToken);
        if (lobby.SubtypeIdentifier != selector)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetEventInformationResult,
            informationService.BuildRecord(selector),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetEventInformationResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>Serves the event information record for one cached event identifier.</summary>
/// <param name="informationService">Service that builds the record.</param>
/// <param name="options">Event configuration, for the enabled flag.</param>
/// <param name="sessionHelper">Helper used to write the reply.</param>
public sealed class GetEventInformationByIdHandler(
    EventInformationService informationService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var eventIdentifier = packet.Payload.Length == 4
            ? (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.Payload)
            : 0;

        // The reply echoes the record the client cached, so only the transient
        // correlation this server publishes is answerable.
        if (!options.Value.Enabled || eventIdentifier != EventConstants.TransientEventIdentifier)
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.GetEventInformationByIdResult,
                ErrorCodeConstants.ResultGeneral,
                cancellationToken);
            return;
        }

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetEventInformationByIdResult,
            informationService.BuildRecord(eventIdentifier, EventConstants.SurvivalSelector, DateTimeOffset.UtcNow, EventHostEnvironment.CreateDefault()),
            cancellationToken);
    }
}

/// <summary>
/// Answers the saved team-creation preset request. No preset is stored, and the
/// client's own not-found path builds localized defaults, so the documented
/// not-found result is returned rather than a synthesized empty preset.
/// </summary>
/// <param name="options">Event configuration, for the enabled flag.</param>
/// <param name="sessionHelper">Helper used to write the reply.</param>
public sealed class GetTeamCreateInformationHandler(
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var result = options.Value.Enabled && packet.Payload.Length == 0
            ? EventConstants.TeamCreatePresetNotFound
            : ErrorCodeConstants.ResultGeneral;

        return sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetTeamCreateInformationResult,
            result,
            cancellationToken);
    }
}

/// <summary>
/// Answers a directly reachable event screen whose success body is not
/// recovered. One handler serves all seven commands, because they differ only in
/// their identifiers and the exact request size the screen sends.
/// </summary>
/// <param name="options">Event configuration, for the enabled flag.</param>
/// <param name="sessionHelper">Helper used to write the reply.</param>
public sealed class EventAdjacentRequestHandler(
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled
            || !EventAdjacentRequestUtils.TryResolve(packet.Header.Command, out var request))
        {
            // A command with no mapping has no reply id either, so it is left
            // alone rather than answered with an identifier the client may not
            // parse.
            return Task.CompletedTask;
        }

        var result = packet.Payload.Length == request.ExpectedRequestSize
            ? ErrorCodeConstants.ResultFeatureNotImplemented
            : ErrorCodeConstants.ResultGeneral;

        return sessionHelper.SendResultAsync(session, request.ResponseCommand, result, cancellationToken);
    }
}
