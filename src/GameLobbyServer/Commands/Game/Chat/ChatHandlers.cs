using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>Echoes the chat-family request back with a result word.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class ChatEchoHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(session, CommandConstants.ChatEchoResult, ErrorCodeConstants.ResultNone, cancellationToken);
}

/// <summary>Broadcasts a chat message to everyone in the same room.</summary>
/// <param name="activeGameSessions">Connections currently in the lobby.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SendChatHandler(
    ActiveGameSessionsService activeGameSessions,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Prefix a client may not impersonate.</summary>
    private const string ServerPrefix = "Server | ";

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is null)
        {
            return;
        }

        var request = ChatPayloadBuilder.ParseRequest(packet.Payload);
        if (request is null)
        {
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // A client may not impersonate the server.
        if (request.Text.StartsWith(ServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.SendChatResult,
                ChatPayloadBuilder.BuildReply(
                    characterIdentifier,
                    request with { Text = ServerPrefix + "You can't send server messages." }),
                cancellationToken);
            return;
        }

        var payload = ChatPayloadBuilder.BuildReply(characterIdentifier, request);
        var gameIdentifier = session.GameIdentifier;

        // Not in a game: answer the sender alone so their own line still renders.
        // The client has no local echo, so a fanned-out reply is the only route
        // the message has to the screen, its author's included.
        if (gameIdentifier is null)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.SendChatResult, payload, cancellationToken);
            return;
        }

        foreach (var target in activeGameSessions.List())
        {
            if (target.GameIdentifier == gameIdentifier)
            {
                await sessionHelper.SendPacketAsync(target, CommandConstants.SendChatResult, payload, cancellationToken);
            }
        }
    }
}
