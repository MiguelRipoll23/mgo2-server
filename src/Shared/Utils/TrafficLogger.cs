using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Logs the traffic of the TCP and UDP transports: connection life cycle,
/// inbound packets and outbound frames, rendered as hex with their ASCII form.
/// </summary>
public static class TrafficLogger
{
    private const int BytesPerLine = 16;

    /// <summary>Renders a buffer as space-separated hexadecimal bytes.</summary>
    /// <param name="buffer">Buffer to render.</param>
    public static string FormatHex(ReadOnlySpan<byte> buffer)
    {
        var text = Convert.ToHexStringLower(buffer);
        var parts = new string[(text.Length + 1) / 2];
        for (var index = 0; index < parts.Length; index++)
        {
            parts[index] = text.Substring(index * 2, Math.Min(2, text.Length - (index * 2)));
        }

        return string.Join(" ", parts);
    }

    /// <summary>Renders a buffer as hex with an ASCII column, sixteen bytes per line.</summary>
    /// <param name="buffer">Buffer to render.</param>
    public static string FormatHexAscii(ReadOnlySpan<byte> buffer)
    {
        var lines = new List<string>();

        for (var offset = 0; offset < buffer.Length; offset += BytesPerLine)
        {
            var chunk = buffer.Slice(offset, Math.Min(BytesPerLine, buffer.Length - offset));
            var hexText = FormatHex(chunk).PadRight(47, ' ');

            var ascii = new char[chunk.Length];
            for (var index = 0; index < chunk.Length; index++)
            {
                ascii[index] = chunk[index] is >= 0x20 and <= 0x7e ? (char)chunk[index] : '.';
            }

            lines.Add($"{offset:x4}  {hexText}  {new string(ascii)}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>Renders a command as the four hexadecimal digits the protocol is written in.</summary>
    /// <param name="command">Command to render.</param>
    public static string FormatCommand(ushort command) => command.ToString("x4");

    /// <summary>Renders a payload as hex, cut short so a large frame cannot flood the log.</summary>
    /// <param name="payload">Payload to render.</param>
    public static string FormatPayload(byte[] payload)
    {
        const int MaximumBytes = 64;
        if (payload.Length == 0)
        {
            return "-";
        }

        var hex = Convert.ToHexString(payload.AsSpan(0, Math.Min(MaximumBytes, payload.Length)));
        return payload.Length <= MaximumBytes ? hex : $"{hex}..({payload.Length} bytes)";
    }

    /// <summary>Renders what a session has proved and where it has got to.</summary>
    /// <param name="session">Session to render.</param>
    public static string FormatSessionState(TcpSession session) =>
        $"auth={(session.AccountIdentifier is null ? "missing" : "ok")} " +
        $"accountId={session.AccountIdentifier?.ToString() ?? "none"} " +
        $"characterId={session.CharacterIdentifier?.ToString() ?? "none"} " +
        $"lobbyId={session.LobbyIdentifier?.ToString() ?? "none"} " +
        $"gameId={session.GameIdentifier?.ToString() ?? "none"}";

    /// <summary>Logs a TCP client connecting.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="remoteAddress">Address of the client.</param>
    public static void LogConnection(ILogger logger, string logPrefix, string remoteAddress) =>
        logger.LogInformation("[{LogPrefix}] Client connected from {RemoteAddress}", logPrefix, remoteAddress);

    /// <summary>Logs a TCP client disconnecting.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="remoteAddress">Address of the client.</param>
    public static void LogDisconnection(ILogger logger, string logPrefix, string remoteAddress) =>
        logger.LogInformation("[{LogPrefix}] Client disconnected from {RemoteAddress}", logPrefix, remoteAddress);

    /// <summary>Logs an inbound TCP packet.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="session">Connection the packet arrived on.</param>
    /// <param name="buffer">Packet bytes.</param>
    public static void LogInboundPacket(ILogger logger, TcpSession session, ReadOnlySpan<byte> buffer)
    {
        // The wire carries the command exclusive-ORed with the upper half of the
        // packet key; the raw bytes are already shown, the label is the real one.
        var command = buffer.Length >= 2
            ? (ushort)(((buffer[0] << 8) | buffer[1]) ^ (ushort)(CryptoKeyConstants.XorKey >> 16))
            : (ushort)0;
        logger.LogDebug(
            "[{LogPrefix}] IN 0x{Command} ({Length} bytes): {Bytes}",
            session.LogPrefix,
            command.ToString("x4"),
            buffer.Length,
            FormatHex(buffer));
    }

    /// <summary>Logs a UDP datagram with its ASCII form.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="remoteAddress">Address of the peer.</param>
    /// <param name="direction">Direction of the datagram.</param>
    /// <param name="buffer">Datagram bytes.</param>
    public static void LogUdpTraffic(
        ILogger logger,
        string logPrefix,
        string remoteAddress,
        string direction,
        ReadOnlySpan<byte> buffer) =>
        logger.LogDebug(
            "[{LogPrefix}] {Direction} {RemoteAddress} {Length} bytes\n{Bytes}",
            logPrefix,
            direction,
            remoteAddress,
            buffer.Length,
            FormatHexAscii(buffer));

    /// <summary>Logs an inbound peer-to-peer frame.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="tag">Description of the frame.</param>
    /// <param name="buffer">Decrypted datagram.</param>
    public static void LogUdpInboundPacket(ILogger logger, string logPrefix, string tag, ReadOnlySpan<byte> buffer) =>
        logger.LogDebug(
            "[{LogPrefix}] IN {Tag} ({Length} bytes): {Bytes}",
            logPrefix,
            tag,
            buffer.Length,
            FormatHex(buffer));

    /// <summary>Logs an outbound peer-to-peer frame.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="tag">Description of the frame.</param>
    /// <param name="buffer">Plaintext frame.</param>
    public static void LogUdpOutboundPacket(ILogger logger, string logPrefix, string tag, ReadOnlySpan<byte> buffer) =>
        logger.LogDebug(
            "[{LogPrefix}] OUT {Tag} ({Length} bytes): {Bytes}",
            logPrefix,
            tag,
            buffer.Length,
            FormatHex(buffer));

    /// <summary>Logs a peer connecting.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="remoteAddress">Address of the peer.</param>
    public static void LogPeerConnection(ILogger logger, string logPrefix, string remoteAddress) =>
        logger.LogInformation("[{LogPrefix}] Peer connected from {RemoteAddress}", logPrefix, remoteAddress);

    /// <summary>Logs a peer disconnecting.</summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="logPrefix">Prefix of the server.</param>
    /// <param name="remoteAddress">Address of the peer.</param>
    public static void LogPeerDisconnection(ILogger logger, string logPrefix, string remoteAddress) =>
        logger.LogInformation("[{LogPrefix}] Peer disconnected from {RemoteAddress}", logPrefix, remoteAddress);
}
