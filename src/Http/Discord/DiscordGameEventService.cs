using Mgo2Server.Http.Options;
using Mgo2Server.Shared.Domain.Characters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Posts messages to the players Discord channel when a player creates or joins a
/// game, and when a player connects or disconnects.
/// </summary>
/// <remarks>
/// Character and game names are bold; lobby names are italic. No emoji is used, so
/// the channel reads as one consistent list rather than a stream of pictographs.
/// </remarks>
/// <param name="restClient">REST side of the integration.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordGameEventService(
    DiscordRestClientService restClient,
    IOptions<DiscordOptions> options,
    ILogger<DiscordGameEventService> logger)
{
    /// <summary>
    /// Whether the game events channel is configured (uses the players channel).
    /// </summary>
    public bool IsConfigured =>
        options.Value.Enabled &&
        !string.IsNullOrWhiteSpace(options.Value.BotToken) &&
        !string.IsNullOrWhiteSpace(options.Value.PlayerCountChannelIdentifier);

    /// <summary>
    /// Posts a message when a player creates a game.
    /// </summary>
    /// <param name="characterName">Name of the player who created the game.</param>
    /// <param name="gameName">Name of the created game.</param>
    /// <param name="lobbyName">Name of the lobby the game is in.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PostGameCreatedAsync(
        string characterName,
        string gameName,
        string lobbyName,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Discord game events not configured; skipping game created message");
            return;
        }

        var message = $"**{characterName}** created a game: **{gameName}** in lobby *{lobbyName}*";

        await SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Posts a message when a player joins a game.
    /// </summary>
    /// <param name="characterName">Name of the player who joined.</param>
    /// <param name="gameName">Name of the game joined.</param>
    /// <param name="lobbyName">Name of the lobby the game is in.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PostGameJoinedAsync(
        string characterName,
        string gameName,
        string lobbyName,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Discord game events not configured; skipping game joined message");
            return;
        }

        var message = $"**{characterName}** joined the game: **{gameName}** in lobby *{lobbyName}*";

        await SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Posts a message when a player connects.
    /// </summary>
    /// <param name="characterName">Name of the player who connected.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PostPlayerConnectedAsync(string characterName, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Discord game events not configured; skipping player connected message");
            return;
        }

        await SendMessageAsync($"**{characterName}** is online", cancellationToken);
    }

    /// <summary>
    /// Posts a message when a player disconnects.
    /// </summary>
    /// <param name="characterName">Name of the player who disconnected.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public async Task PostPlayerDisconnectedAsync(string characterName, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Discord game events not configured; skipping player disconnected message");
            return;
        }

        await SendMessageAsync($"**{characterName}** is offline", cancellationToken);
    }

    /// <summary>
    /// Sends a message to the game events channel.
    /// </summary>
    private async Task SendMessageAsync(string content, CancellationToken cancellationToken)
    {
        var channelId = options.Value.PlayerCountChannelIdentifier;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return;
        }

        try
        {
            var success = await restClient.SendChannelMessageAsync(channelId, content, cancellationToken);

            if (success)
            {
                logger.LogInformation("Posted game event to Discord channel {ChannelId}", channelId);
            }
            else
            {
                logger.LogWarning(
                    "Failed to post game event to Discord channel {ChannelId}",
                    channelId);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error posting game event to Discord channel {ChannelId}",
                channelId);
        }
    }
}
