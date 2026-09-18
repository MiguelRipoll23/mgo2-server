using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Discord;

namespace Mgo2Server.Http.Endpoints.Authenticated;

/// <summary>
/// The endpoint that drives the bot: it sends a message from the bot into a
/// channel of a guild. It is authenticated because writing in the guild is a
/// privileged action of the deployment.
/// </summary>
internal static class DiscordMessageEndpoints
{
    /// <summary>Maps the Discord message endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapDiscordMessageEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/discord/messages", SendMessage)
            .WithTags("Discord")
            .WithSummary("Send a Discord message")
            .WithDescription(
                "Writes a message from the bot into the channel of a guild. " +
                "The channel is named in the body, so the bot sends exactly where the caller asks.");
    }

    /// <summary>Writes one message in a channel of the guild.</summary>
    /// <param name="request">Channel and text of the message.</param>
    /// <param name="messenger">REST side of the integration, which writes the message.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private static async Task<IResult> SendMessage(
        DiscordMessageSendRequest request,
        IDiscordMessageService messenger,
        CancellationToken cancellationToken)
    {
        var sent = await messenger.SendChannelMessageAsync(
            request.ChannelIdentifier,
            request.Content,
            cancellationToken);

        return sent
            ? Results.Ok()
            : Results.StatusCode(StatusCodes.Status502BadGateway);
    }
}