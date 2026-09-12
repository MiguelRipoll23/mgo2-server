using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game;

/// <summary>
/// Echoes the payload of the pre-lobby handshake back to the client. The
/// command sits outside the lobby packet space, but the client stalls when it
/// is not answered.
/// </summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class EchoHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(session, CommandConstants.Echo, packet.Payload, cancellationToken);
}

/// <summary>Leaves the gameplay lobby and republishes the player counts.</summary>
/// <param name="lobbyTrackerService">Service that tracks the lobby population.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetLobbyDisconnectHandler(
    LobbyTrackerService lobbyTrackerService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        lobbyTrackerService.LeaveLobby(session);
        await lobbyTrackerService.SynchronizeAllLobbyCountsAsync(cancellationToken);
        await sessionHelper.SendResultAsync(session, CommandConstants.GetLobbyDisconnectResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Answers a training-session connection with its fixed payload.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class TrainingConnectHandler(SessionHelper sessionHelper) : ICommandHandler
{
    private static readonly byte[] Payload =
    [
        0x00, 0x0a, 0x00, 0x15, 0x00, 0x3a, 0x00, 0x08, 0x00, 0x61,
    ];

    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(session, CommandConstants.TrainingConnectResult, Payload, cancellationToken);
}
