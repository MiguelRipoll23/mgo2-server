using System.Text;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GateLobbyServer.Commands;

/// <summary>
/// Serves the news articles to a client that just connected to the gate: an
/// opening marker, one packet per article, then a closing marker.
/// </summary>
/// <param name="newsService">Service that owns the articles.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetNewsHandler(
    NewsService newsService,
    SessionHelper sessionHelper,
    ILogger<GetNewsHandler> logger) : ICommandHandler
{
    /// <summary>Field length of an article topic.</summary>
    private const int NewsTopicLength = 128;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        try
        {
            await sessionHelper.SendStartEndPacketAsync(session, 0x2009, cancellationToken);

            var articles = await newsService.FindAllAsync(cancellationToken);
            foreach (var article in articles)
            {
                var payload = BuildNewsPayload(article);
                if (payload.Length > PacketConstants.MaximumPayloadLength)
                {
                    throw new InvalidOperationException(
                        $"news item {article.Identifier} exceeds max payload size " +
                        $"({payload.Length} > {PacketConstants.MaximumPayloadLength})");
                }

                await sessionHelper.SendPacketAsync(session, 0x200a, payload, cancellationToken);
            }

            await sessionHelper.SendStartEndPacketAsync(session, 0x200b, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "getNews error");
            await sessionHelper.SendErrorAsync(session, 0x2009, ErrorCodeConstants.ErrorGeneral, cancellationToken);
        }
    }

    /// <summary>Builds the payload of one article.</summary>
    /// <param name="article">Article to encode.</param>
    private static byte[] BuildNewsPayload(NewsArticle article)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32((uint)article.Identifier);
        writer.WriteUInt8(article.Important ? 1 : 0);
        writer.WriteUInt32((uint)article.Time);
        writer.WriteFixedString(article.Topic, NewsTopicLength);
        writer.WriteBytes(Encoding.UTF8.GetBytes(article.Message));
        writer.WriteUInt8(0);
        return writer.Build();
    }
}
