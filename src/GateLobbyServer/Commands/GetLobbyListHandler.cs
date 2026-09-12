using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GateLobbyServer.Commands;

/// <summary>
/// Serves the lobby list to a client that just connected to the gate. Each page
/// carries at most twenty-two entries because of the payload limit, and the
/// whole list is cut at the client's own hard cap of thirty-two entries.
/// </summary>
/// <param name="lobbyService">Service that owns the lobby cache.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetLobbyListHandler(
    LobbyService lobbyService,
    SessionHelper sessionHelper,
    ILogger<GetLobbyListHandler> logger) : ICommandHandler
{
    /// <summary>Maximum number of entries that fit in one page.</summary>
    private const int MaximumLobbiesPerPacket = 22;

    /// <summary>Client-side cap on the total number of entries; the client aborts the whole list above it.</summary>
    private const int MaximumLobbiesTotal = 32;

    /// <summary>Field length of a lobby name.</summary>
    private const int LobbyNameLength = 16;

    /// <summary>Field length of a lobby address.</summary>
    private const int LobbyIpAddressLength = 15;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        try
        {
            var lobbies = lobbyService.GetCached().Take(MaximumLobbiesTotal).ToList();
            var pages = new List<byte[]>();
            var baseIndex = 0;

            PayloadHelper.ForEachPage(lobbies, MaximumLobbiesPerPacket, page =>
            {
                var writer = new PacketWriter();
                foreach (var lobby in page)
                {
                    var restriction = 0;
                    restriction |= lobby.BeginnerOnly ? 0x01 : 0;
                    restriction |= lobby.ExpansionOnly ? 0x08 : 0;
                    restriction |= lobby.NoHeadshot ? 0x10 : 0;

                    writer.WriteUInt32((uint)baseIndex++);
                    writer.WriteUInt32((uint)lobby.Type);
                    writer.WriteFixedString(lobby.Name, LobbyNameLength);
                    writer.WriteFixedString(lobby.IpAddress, LobbyIpAddressLength);
                    writer.WriteUInt16(lobby.Port);
                    writer.WriteUInt16(lobby.PlayersCount);
                    writer.WriteUInt16(lobby.Identifier);
                    writer.WriteUInt8(restriction);
                }

                pages.Add(writer.Build());
            });

            await sessionHelper.SendResultAsync(session, 0x2002, ErrorCodeConstants.ResultNone, cancellationToken);
            foreach (var payload in pages)
            {
                await sessionHelper.SendPacketAsync(session, 0x2003, payload, cancellationToken);
            }

            await sessionHelper.SendResultAsync(session, 0x2004, ErrorCodeConstants.ResultNone, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "getLobbyList error");
            await sessionHelper.SendErrorAsync(session, 0x2002, ErrorCodeConstants.ErrorGeneral, cancellationToken);
        }
    }
}
