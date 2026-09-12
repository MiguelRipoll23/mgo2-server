using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game;

/// <summary>Lists the gameplay lobbies on the lobby-select screen.</summary>
/// <param name="lobbyService">Service that owns the lobby metadata.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetGameLobbyInfoHandler(
    LobbyService lobbyService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Maximum number of lobbies per packet.</summary>
    private const int MaximumLobbiesPerPacket = 8;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var lobbies = lobbyService.GetCached()
            .Where(lobby => lobby.Type == LobbyType.Game)
            .ToList();

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetGameLobbyInfoStart, cancellationToken);

        for (var offset = 0; offset < lobbies.Count; offset += MaximumLobbiesPerPacket)
        {
            var page = lobbies.Skip(offset).Take(MaximumLobbiesPerPacket).ToList();
            var writer = new PacketWriter();

            for (var index = 0; index < page.Count; index++)
            {
                var lobby = page[index];
                writer.WriteUInt32((uint)(offset + index));
                // Attributes word: the subtype lives in the top byte.
                writer.WriteUInt32((uint)((lobby.SubtypeIdentifier & 0xff) << 24));
                writer.WriteUInt16(lobby.Identifier);
                writer.WriteFixedString(lobby.Name, 16);
                // The client parser reads a fixed 35-byte stride, so there is
                // no text block here.
                writer.WriteUInt32(0);
                writer.WriteUInt32(0);
                writer.WriteUInt8(1);
            }

            if (writer.Size > 0)
            {
                await sessionHelper.SendPacketAsync(session, CommandConstants.GetGameLobbyInfoPage, writer.Build(), cancellationToken);
            }
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetGameLobbyInfoEnd, cancellationToken);
    }
}

/// <summary>Serves the fixed game-entry information grid.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetGameEntryInfoHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32(4);
        writer.WritePadding(4 * 57);
        return sessionHelper.SendPacketAsync(session, CommandConstants.GetGameEntryInfoResult, writer.Build(), cancellationToken);
    }
}
