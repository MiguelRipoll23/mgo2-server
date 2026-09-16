using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// Relays one ticker announcement to every connected gameplay lobby. It is the
/// single path a flash takes, whatever asked for it: the broadcast endpoints of
/// the API and the Discord command both end up here.
/// </summary>
/// <param name="registry">Registry of the connected lobbies.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class FlashNewsDispatcherService(
    LobbyConnectionRegistryService registry,
    ILogger<FlashNewsDispatcherService> logger)
{
    /// <summary>Sends an announcement to every connected lobby.</summary>
    /// <param name="announcement">Announcement to relay.</param>
    /// <returns>Number of lobbies the announcement was queued for.</returns>
    public int Dispatch(FlashNewsAnnouncement announcement)
    {
        var message = new HttpEvent
        {
            FlashNews = new FlashNewsBroadcast
            {
                Message = announcement.Message,
                Unknown1 = announcement.Unknown1,
                Unknown2 = announcement.Unknown2,
                Subcommand = announcement.Subcommand,
                Unknown5 = announcement.Unknown5,
                MaintenanceTime = announcement.MaintenanceTime,
            },
        };

        var recipients = registry.Broadcast(message);

        logger.LogInformation(
            "Relayed a {Subcommand} flash news to {Recipients} lobbies",
            announcement.Subcommand,
            recipients);

        return recipients;
    }
}
