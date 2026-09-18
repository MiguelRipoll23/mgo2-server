using System.Net.WebSockets;
using System.Text.Json;
using Mgo2Server.Http.Contracts;

namespace Mgo2Server.Http.Discord;

/// <summary>
/// The framing of the Discord gateway: how one frame of the socket is read and
/// written, and how the data of a dispatch is bound.
/// </summary>
public static class DiscordGatewayStreamUtils
{
    /// <summary>Serializer the gateway frames and their data are read and written with.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads one frame, joining the fragments of a large one.</summary>
    /// <param name="socket">Socket the frame is read from.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>The frame, or <c>null</c> when the gateway closed the connection.</returns>
    public static async Task<GatewayFrame?> ReceiveAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        using var payload = new MemoryStream();
        var buffer = new byte[8192];

        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            await payload.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
        }
        while (!result.EndOfMessage);

        if (payload.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<GatewayFrame>(payload.ToArray(), SerializerOptions);
    }

    /// <summary>Sends one request to the gateway.</summary>
    /// <param name="socket">Socket the request is sent over.</param>
    /// <param name="payload">Payload to serialize.</param>
    /// <param name="cancellationToken">Token that cancels the send.</param>
    public static async Task SendAsync(
        ClientWebSocket socket,
        object payload,
        CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    /// <summary>Reads the data of a frame, or <c>null</c> when it carries none.</summary>
    /// <typeparam name="T">Type the data is read into.</typeparam>
    /// <param name="frame">Frame whose data is read.</param>
    public static T? ReadData<T>(GatewayFrame frame) =>
        frame.Data is { } data ? data.Deserialize<T>(SerializerOptions) : default;
}