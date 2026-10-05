using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Announces the games a player opens and enters in the players Discord
/// channel, the same channel the player count is published in.
/// </summary>
/// <remarks>
/// It observes the coordinator rather than being called by it, which is why a
/// gameplay lobby never reaches Discord: the lobby reports a game over its
/// coordination stream and this is the destination that writes it down.
/// Character and game names are bold; lobby names are italic. No emoji is used,
/// so the channel reads as one consistent list rather than a stream of
/// pictographs.
/// </remarks>
/// <param name="restClient">REST side of the integration.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordGameEventService(
    DiscordRestClientService restClient,
    IOptions<DiscordOptions> options,
    ILogger<DiscordGameEventService> logger) : IGameActivityObserver
{
    /// <summary>
    /// Whether the game events channel is configured (uses the players channel).
    /// </summary>
    private bool IsConfigured =>
        options.Value.Enabled &&
        !string.IsNullOrWhiteSpace(options.Value.BotToken) &&
        !string.IsNullOrWhiteSpace(options.Value.PlayerCountChannelIdentifier);

    /// <inheritdoc />
    public Task GameActivityReportedAsync(
        GameActivityNotification notification,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Discord game events not configured; skipping game announcement");
            return Task.CompletedTask;
        }

        var message = notification.Created
            ? $"**{notification.CharacterName}** created a game: **{notification.GameName}** in lobby *{notification.LobbyName}*"
            : $"**{notification.CharacterName}** joined the game: **{notification.GameName}** in lobby *{notification.LobbyName}*";

        return SendMessageAsync(message, cancellationToken);
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
