using Mgo2Server.Http.Contracts;
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

        flashNews.MapPost("/broadcast", BroadcastAsync)
            .WithSummary("Broadcast a server message")
            .WithDescription(
                "Sends a ticker packet with the server-message subcommand to every active player. " +
                "The message text is displayed in the client's ticker.");

        flashNews.MapPost("/emergency", BroadcastEmergencyAsync)
            .WithSummary("Broadcast an emergency maintenance notice")
            .WithDescription(
                "Sends a ticker packet with the emergency-maintenance subcommand to every active player. " +
                "The client ignores the message text and displays a built-in emergency maintenance screen.");
    }

    private static async Task<IResult> BroadcastAsync(
        FlashNewsService flashNewsService,
        FlashNewsBroadcastRequest request,
        CancellationToken cancellationToken)
    {
        await flashNewsService.BroadcastAsync(
            new FlashNewsAnnouncement(
                request.Message,
                request.Unknown1,
                request.Unknown2,
                FlashNewsSubcommand.ServerMessage,
                request.Unknown5,
                request.Unknown6),
            cancellationToken);

        return Results.Accepted();
    }

    private static async Task<IResult> BroadcastEmergencyAsync(
        FlashNewsService flashNewsService,
        FlashNewsEmergencyRequest request,
        CancellationToken cancellationToken)
    {
        await flashNewsService.BroadcastAsync(
            new FlashNewsAnnouncement(
                string.Empty,
                request.Unknown1,
                request.Unknown2,
                FlashNewsSubcommand.EmergencyMaintenance,
                request.Unknown5,
                request.MaintenanceTime),
            cancellationToken);

        return Results.Accepted();
    }
}
