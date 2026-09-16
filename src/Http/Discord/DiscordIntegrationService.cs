using Mgo2Server.Http.Coordination;
using Mgo2Server.Http.Options;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Brings the Discord integration up once the API is running: it registers the
/// flash command and finds or creates the channel the player count is published
/// in.
/// </summary>
/// <remarks>
/// It runs in the background and reports every failure itself. Discord is
/// optional, so a deployment whose token is wrong, whose bot has been removed
/// from the guild or whose Discord is unreachable must start and serve exactly
/// as one that never enabled the integration.
/// </remarks>
/// <param name="options">Options of the integration.</param>
/// <param name="restClient">Client of the Discord REST API.</param>
/// <param name="playerCount">Service that publishes the player count.</param>
/// <param name="presence">Counts the coordinator holds.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordIntegrationService(
    IOptions<DiscordOptions> options,
    DiscordRestClientService restClient,
    DiscordPlayerCountService playerCount,
    LobbyPresenceService presence,
    ILogger<DiscordIntegrationService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("The Discord integration is disabled");
            return Task.CompletedTask;
        }

        logger.LogInformation("The Discord integration is starting");

        // Not awaited: the API has to start whether or not Discord answers.
        _ = Task.Run(() => InitializeAsync(cancellationToken), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Registers the commands and prepares the channel of the count.</summary>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (options.Value.IsInteractionConfigured)
            {
                await restClient.RegisterFlashCommandAsync(cancellationToken);
            }
            else
            {
                logger.LogWarning(
                    "The Discord slash commands are not registered; DISCORD_BOT_TOKEN, DISCORD_APPLICATION_ID and DISCORD_PUBLIC_KEY are required");
            }

            if (!options.Value.IsPlayerCountConfigured)
            {
                logger.LogWarning(
                    "The Discord player count is not published; DISCORD_BOT_TOKEN and DISCORD_GUILD_ID are required");
                return;
            }

            await playerCount.InitializeAsync(presence.TotalPlayers, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The Discord integration could not be initialized");
        }
    }
}
