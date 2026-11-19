using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Chat;

/// <summary>
/// Handles the room-scoped <c>/bots pause</c> chat command, and its resume
/// counterpart, by asking the room's gameplay host to hold or release the bots.
/// </summary>
/// <remarks>
/// <para>
/// <b>The command is addressed by the sender's own room.</b> The session's
/// <see cref="TcpSession.GameIdentifier"/> is the room the line was typed in, and
/// only that room's host is asked. A player in another room cannot pause these
/// bots, and the gameplay host checks the identifier again before applying it.
/// </para>
/// <para>
/// <b>The answer is the sender's alone.</b> It is a server-authored line sent
/// back as the player's own message, the same route <c>/ip</c> uses, so the
/// room's other players do not see a command they did not type. A room with no
/// reachable gameplay host is told so rather than being left with a command that
/// silently did nothing.
/// </para>
/// </remarks>
/// <param name="gameService">Service the room and its host endpoint are read from.</param>
/// <param name="control">Client of the gameplay host's control RPC.</param>
/// <param name="sessionHelper">Helper used to write the confirmation.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class BotPauseChatCommandService(
    GameService gameService,
    GameplayControlClientService control,
    SessionHelper sessionHelper,
    ILogger<BotPauseChatCommandService> logger)
{
    /// <summary>Text that pauses the room's bots.</summary>
    public const string PauseCommandText = "/bots pause";

    /// <summary>Text that lists the bot subcommands.</summary>
    public const string HelpCommandText = "/bots help";

    /// <summary>Text that resumes the room's bots.</summary>
    public const string ResumeCommandText = "/bots resume";

    private const string NotInRoomMessage = "You are not in a game room.";
    private const string NoRoomMessage = "That room is no longer available.";
    private const string UnreachableMessage = "The gameplay server did not answer; the bots were not changed.";

    /// <summary>
    /// Reads the command a chat line states.
    /// </summary>
    /// <param name="text">Chat text as it arrived.</param>
    /// <param name="command">The bot subcommand the line names.</param>
    /// <returns><c>true</c> when the line is one of this service's commands.</returns>
    public static bool TryRead(string text, out BotPauseChatCommand command)
    {
        var trimmed = text.Trim();
        if (string.Equals(trimmed, PauseCommandText, StringComparison.OrdinalIgnoreCase))
        {
            command = BotPauseChatCommand.Pause;
            return true;
        }

        if (string.Equals(trimmed, ResumeCommandText, StringComparison.OrdinalIgnoreCase))
        {
            command = BotPauseChatCommand.Resume;
            return true;
        }

        if (string.Equals(trimmed, HelpCommandText, StringComparison.OrdinalIgnoreCase))
        {
            command = BotPauseChatCommand.Help;
            return true;
        }

        command = BotPauseChatCommand.None;
        return false;
    }

    /// <summary>
    /// Applies the command to the sender's room and answers the sender.
    /// </summary>
    /// <param name="session">Connection that typed the command.</param>
    /// <param name="characterIdentifier">Character the line is spoken as.</param>
    /// <param name="command">The bot subcommand to run.</param>
    /// <param name="cancellationToken">Token that cancels the work.</param>
    public async Task HandleAsync(
        TcpSession session,
        int characterIdentifier,
        BotPauseChatCommand command,
        CancellationToken cancellationToken)
    {
        if (command == BotPauseChatCommand.Help)
        {
            await ReplyAsync(
                session,
                characterIdentifier,
                "Bot commands: /bots pause, /bots resume, /bots help.",
                cancellationToken);
            await ReplyAsync(
                session,
                characterIdentifier,
                "Pause stops the bots moving and shooting; resume lets them continue.",
                cancellationToken);
            return;
        }

        var paused = command == BotPauseChatCommand.Pause;

        if (session.GameIdentifier is not { } gameIdentifier)
        {
            await ReplyAsync(session, characterIdentifier, NotInRoomMessage, cancellationToken);
            return;
        }

        var room = await gameService.FindByIdAsync(gameIdentifier, cancellationToken);
        if (room is null)
        {
            await ReplyAsync(session, characterIdentifier, NoRoomMessage, cancellationToken);
            return;
        }

        var endpoint = await gameService.GetConnectionInformationAsync(
            room.HostIdentifier,
            cancellationToken);
        if (endpoint is null)
        {
            logger.LogDebug(
                "Room {GameIdentifier}: bot pause refused because host {HostIdentifier} has no registered endpoint",
                gameIdentifier,
                room.HostIdentifier);
            await ReplyAsync(session, characterIdentifier, UnreachableMessage, cancellationToken);
            return;
        }

        var result = await control.SetBotsPausedAsync(
            endpoint,
            gameIdentifier,
            paused,
            cancellationToken);

        if (!result.Applied)
        {
            logger.LogDebug(
                "Room {GameIdentifier}: bot pause was not applied: {Detail}",
                gameIdentifier,
                result.Detail);
            await ReplyAsync(session, characterIdentifier, UnreachableMessage, cancellationToken);
            return;
        }

        logger.LogInformation(
            "Room {GameIdentifier}: character {CharacterIdentifier} {BotCommand} the bots",
            gameIdentifier,
            characterIdentifier,
            paused ? "paused" : "resumed");
        await ReplyAsync(
            session,
            characterIdentifier,
            paused ? "Bots paused: they will hold still and hold their fire." : "Bots resumed.",
            cancellationToken);
    }

    /// <summary>
    /// Sends one server-authored line to the command's sender.
    /// </summary>
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

/// <summary>Subcommand a <c>/bots</c> chat line names.</summary>
public enum BotPauseChatCommand
{
    /// <summary>The line is not a bot command.</summary>
    None,

    /// <summary>Stop the bots moving and shooting.</summary>
    Pause,

    /// <summary>Let the bots move and shoot again.</summary>
    Resume,

    /// <summary>List the bot subcommands.</summary>
    Help,
}
