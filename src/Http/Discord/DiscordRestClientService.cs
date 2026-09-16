using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// The Discord REST API, as far as the integration needs it: the channels the
/// player count is published in, the commands the application offers and the
/// replies to the interactions it receives.
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
    ILogger<DiscordRestClientService> logger)
{
    /// <summary>Channel type of a text channel, which is what the count is published in.</summary>
    private const int TextChannelType = 0;

    /// <summary>Option type of a free-text command option.</summary>
    private const int StringOptionType = 3;

    /// <summary>Serializer the request bodies and the channel responses are read and written with.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly DiscordOptions options = options.Value;

    /// <summary>Registers the flash command of the application.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> RegisterFlashCommandAsync(CancellationToken cancellationToken)
    {
        var command = new
        {
            name = DiscordOptions.FlashCommandName,
            description = "Sends flash news to every game lobby.",
            options = new[]
            {
                new
                {
                    type = StringOptionType,
                    name = "message",
                    description = "Text shown in the ticker of every connected client.",
                    required = true,
                },
            },
        };

        // A guild command is registered against the guild and is available at
        // once; a global command may take an hour to appear, which would make
        // the integration look broken right after it was configured.
        var path = string.IsNullOrWhiteSpace(options.GuildIdentifier)
            ? $"applications/{options.ApplicationIdentifier}/commands"
            : $"applications/{options.ApplicationIdentifier}/guilds/{options.GuildIdentifier}/commands";

        return await SendAsync(
            HttpMethod.Put,
            path,
            command,
            "register the flash command",
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

    /// <summary>Creates the channel the player count is published in.</summary>
    /// <param name="name">Name the channel is created with.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The identifier of the channel, or <c>null</c> when it was not created.</returns>
    public async Task<string?> CreatePlayerCountChannelAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"guilds/{options.GuildIdentifier}/channels",
            new { name, type = TextChannelType },
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

    /// <summary>Renames a channel.</summary>
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
            new { name },
            $"rename channel {channelIdentifier}",
            cancellationToken) is not null;

    /// <summary>Writes a message in a channel.</summary>
    /// <param name="channelIdentifier">Identifier of the channel.</param>
    /// <param name="content">Text of the message.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> SendChannelMessageAsync(
        string channelIdentifier,
        string content,
        CancellationToken cancellationToken) =>
        await SendAsync(
            HttpMethod.Post,
            $"channels/{channelIdentifier}/messages",
            new
            {
                content,

                // A character name is player input, so it is never allowed to
                // ping a role or everyone in the guild.
                allowed_mentions = new { parse = Array.Empty<string>() },
            },
            $"write in channel {channelIdentifier}",
            cancellationToken) is not null;

    /// <summary>Answers an interaction through the token it arrived with.</summary>
    /// <param name="interactionIdentifier">Identifier of the interaction.</param>
    /// <param name="interactionToken">Token of the interaction.</param>
    /// <param name="response">Reply to deliver.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task<bool> RespondToInteractionAsync(
        string interactionIdentifier,
        string interactionToken,
        DiscordInteractionResponse response,
        CancellationToken cancellationToken) =>
        await SendAsync(
            HttpMethod.Post,
            $"interactions/{interactionIdentifier}/{interactionToken}/callback",
            response,
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
