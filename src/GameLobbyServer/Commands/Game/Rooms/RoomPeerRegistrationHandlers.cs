using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// Answers the host's peer-to-peer state machine. All three of its
/// register-with-server round-trips read a result word and a peer-table key,
/// so the key is echoed from the request and a short read would stall the
/// state machine until it disconnects the peer.
/// </summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPeerRegistrationHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Echoes the request's leading word back as the peer-table key.</summary>
    public static async Task ReplyAsync(
        TcpSession session,
        ushort replyCommand,
        Packet packet,
        SessionHelper sessionHelper,
        CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var key = reader.Remaining >= 4 ? reader.ReadUInt32() : 0;

        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32(key);
        await sessionHelper.SendPacketAsync(session, replyCommand, writer.Build(), cancellationToken);
    }

    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        ReplyAsync(session, CommandConstants.HostPlayerConnectedResult, packet, sessionHelper, cancellationToken);
}

/// <summary>Answers the host's disconnect notification.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPlayerDisconnectedHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        HostPeerRegistrationHandler.ReplyAsync(session, CommandConstants.HostPlayerDisconnectedResult, packet, sessionHelper, cancellationToken);
}

/// <summary>
/// Answers the peer registration that also reports a team, and stores the team.
/// <para>
/// The payload is <c>{u32 character id, u8 team}</c>, and the byte is the raw
/// roster slot: unlike the player's own team change it can carry 0, 2 and the 254
/// "no team" sentinel, so it is read through <see cref="TeamSlotUtils"/> and never
/// copied straight into a slot the other packet wrote. The reply is untouched —
/// the ack's second word is a request handle the client looks up by value, and a
/// mismatched one leaves the host's state machine to time out rather than fail.
/// </para>
/// </summary>
/// <param name="gameService">Service the reported slot is stored with.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostSetPlayerTeamHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.GameIdentifier is { } gameIdentifier && packet.Payload.Length >= 5)
        {
            var reader = new PacketReader(packet.Payload);
            var characterIdentifier = (int)reader.ReadUInt32();
            var team = TeamSlotUtils.FromRawSlot(reader.ReadUInt8());

            if (characterIdentifier > 0)
            {
                await gameService.SetPlayerTeamAsync(gameIdentifier, characterIdentifier, team, cancellationToken);
            }
        }

        await HostPeerRegistrationHandler.ReplyAsync(
            session,
            CommandConstants.HostSetPlayerTeamResult,
            packet,
            sessionHelper,
            cancellationToken);
    }
}

/// <summary>Answers the host's finished-connect registration.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPlayerConnectFinishHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        HostPeerRegistrationHandler.ReplyAsync(session, CommandConstants.HostPlayerConnectFinishResult, packet, sessionHelper, cancellationToken);
}
