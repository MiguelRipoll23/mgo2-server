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
    /// is a window and a field size rather than a sentence.
    /// <para>
    /// Every option is a word or a clock time a moderator can read. The command
    /// used to ask for an event identifier and a pair of epoch seconds, which
    /// put two numbers nobody can check in front of the person scheduling the
    /// event; both are now the name the event was given and the hours it runs.
    /// </para>
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
            description = "Schedules a Survival or Tournament event.",
            options = new object[]
            {
                new
                {
                    type = StringOptionType,
                    name = "action",
                    description = "What to do with the event.",
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
                    type = StringOptionType,
                    name = "event",
                    description = "Name of the event, for example Survival Night 3. Run list to see them.",
                    required = false,
                    max_length = EventScheduleNameService.MaximumLength,
                },
                new
                {
                    type = StringOptionType,
                    name = "mode",
                    description = "Which lobby the event is played in.",
                    required = false,
                    choices = new[]
                    {
                        new { name = "Survival", value = "survival" },
                        new { name = "Tournament", value = "tournament" },
                        new { name = "Registration", value = "registration" },
                    },
                },
                new
                {
                    type = IntegerOptionType,
                    name = "teams",
                    description = $"How many teams fit in the event, up to {EventConstants.BracketMaximumEntrants}.",
                    required = false,
                    min_value = 1,
                    max_value = EventConstants.BracketMaximumEntrants,
                },
                new
                {
                    type = StringOptionType,
                    name = "from",
                    description = "Hour the event opens, like 20:00. An hour already past means tomorrow.",
                    required = false,
                },
                new
                {
                    type = StringOptionType,
                    name = "until",
                    description = "Hour the event closes, like 23:00, or never.",
                    required = false,
                },
                new
                {
                    type = BooleanOptionType,
                    name = "enabled",
                    description = "Whether players can see and enter the event.",
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
    /// Registers the fake player command. Its options are a lobby, a count and
    /// a team name, because that is all a moderator needs to put players into an
    /// event they are about to look at.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the command.</returns>
    public Task<bool> RegisterFakePlayerCommandAsync(CancellationToken cancellationToken)
    {
        var applicationIdentifier = DiscordApplicationIdentifierUtils.FromBotToken(options.BotToken);
        if (applicationIdentifier is null)
        {
            logger.LogWarning(
                "The {Command} command is not registered; the bot token does not carry an application identifier",
                DiscordOptions.FakePlayerCommandName);
            return Task.FromResult(false);
        }

        var command = new
        {
            name = DiscordOptions.FakePlayerCommandName,
            description = "Fills a Survival or Tournament lobby with players who are only in memory.",
            options = new object[]
            {
                new
                {
                    type = StringOptionType,
                    name = "mode",
                    description = "Which lobby to put the players in.",
                    required = true,
                    choices = new[]
                    {
                        new { name = "Survival", value = "survival" },
                        new { name = "Tournament", value = "tournament" },
                    },
                },
                new
                {
                    type = IntegerOptionType,
                    name = "count",
                    description = "How many players to add, from 1 to 24.",
                    required = true,
                    min_value = 1,
                    max_value = FakePlayerDispatchService.MaximumCount,
                },
                new
                {
                    type = StringOptionType,
                    name = "team",
                    description = "Name to give the team they are put in. Leave blank for a made-up one.",
                    required = false,
                    max_length = 16,
                },
            },
        };

        return SendAsync(
            HttpMethod.Post,
            CommandsPath(applicationIdentifier),
            command,
            "register the fake player command",
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
