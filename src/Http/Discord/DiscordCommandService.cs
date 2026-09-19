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
public sealed class DiscordCommandService(
    IDiscordInteractionResponder responder,
    IDiscordMessageService messageService,
    FlashNewsDispatcherService flashNewsDispatcher,
    IOptions<DiscordOptions> options,
    ILogger<DiscordCommandService> logger)
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
        // where the whole guild sees it. A text longer than Discord accepts is
        // trimmed to what the channel can hold.
        if (message.Length > MaximumChannelMessageLength)
        {
            message = message[..MaximumChannelMessageLength];
        }

        var sent = await messageService.SendChannelMessageAsync(
            channelIdentifier,
            message,
            cancellationToken);
        if (!sent)
        {
            logger.LogWarning(
                "The {Command} command could not write in the channel of the interaction",
                DiscordOptions.MessageCommandName);
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "The message could not be sent. Try again later.",
                cancellationToken);
            return;
        }

        logger.LogInformation(
            "The {Command} command wrote an official message in the channel {ChannelIdentifier}",
            DiscordOptions.MessageCommandName,
            channelIdentifier);

        await ReplyAsync(
            interactionIdentifier,
            interactionToken,
            "Official message sent in this channel.",
            cancellationToken);
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