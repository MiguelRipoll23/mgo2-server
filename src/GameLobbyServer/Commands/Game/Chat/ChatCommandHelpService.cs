using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Answers <c>/help</c> with every chat command this lobby can run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The list is the lobby's, not one handler's.</b> The public-address line is
/// always available, the bot commands are available in a room, and the Survival
/// self-test exists only in the Survival lobby. A help line that named a command
/// this lobby cannot run would send a player after a refusal, so the mode is read
/// before the survival line is added.
/// </para>
/// <para>
/// The answer is the sender's own line and is split by
/// <see cref="ChatPayloadBuilder.BuildServerReplies"/> when it is wider than the
/// client's chat row.
/// </para>
/// </remarks>
/// <param name="lobbyIdentity">Service that knows this lobby's own game type.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class ChatCommandHelpService(
    LobbyIdentityService lobbyIdentity,
    SessionHelper sessionHelper)
{
    /// <summary>Text that asks for the lobby's commands.</summary>
    public const string CommandText = "/help";

    /// <summary>Whether a chat line is the help command.</summary>
    /// <param name="text">Chat text as it arrived.</param>
    public static bool IsCommand(string text) =>
        string.Equals(text.Trim(), CommandText, StringComparison.OrdinalIgnoreCase);

    /// <summary>Answers the command to the character that asked.</summary>
    /// <param name="session">Connection the line is written to.</param>
    /// <param name="characterIdentifier">Character the line is spoken as.</param>
    /// <param name="cancellationToken">Token that cancels the writes.</param>
    public async Task HandleAsync(
        TcpSession session,
        int characterIdentifier,
        CancellationToken cancellationToken)
    {
        await ReplyAsync(
            session,
            characterIdentifier,
            "Commands: /help, /ip, /bots pause, /bots resume, /bots help.",
            cancellationToken);

        if (await lobbyIdentity.ResolveModeAsync(cancellationToken) == LobbySubtypeConstants.Survival)
        {
            await ReplyAsync(
                session,
                characterIdentifier,
                "Survival: /test.",
                cancellationToken);
        }

        await ReplyAsync(
            session,
            characterIdentifier,
            "Chat channels: /all, /team.",
            cancellationToken);
    }

    /// <summary>Sends one server-authored line to the command's sender.</summary>
    /// <param name="session">Connection the line is written to.</param>
    /// <param name="characterIdentifier">Character the line is spoken as.</param>
    /// <param name="message">Text of the line.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    private async Task ReplyAsync(
        TcpSession session,
        int characterIdentifier,
        string message,
        CancellationToken cancellationToken)
    {
        var line = new ChatRequest(ChatPayloadBuilder.PublicChannelDigit, message);
        foreach (var payload in ChatPayloadBuilder.BuildServerReplies(characterIdentifier, line))
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.SendChatResult,
                payload,
                cancellationToken);
        }
    }
}
