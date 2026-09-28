using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Coordination;

/// <summary>
/// Acts on everything the coordinator sends down this lobby's stream.
/// <para>
/// A gameplay lobby knows nothing about Discord or about HTTP: the message is
/// the protocol, and the payload of a ticker packet is rebuilt here because the
/// client protocol stays where the protocol is.
/// </para>
/// </summary>
/// <param name="flashNewsService">Service that writes an announcement to this lobby's clients.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyCommandApplyService(
    FlashNewsService flashNewsService,
    ILogger<LobbyCommandApplyService> logger)
{
    /// <summary>Applies one message the coordinator sent.</summary>
    /// <param name="message">Message to act on.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    public Task ApplyAsync(HttpEvent message, CancellationToken cancellationToken)
    {
        switch (message.EventCase)
        {
            case HttpEvent.EventOneofCase.FlashNews:
                return WriteFlashNewsAsync(message.FlashNews, cancellationToken);

            default:
                logger.LogDebug("The coordinator sent an unknown {EventCase}; ignored", message.EventCase);
                return Task.CompletedTask;
        }
    }

    /// <summary>Writes one relayed announcement to the clients of this lobby.</summary>
    /// <param name="broadcast">Message the coordinator sent.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private async Task WriteFlashNewsAsync(FlashNewsBroadcast broadcast, CancellationToken cancellationToken)
    {
        try
        {
            var result = await flashNewsService.BroadcastAsync(
                new FlashNewsAnnouncement(
                    broadcast.Message,
                    (byte)broadcast.Unknown1,
                    (byte)broadcast.Unknown2,
                    (ushort)broadcast.Subcommand,
                    (byte)broadcast.Unknown5,
                    (byte)broadcast.MaintenanceTime),
                cancellationToken);

            logger.LogInformation(
                "Relayed flash news reached {Recipients} clients of this lobby",
                result.Recipients);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // One client that cannot be written to must not end the stream of
            // the whole lobby.
            logger.LogError(exception, "A relayed flash news could not be written to the clients");
        }
    }
}
