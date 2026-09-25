using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// The slash command registrations of the application. Discord only takes a
/// command over its REST API, so registering them is part of what the
/// WebSocket integration keeps doing on every start. They live apart from the
/// channel messages because they are a different shape of call: a command
/// carries a definition and answers nothing, while a message carries text.
/// </summary>
public sealed partial class DiscordRestClientService
{
    /// <summary>
    /// Registers the event scheduling command. Unlike the message commands it
    /// carries typed options rather than one block of text, because a schedule
    /// is a window and a field size rather than a sentence, and asking a
    /// moderator to express an epoch second as prose would be a way of getting
    /// it wrong.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the command.</returns>
    public Task<bool> RegisterEventScheduleCommandAsync(CancellationToken cancellationToken)
    {
        var applicationIdentifier = DiscordApplicationIdentifierUtils.FromBotToken(options.BotToken);
        if (applicationIdentifier is null)
        {
            logger.LogWarning(
                "The {Command} command is not registered; the bot token does not carry an application identifier",
                DiscordOptions.ScheduleCommandName);
            return Task.FromResult(false);
        }

        var command = new
        {
            name = DiscordOptions.ScheduleCommandName,
            description = "Schedules a Tournament or Survival event.",
            options = new object[]
            {
                new
                {
                    type = StringOptionType,
                    name = "action",
                    description = "list, create, update or withdraw.",
                    required = true,
                    choices = new[]
                    {
                        new { name = "list", value = "list" },
                        new { name = "create", value = "create" },
                        new { name = "update", value = "update" },
                        new { name = "withdraw", value = "withdraw" },
                    },
                },
                new
                {
                    type = IntegerOptionType,
                    name = "event",
                    description = "Identifier of the event.",
                    required = false,
                },
                new
                {
                    type = IntegerOptionType,
                    name = "mode",
                    description = "Lobby mode: 4 Survival, 3 Tournament, 10 registration.",
                    required = false,
                },
                new
                {
                    type = IntegerOptionType,
                    name = "teams",
                    description = $"Number of teams the field holds, at most {EventConstants.BracketMaximumEntrants}.",
                    required = false,
                },
                new
                {
                    type = IntegerOptionType,
                    name = "from",
                    description = "Epoch second the event is published from.",
                    required = false,
                },
                new
                {
                    type = IntegerOptionType,
                    name = "until",
                    description = "Epoch second it stops being published, or 0 for never.",
                    required = false,
                },
                new
                {
                    type = BooleanOptionType,
                    name = "enabled",
                    description = "Whether the event is published at all.",
                    required = false,
                },
            },
        };

        return SendAsync(
            HttpMethod.Post,
            CommandsPath(applicationIdentifier),
            command,
            "register the event command",
            cancellationToken) is not null
            ? Task.FromResult(true)
            : Task.FromResult(false);
    }

    /// <summary>
    /// Registers one command of the application. Discord only takes a command
    /// over its REST API, so this is the one setup call the WebSocket
    /// integration keeps.
    /// </summary>
    /// <remarks>
    /// The registration is a POST: Discord treats it as an upsert, so the same
    /// call creates the command once and updates it on every later start. The
    /// bulk overwrite the collection answers with expects a list of commands,
    /// and refuses a single one with 400.
    /// </remarks>
    /// <param name="name">Name the command is registered with.</param>
    /// <param name="description">Description Discord shows for the command.</param>
    /// <param name="optionDescription">Description of the option that carries the text.</param>
    /// <param name="optionName">Name of the option that carries the text.</param>
    /// <param name="maximumOptionLength">Longest text the option accepts.</param>
    /// <param name="callDescription">Description of the call, used in the log.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the command.</returns>
    private async Task<bool> RegisterCommandAsync(
        string name,
        string description,
        string optionDescription,
        string optionName,
        int maximumOptionLength,
        string callDescription,
        CancellationToken cancellationToken)
    {
        var applicationIdentifier = DiscordApplicationIdentifierUtils.FromBotToken(options.BotToken);
        if (applicationIdentifier is null)
        {
            logger.LogWarning(
                "The {Command} command is not registered; the bot token does not carry an application identifier",
                name);
            return false;
        }

        var command = new
        {
            name,
            description,
            options = new[]
            {
                new
                {
                    type = StringOptionType,
                    name = optionName,
                    description = optionDescription,
                    required = true,
                    max_length = maximumOptionLength,
                },
            },
        };

        // A guild command is registered against the guild and is available at
        // once; a global command may take an hour to appear, which would make
        // the integration look broken right after it was configured.
        return await SendAsync(
            HttpMethod.Post,
            CommandsPath(applicationIdentifier),
            command,
            callDescription,
            cancellationToken) is not null;
    }

    /// <summary>Builds the path commands are registered against.</summary>
    /// <param name="applicationIdentifier">Identifier of the application.</param>
    private string CommandsPath(string applicationIdentifier) =>
        string.IsNullOrWhiteSpace(options.GuildIdentifier)
            ? $"applications/{applicationIdentifier}/commands"
            : $"applications/{applicationIdentifier}/guilds/{options.GuildIdentifier}/commands";
}
