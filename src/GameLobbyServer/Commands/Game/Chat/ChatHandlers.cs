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

    /// <summary>Longest message the client can carry.</summary>
    private const int MaximumMessageLength = 127;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.GameIdentifier is null || session.CharacterIdentifier is null || packet.Payload.Length < 1)
        {
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var flag = reader.ReadUInt8();
        var raw = reader.Remaining > 0
            ? reader.ReadFixedString(Math.Min(reader.Remaining, MaximumMessageLength))
            : string.Empty;

        var message = StripChannelPrefix(raw);

        // A client may not impersonate the server.
        if (message.StartsWith(ServerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.SendChatResult,
                BuildPayload(session.CharacterIdentifier.Value, flag, ServerPrefix + "You can't send server messages."),
                cancellationToken);
            return;
        }

        var payload = BuildPayload(session.CharacterIdentifier.Value, flag, message);
        var gameIdentifier = session.GameIdentifier;

        foreach (var target in activeGameSessions.List())
        {
            if (target.GameIdentifier == gameIdentifier)
            {
                await sessionHelper.SendPacketAsync(target, CommandConstants.SendChatResult, payload, cancellationToken);
            }
        }
    }

    private static byte[] BuildPayload(int characterIdentifier, int flag, string message)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32((uint)characterIdentifier);
        writer.WriteUInt8(flag);
        // The text is NUL-terminated, so it occupies its length plus one byte.
        writer.WriteFixedString(message, message.Length + 1);
        return writer.Build();
    }

    private static string StripChannelPrefix(string message)
    {
        var stripped = message;
        if (stripped.StartsWith("/all", StringComparison.Ordinal))
        {
            stripped = stripped[4..];
        }
        else if (stripped.StartsWith("/team", StringComparison.Ordinal))
        {
            stripped = stripped[5..];
        }

        return stripped.StartsWith(' ') ? stripped.TrimStart() : stripped;
    }
}
