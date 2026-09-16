using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.News;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Answers the interactions Discord delivers: it proves the request comes from
/// Discord, then runs the command it carries. The flash command is the one the
/// deployment offers, and it relays its message through the same dispatcher the
/// broadcast endpoints of the API use.
/// </summary>
/// <param name="flashNewsDispatcher">Service every flash is relayed through.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordInteractionService(
    FlashNewsDispatcherService flashNewsDispatcher,
    IOptions<DiscordOptions> options,
    ILogger<DiscordInteractionService> logger)
{
    /// <summary>Interaction Discord sends to prove the endpoint is alive.</summary>
    private const int PingInteraction = 1;

    /// <summary>Interaction of a command a member used.</summary>
    private const int ApplicationCommandInteraction = 2;

    /// <summary>Reply that acknowledges a ping.</summary>
    private const int PongResponse = 1;

    /// <summary>Reply that posts a message in the channel of the interaction.</summary>
    private const int ChannelMessageResponse = 4;

    /// <summary>Flag that shows a reply only to the member that triggered it.</summary>
    private const int EphemeralMessageFlag = 64;

    /// <summary>Name of the option that carries the text of a flash.</summary>
    private const string MessageOptionName = "message";

    private readonly DiscordOptions options = options.Value;

    /// <summary>Answers one interaction.</summary>
    /// <param name="body">Raw body of the request, exactly as it arrived.</param>
    /// <param name="signature">Value of the signature header.</param>
    /// <param name="timestamp">Value of the timestamp header.</param>
    public DiscordInteractionResult Handle(byte[] body, string? signature, string? timestamp)
    {
        if (!options.IsInteractionConfigured)
        {
            logger.LogWarning("An interaction arrived while the Discord integration is not configured");
            return new DiscordInteractionResult(StatusCodes.Status503ServiceUnavailable);
        }

        if (!DiscordSignatureVerifierUtils.Verify(body, signature, timestamp, options.ApplicationPublicKey))
        {
            logger.LogWarning("An interaction arrived with a signature that does not belong to the application");
            return new DiscordInteractionResult(StatusCodes.Status401Unauthorized);
        }

        DiscordInteraction? interaction;
        try
        {
            interaction = JsonSerializer.Deserialize<DiscordInteraction>(body);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "An interaction could not be read");
            return new DiscordInteractionResult(StatusCodes.Status400BadRequest);
        }

        if (interaction is null)
        {
            return new DiscordInteractionResult(StatusCodes.Status400BadRequest);
        }

        return interaction.Type switch
        {
            PingInteraction => Reply(new DiscordInteractionResponse(PongResponse)),
            ApplicationCommandInteraction => HandleCommand(interaction),
            _ => Reply(Ephemeral("This kind of interaction is not supported.")),
        };
    }

    /// <summary>Runs the one command the integration offers.</summary>
    /// <param name="interaction">Interaction that carried the command.</param>
    private DiscordInteractionResult HandleCommand(DiscordInteraction interaction)
    {
        if (!string.Equals(
                interaction.Data?.Name,
                DiscordOptions.FlashCommandName,
                StringComparison.OrdinalIgnoreCase))
        {
            return Reply(Ephemeral("This command is not supported."));
        }

        var roles = interaction.Member?.Roles ?? [];
        if (!options.AllowsFlashCommand(roles))
        {
            logger.LogWarning(
                "A member without the moderator or manager role used the {Command} command",
                DiscordOptions.FlashCommandName);
            return Reply(Ephemeral("You need the Moderator or Manager role to use this command."));
        }

        var message = MessageOf(interaction);
        if (string.IsNullOrWhiteSpace(message))
        {
            return Reply(Ephemeral("The message option is required."));
        }

        var recipients = flashNewsDispatcher.Dispatch(new FlashNewsAnnouncement(message));
        logger.LogInformation(
            "The {Command} command relayed a flash news to {Recipients} lobbies",
            DiscordOptions.FlashCommandName,
            recipients);

        return Reply(Ephemeral($"Flash news sent to {recipients} lobbies."));
    }

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

    /// <summary>Builds the reply of an interaction.</summary>
    /// <param name="response">Reply to deliver.</param>
    private static DiscordInteractionResult Reply(DiscordInteractionResponse response) =>
        new(StatusCodes.Status200OK, response);

    /// <summary>Builds a reply only the member that triggered it can see.</summary>
    /// <param name="content">Text of the reply.</param>
    private static DiscordInteractionResponse Ephemeral(string content) =>
        new(ChannelMessageResponse, new DiscordInteractionMessage(content, EphemeralMessageFlag));
}
