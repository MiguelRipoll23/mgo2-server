using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Sends messages into the channels of a guild through the Discord REST API.
/// It is one of the two outbound calls of the integration; the slash commands
/// and every event travel over the gateway socket instead.
/// </summary>
/// <param name="channelIdentifier">Identifier of the channel.</param>
/// <param name="content">Text of the message.</param>
/// <param name="cancellationToken">Token that cancels the operation.</param>
/// <returns>Whether Discord accepted the message.</returns>
public interface IDiscordMessageService
{
    Task<bool> SendChannelMessageAsync(
        string channelIdentifier,
        string content,
        CancellationToken cancellationToken);
}

/// <summary>Answers the interactions the gateway delivers, over the REST API.</summary>
public interface IDiscordInteractionResponder
{
    /// <summary>Answers one interaction, so the command that carried it does not time out.</summary>
    /// <param name="interactionIdentifier">Identifier of the interaction.</param>
    /// <param name="interactionToken">Token that authorizes the answer.</param>
    /// <param name="content">Text of the answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the answer.</returns>
    Task<bool> RespondAsync(
        string interactionIdentifier,
        string interactionToken,
        string content,
        CancellationToken cancellationToken);
}

/// <summary>
/// The REST side of the Discord integration, as far as it needs it: the channel
/// the player count is published in and the channel messages the bot writes.
/// Discord does not deliver these over its gateway socket, so they are the one
/// HTTP call of the deployment.
/// </summary>
/// <remarks>
/// Every call reports its own failure instead of throwing one. Discord being
/// unavailable, rate limited or refusing a call is a fact of the deployment,
/// not an error of the API that relays a flash or counts a player.
/// </remarks>
/// <param name="httpClientFactory">Factory the HTTP client is created with.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordRestClientService(
    IHttpClientFactory httpClientFactory,
    IOptions<DiscordOptions> options,
    ILogger<DiscordRestClientService> logger) : IDiscordMessageService, IDiscordInteractionResponder
{
    /// <summary>Channel type of a text channel, which is what the count is published in.</summary>
    private const int TextChannelType = 0;

    /// <summary>Option type of a free-text command option.</summary>
    private const int StringOptionType = 3;

    /// <summary>Response that writes a message in the channel of the interaction.</summary>
    private const int ChannelMessageResponseType = 4;

    /// <summary>Flag that shows the response only to the member that used the command.</summary>
    private const int EphemeralResponseFlag = 64;

    /// <summary>Longest message the command accepts, which is what the ticker of the game holds.</summary>
    private const int MaximumMessageOptionLength = 255;

    /// <summary>Longest channel message Discord accepts.</summary>
    private const int MaximumChannelMessageLength = 2000;

    /// <summary>Serializer the request bodies and the channel responses are read and written with.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly DiscordOptions options = options.Value;

    /// <summary>
    /// Registers the flash command of the application. Discord only takes a
    /// command over its REST API, so this is one of the setup calls the
    /// WebSocket integration keeps.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the command.</returns>
    public Task<bool> RegisterFlashCommandAsync(CancellationToken cancellationToken) =>
        RegisterCommandAsync(
            DiscordOptions.FlashCommandName,
            "Sends flash news to every game lobby.",
            "Text shown in the ticker of every connected client.",
            "message",
            MaximumMessageOptionLength,
            "register the flash command",
            cancellationToken);

    /// <summary>
    /// Registers the message command of the application. Discord only takes a
    /// command over its REST API, so this is one of the setup calls the
    /// WebSocket integration keeps.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>Whether Discord accepted the command.</returns>
    public Task<bool> RegisterMessageCommandAsync(CancellationToken cancellationToken) =>
        RegisterCommandAsync(
            DiscordOptions.MessageCommandName,
            "Sends an official message from the bot into this channel.",
            "Text of the official message.",
            "body",
            MaximumChannelMessageLength,
            "register the message command",
            cancellationToken);

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
        var path = string.IsNullOrWhiteSpace(options.GuildIdentifier)
            ? $"applications/{applicationIdentifier}/commands"
            : $"applications/{applicationIdentifier}/guilds/{options.GuildIdentifier}/commands";

        return await SendAsync(
            HttpMethod.Post,
            path,
            command,
            callDescription,
            cancellationToken) is not null;
    }

    /// <summary>Lists the channels of the guild.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The channels, or <c>null</c> when they could not be read.</returns>
    public async Task<List<DiscordChannel>?> ListGuildChannelsAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            HttpMethod.Get,
            $"guilds/{options.GuildIdentifier}/channels",
            null,
            "list the guild channels",
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<List<DiscordChannel>>(
                SerializerOptions,
                cancellationToken) ?? [];
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning(exception, "The channel list of the guild could not be read");
            return null;
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>Creates a text channel in the guild.</summary>
    /// <param name="name">Name the channel is created with.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The identifier of the channel, or <c>null</c> when it was not created.</returns>
    public async Task<string?> CreateChannelAsync(string name, CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"guilds/{options.GuildIdentifier}/channels",
            new DiscordChannelCreateBody(name, TextChannelType),
            "create the player count channel",
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        try
        {
            var channel = await response.Content.ReadFromJsonAsync<DiscordChannel>(
                SerializerOptions,
                cancellationToken);
            return channel?.Identifier;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            logger.LogWarning(exception, "The created channel could not be read");
            return null;
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Renames a channel. The body carries the name only: the type of the
    /// modify endpoint is reserved for a text to announcement conversion, and
    /// any other value, a null among them, Discord refuses with 400.
    /// </summary>
    /// <param name="channelIdentifier">Identifier of the channel.</param>
    /// <param name="name">Name to give it.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> RenameChannelAsync(
        string channelIdentifier,
        string name,
        CancellationToken cancellationToken) =>
        await SendAsync(
            HttpMethod.Patch,
            $"channels/{channelIdentifier}",
            new DiscordChannelEditBody(name),
            $"rename channel {channelIdentifier}",
            cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<bool> SendChannelMessageAsync(
        string channelIdentifier,
        string content,
        CancellationToken cancellationToken) =>
        await SendAsync(
            HttpMethod.Post,
            $"channels/{channelIdentifier}/messages",
            new DiscordChannelMessageBody(
                content,
                // A command option or a moderator is player input, so a message
                // is never allowed to ping a role or everyone in the guild.
                new DiscordAllowedMentions(Parse: [])),
            $"write in channel {channelIdentifier}",
            cancellationToken) is not null;

    /// <inheritdoc />
    public async Task<bool> RespondAsync(
        string interactionIdentifier,
        string interactionToken,
        string content,
        CancellationToken cancellationToken) =>
        await SendAsync(
            HttpMethod.Post,
            $"interactions/{interactionIdentifier}/{interactionToken}/callback",
            new DiscordInteractionResponse(
                ChannelMessageResponseType,
                // The answer is the feedback of the command, not a message of
                // the guild, so it is shown only to the member that used it.
                new DiscordInteractionResponseData(content, EphemeralResponseFlag)),
            "answer an interaction",
            cancellationToken) is not null;

    /// <summary>
    /// Sends one call and reports whether Discord accepted it. Every failure is
    /// logged and answered with <c>null</c>, so no caller has to handle one.
    /// </summary>
    /// <param name="method">Method of the call.</param>
    /// <param name="path">Path of the call, below the API base URL.</param>
    /// <param name="payload">Body of the call, when it carries one.</param>
    /// <param name="description">Description of the call, used in the log.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task<HttpResponseMessage?> SendAsync(
        HttpMethod method,
        string path,
        object? payload,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", options.BotToken);
            request.Headers.TryAddWithoutValidation("User-Agent", options.UserAgent);

            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload, options: SerializerOptions);
            }

            var client = httpClientFactory.CreateClient(nameof(DiscordRestClientService));

            // The trailing slash matters: without it the last path segment of
            // the base URL is replaced instead of extended, and every call
            // would leave out the version of the API.
            client.BaseAddress = new Uri($"{options.ApiBaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMilliseconds);

            var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            // A rate limited rename is expected: Discord allows a handful of
            // renames per channel and the count moves more often than that.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Discord refused to {Description} with {StatusCode}: {Body}",
                description,
                (int)response.StatusCode,
                body.Length > 512 ? body[..512] : body);

            response.Dispose();
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            logger.LogWarning(exception, "Discord could not be reached to {Description}", description);
            return null;
        }
    }
}
