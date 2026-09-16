using Mgo2Server.Http.Contracts;
using Mgo2Server.Http.Coordination;
using Mgo2Server.Shared.Domain.News;

namespace Mgo2Server.Http.Endpoints.Authenticated;

/// <summary>The ticker broadcast endpoints of the authenticated API surface.</summary>
internal static class FlashNewsEndpoints
{
    /// <summary>Maps the ticker broadcast endpoints.</summary>
    /// <param name="group">Group the endpoints are added to.</param>
    public static void MapFlashNewsEndpoints(this RouteGroupBuilder group)
    {
        var flashNews = group.MapGroup("/flash-news")
            .WithTags("Flash news")
            .RequireAuthorization();

        flashNews.MapPost("/broadcast", Broadcast)
            .WithSummary("Broadcast flash news")
            .WithDescription(
                "Sends a ticker packet with the server-message subcommand to every game lobby, " +
                "which writes it to every active player. The message text is displayed in the client's ticker.");

        flashNews.MapPost("/emergency", BroadcastEmergency)
            .WithSummary("Broadcast emergency maintenance")
            .WithDescription(
                "Sends a ticker packet with the emergency-maintenance subcommand to every game lobby, " +
                "which writes it to every active player. " +
                "The client ignores the message text and displays a built-in emergency maintenance screen.");
    }

    /// <summary>Relays a ticker announcement to every connected game lobby.</summary>
    /// <param name="flashNewsDispatcher">Service every flash is relayed through.</param>
    /// <param name="request">Announcement to relay.</param>
    private static IResult Broadcast(
        FlashNewsDispatcherService flashNewsDispatcher,
        FlashNewsBroadcastRequest request)
    {
        // The lobbies own their sessions, so the API relays the announcement
        // instead of writing packets it holds no connection for. The Discord
        // command goes through the same call.
        flashNewsDispatcher.Dispatch(
            new FlashNewsAnnouncement(
                request.Message,
                request.Unknown1,
                request.Unknown2,
                FlashNewsSubcommand.ServerMessage,
                request.Unknown5,
                request.Unknown6));

        return Results.Accepted();
    }

    /// <summary>Relays an emergency maintenance announcement to every connected game lobby.</summary>
    /// <param name="flashNewsDispatcher">Service every flash is relayed through.</param>
    /// <param name="request">Announcement to relay.</param>
    private static IResult BroadcastEmergency(
        FlashNewsDispatcherService flashNewsDispatcher,
        FlashNewsEmergencyRequest request)
    {
        flashNewsDispatcher.Dispatch(
            new FlashNewsAnnouncement(
                string.Empty,
                request.Unknown1,
                request.Unknown2,
                FlashNewsSubcommand.EmergencyMaintenance,
                request.Unknown5,
                request.MaintenanceTime));

        return Results.Accepted();
    }
}
