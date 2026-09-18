using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.News;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Runs the commands the gateway delivers: the flash command relays its message
/// through the same dispatcher the broadcast endpoints of the API use, and the
/// interaction is answered so the command does not time out.
/// </summary>
/// <param name="responder">Service that answers the interactions.</param>
/// <param name="flashNewsDispatcher">Service every flash is relayed through.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordCommandService(
    IDiscordInteractionResponder responder,
    FlashNewsDispatcherService flashNewsDispatcher,
    IOptions<DiscordOptions> options,
    ILogger<DiscordCommandService> logger)
{
    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteractionType = 2;

    /// <summary>Name of the option that carries the text of a flash.</summary>
    private const string MessageOptionName = "message";

    private readonly DiscordOptions options = options.Value;

    /// <summary>Handles one interaction the gateway delivered.</summary>
    /// <param name="interaction">Interaction to answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task HandleInteractionAsync(
        DiscordInteraction interaction,
        CancellationToken cancellationToken)
    {
        if (!IsFlashCommand(interaction) || !GuildMatches(interaction.GuildIdentifier))
        {
            return;
        }

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
        if (!options.AllowsFlashCommand(roles))
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

        var message = MessageOf(interaction);
        if (string.IsNullOrWhiteSpace(message))
        {
            await ReplyAsync(
                interactionIdentifier,
                interactionToken,
                "The message option is required.",
                cancellationToken);
            return;
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

    /// <summary>Reports whether the interaction carries the flash command.</summary>
    /// <param name="interaction">Interaction to look at.</param>
    private static bool IsFlashCommand(DiscordInteraction interaction) =>
        interaction.Type == ApplicationCommandInteractionType &&
        string.Equals(
            interaction.Data?.Name,
            DiscordOptions.FlashCommandName,
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
    private static string? MessageOf(DiscordInteraction interaction)
    {
        var option = interaction.Data?.Options?
            .FirstOrDefault(candidate => string.Equals(
                candidate.Name,
                MessageOptionName,
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