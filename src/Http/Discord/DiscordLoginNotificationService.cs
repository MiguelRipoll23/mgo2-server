using Mgo2Server.Http.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// Writes every login attempt of the game login endpoint in a dedicated Discord
/// channel: the account that was named, whether the credentials matched, and the
/// auth header the client presented.
/// </summary>
/// <remarks>
/// The header is the value the mgo2-plugin sends, which is what a fingerprint
/// that is being checked has to be compared against; the channel is where a
/// deployment reads it. A deployment that configured no channel keeps working
/// exactly as it did, and no attempt ever waits on Discord.
/// </remarks>
/// <param name="messageService">Messages the integration writes in a channel.</param>
/// <param name="options">Options of the integration.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class DiscordLoginNotificationService(
    IDiscordMessageService messageService,
    IOptions<DiscordOptions> options,
    ILogger<DiscordLoginNotificationService> logger)
{
    private readonly DiscordOptions options = options.Value;

    /// <summary>
    /// Reports one login attempt. It is a notification rather than a call: the
    /// login answers the client whether or not Discord is reachable, so the
    /// write is started and not waited on.
    /// </summary>
    /// <param name="accountName">Name the attempt tried to log in with.</param>
    /// <param name="authHeader">Auth header the client presented; empty when it carried none.</param>
    /// <param name="succeeded">Whether the credentials matched an account.</param>
    public void LoginAttempted(string accountName, string authHeader, bool succeeded)
    {
        if (!options.IsLoginLogConfigured)
        {
            return;
        }

        // The reply the client received is the outcome, so the same word is what
        // the channel shows: an attempt that failed is as worth reading as one
        // that succeeded. Fields are comma separated so the line parses without
        // the header being mistaken for part of the name.
        var content = $"{accountName},{(succeeded ? "ok" : "ko")},{authHeader}";

        // Not awaited: the latency of Discord is not the latency of a login, and
        // the write reports its own failure.
        _ = WriteAsync(content);
    }

    /// <summary>Writes one attempt in the channel of the integration.</summary>
    /// <param name="content">Text of the message.</param>
    private async Task WriteAsync(string content)
    {
        try
        {
            await messageService.SendChannelMessageAsync(
                options.LoginsChannelIdentifier,
                content,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "A login attempt could not be written in the Discord logins channel");
        }
    }
}
