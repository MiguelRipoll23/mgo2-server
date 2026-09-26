using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.News;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Runs the commands the gateway delivers. The flash command relays its message
/// through the same dispatcher the broadcast endpoints of the API use, and the
/// message command writes an official bot message in the channel it was used
/// in; each interaction is answered so the command does not time out.
/// </summary>
/// <param name="responder">Service that answers the interactions.</param>
/// <param name="messageService">Service that writes the channel messages.</param>
/// <param name="flashNewsDispatcher">Service every flash is relayed through.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
/// <param name="scheduleCommands">
/// Service that runs the event scheduling command. It is last and optional
/// because it is the only command that writes, and a deployment that has not
/// wired it up should still answer the two that do not.
/// </param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordCommandService(
    IDiscordInteractionResponder responder,
    IDiscordMessageService messageService,
    FlashNewsDispatcherService flashNewsDispatcher,
    IOptions<DiscordOptions> options,
    ILogger<DiscordCommandService> logger,
    DiscordEventScheduleCommandService? scheduleCommands = null,
    DiscordFakePlayerCommandService? fakePlayerCommands = null)
{
    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteractionType = 2;

    /// <summary>Name of the option that carries the text of a flash.</summary>
    private const string MessageOptionName = "message";

    /// <summary>Name of the option that carries the text of an official message.</summary>
    private const string BodyOptionName = "body";

    /// <summary>
    /// Longest message the ticker of the game client carries, which is the cap
    /// the HTTP broadcast API enforces as well.
    /// </summary>
    private const int MaximumFlashMessageLength = 255;

    /// <summary>Longest channel message Discord accepts.</summary>
    private const int MaximumChannelMessageLength = 2000;

    /// <summary>
    /// Longest text Discord accepts in one command option. The option holds
    /// three times what a channel message does, so a long body is written as
    /// several messages rather than cut short.
    /// </summary>
    private const int MaximumCommandOptionLength = 6000;

    private readonly DiscordOptions options = options.Value;

    /// <summary>Handles one interaction the gateway delivered.</summary>
    /// <param name="interaction">Interaction to answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleInteractionAsync(
        DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        if (!GuildMatches(interaction.GuildIdentifier))
        {
            return;
        }

        if (IsFlashCommand(interaction))
        {
            await HandleFlashCommandAsync(interaction, cancellationToken);
        }
        else if (IsMessageCommand(interaction))
        {
            await HandleMessageCommandAsync(interaction, cancellationToken);
        }
        else if (scheduleCommands is not null
            && DiscordEventScheduleCommandService.IsEventCommand(interaction))
        {
            await scheduleCommands.HandleAsync(interaction, cancellationToken);
        }
        else if (fakePlayerCommands is not null
            && DiscordFakePlayerCommandService.IsFakePlayerCommand(interaction))
        {
            await fakePlayerCommands.HandleAsync(interaction, cancellationToken);
        }
    }

    /// <summary>Relays a flash news to every lobby the command was used for.</summary>
    /// <param name="interaction">Interaction of the command.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task HandleFlashCommandAsync(
        DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var interactionIdentifier = interaction.Identifier;
        var interactionToken = interaction.Token;
        if (string.IsNullOrWhiteSpace(interactionIdentifier) || string.IsNullOrWhiteSpace(interactionToken))
        {
            logger.LogWarning(
                "The {Command} command arrived without a way to answer it",
                DiscordOptions.FlashCommandName);
            return;
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsStaffCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.FlashCommandName);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "You need the Moderator or Manager role to use this command.",
                cancellationToken);
            return;
        }

        var message = MessageOf(interaction, MessageOptionName);
        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "The message option is required.",
                cancellationToken);
            return;
        }

        // A slash command does not carry the validation the HTTP API applies,
        // so the message is trimmed to what the ticker of the client holds.
        if (message.Length > MaximumFlashMessageLength)
        {
            message = message[..MaximumFlashMessageLength];
        }

        var recipients = flashNewsDispatcher.Dispatch(new FlashNewsAnnouncement(message));
        logger.LogInformation(
            "The {Command} command relayed a flash news to {Recipients} lobbies",
            DiscordOptions.FlashCommandName,
            recipients);

        await ReplyAsync(
            interactionIdentifier,
            interactionToken,
            $"Flash news sent to {recipients} lobbies.",
            cancellationToken);
    }

    /// <summary>Writes an official bot message in the channel of the command.</summary>
    /// <param name="interaction">Interaction of the command.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task HandleMessageCommandAsync(
        DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        var interactionIdentifier = interaction.Identifier;
        var interactionToken = interaction.Token;
        if (string.IsNullOrWhiteSpace(interactionIdentifier) || string.IsNullOrWhiteSpace(interactionToken))
        {
            logger.LogWarning(
                "The {Command} command arrived without a way to answer it",
                DiscordOptions.MessageCommandName);
            return;
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsStaffCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.MessageCommandName);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "You need the Moderator or Manager role to use this command.",
                cancellationToken);
            return;
        }

        var message = MessageOf(interaction, BodyOptionName);
        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "The body option is required.",
                cancellationToken);
            return;
        }

        var channelIdentifier = interaction.ChannelIdentifier;
        if (string.IsNullOrWhiteSpace(channelIdentifier))
        {
            logger.LogWarning(
                "The {Command} command arrived without a channel to write in",
                DiscordOptions.MessageCommandName);
            return;
        }

        // The command is answered and the message is written into the channel,
        // where the whole guild sees it. A body longer than one channel message
        // holds is written as several messages, because a moderator writing an
        // announcement is not helped by the tail of it being dropped.
        var parts = SplitIntoChannelMessages(message);
        var sent = 0;
        foreach (var part in parts)
        {
            // Every part is attempted even after one is refused: a rate limited
            // chunk should not swallow the rest of the announcement.
            if (await messageService.SendChannelMessageAsync(
                channelIdentifier,
                part,
                cancellationToken))
            {
                sent++;
            }
        }

        if (sent != parts.Count)
        {
            logger.LogWarning(
                "The {Command} command wrote {Sent} of {Parts} messages in the channel of the interaction",
                DiscordOptions.MessageCommandName,
                sent,
                parts.Count);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                $"The message could not be sent in full: {sent} of {parts.Count} parts arrived. Try again later.",
                cancellationToken);
            return;
        }

        logger.LogInformation(
            "The {Command} command wrote an official message of {Parts} part(s) in the channel {ChannelIdentifier}",
            DiscordOptions.MessageCommandName,
            parts.Count,
            channelIdentifier);

        await ReplyAsync(
            interactionIdentifier,
            interactionToken,
            parts.Count == 1
                ? "Official message sent in this channel."
                : $"Official message sent in this channel as {parts.Count} messages.",
            cancellationToken);
    }

    /// <summary>
    /// Splits the body into the parts a channel message holds. The parts are cut
    /// on a line where there is one and on a space where there is not, so a long
    /// announcement is not cut through the middle of a word.
    /// </summary>
    /// <param name="body">Text the command was used with.</param>
    /// <returns>The parts to write, in the order they were typed.</returns>
    private static List<string> SplitIntoChannelMessages(string body)
    {
        var parts = new List<string>();
        var remaining = body.Trim();
        if (remaining.Length > MaximumCommandOptionLength)
        {
            remaining = remaining[..MaximumCommandOptionLength];
        }

        while (remaining.Length > MaximumChannelMessageLength)
        {
            var length = BreakAt(remaining, MaximumChannelMessageLength);
            parts.Add(remaining[..length]);
            remaining = remaining[length..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            parts.Add(remaining);
        }

        return parts;
    }

    /// <summary>Finds where the text may be cut without losing a word.</summary>
    /// <param name="text">Text being split.</param>
    /// <param name="limit">Longest part allowed.</param>
    /// <returns>Length of the first part.</returns>
    private static int BreakAt(string text, int limit)
    {
        // A boundary in the second half of the part is the one a reader would
        // have chosen; a boundary in the first half would throw away more text
        // than it keeps the message readable, so the limit is used instead.
        var window = text[..limit];
        var lineBreak = window.LastIndexOfAny(['\n', '\r']);
        if (lineBreak > limit / 2)
        {
            return lineBreak + 1;
        }

        var space = window.LastIndexOf(' ');
        return space > limit / 2 ? space + 1 : limit;
    }

    /// <summary>Reports whether the interaction carries the flash command.</summary>
    /// <param name="interaction">Interaction to look at.</param>
    private static bool IsFlashCommand(DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.FlashCommandName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Reports whether the interaction carries the message command.</summary>
    /// <param name="interaction">Interaction to look at.</param>
    private static bool IsMessageCommand(DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.MessageCommandName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reports whether the guild of the interaction is the configured one. An
    /// unconfigured guild lets every guild the bot is in through.
    /// </summary>
    /// <param name="guildIdentifier">Guild the interaction happened in.</param>
    private bool GuildMatches(string? guildIdentifier) =>
        string.IsNullOrWhiteSpace(options.GuildIdentifier) ||
        string.Equals(guildIdentifier, options.GuildIdentifier, StringComparison.Ordinal);

    /// <summary>Reads the text the command was used with.</summary>
    /// <param name="interaction">Interaction that carried the command.</param>
    /// <param name="optionName">Name of the option that carries the text.</param>
    private static string? MessageOf(DiscordInteraction interaction, string optionName)
    {
        var option = interaction.Data?.Options?
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                optionName,
                StringComparison.OrdinalIgnoreCase));

        return option?.Value?.ValueKind == JsonValueKind.String
            ? option.Value.Value.GetString()
            : null;
    }

    /// <summary>Answers the command.</summary>
    /// <param name="interactionIdentifier">Identifier of the interaction.</param>
    /// <param name="interactionToken">Token that authorizes the answer.</param>
    /// <param name="content">Text of the answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private Task ReplyAsync(
        string interactionIdentifier,
        string interactionToken,
        string content,
        CancellationToken cancellationToken) =>
        responder.RespondAsync(interactionIdentifier, interactionToken, content, cancellationToken);
}