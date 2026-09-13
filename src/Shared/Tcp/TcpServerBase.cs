using System.Net;
using System.Net.Sockets;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Shared.Tcp;

/// <summary>
/// Accepts TCP connections and runs the packet pipeline: accumulate bytes, cut
/// out complete packets, decode them and dispatch them to their handler.
/// </summary>
public abstract class TcpServerBase(IServiceProvider serviceProvider, int port)
{
    /// <summary>Upper 16 bits of the XOR key, used to peek at the command field.</summary>
    private const ushort ExclusiveOrCommandMask = (ushort)((CryptoKeyConstants.XorKey >> 16) & 0xffff);

    /// <summary>Lower 16 bits of the XOR key, used to peek at the payload length field.</summary>
    private const ushort ExclusiveOrPayloadLengthMask = (ushort)(CryptoKeyConstants.XorKey & 0xffff);

    private PacketCodecService? packetCodec;
    private CommandRegistry? commandRegistry;
    private ServerOptions? options;
    private ILogger? logger;
    private TcpListener? listener;

    /// <summary>Role of the server, which selects the command set it dispatches.</summary>
    protected abstract ServerType ServerType { get; }

    /// <summary>Container the server and its sessions resolve their dependencies from.</summary>
    protected IServiceProvider Services => serviceProvider;

    /// <summary>Port the server listens on.</summary>
    protected int Port { get; } = port;

    /// <summary>Prefix used for the log lines of this server.</summary>
    protected virtual string LogPrefix => $"tcp:{ServerType}";

    /// <summary>Codec used to encode and decode packets.</summary>
    protected PacketCodecService PacketCodec =>
        packetCodec ??= serviceProvider.GetRequiredService<PacketCodecService>();

    /// <summary>Registry the server dispatches through.</summary>
    protected CommandRegistry CommandRegistry =>
        commandRegistry ??= serviceProvider.GetRequiredService<CommandRegistry>();

    /// <summary>Options this server was configured with.</summary>
    protected ServerOptions Options =>
        options ??= serviceProvider.GetRequiredService<IOptions<ServerOptions>>().Value;

    /// <summary>Logger of this server.</summary>
    protected ILogger Logger =>
        logger ??= serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger($"Mgo2Server.{LogPrefix}");

    /// <summary>Starts accepting connections. Returns when the listener stops.</summary>
    /// <param name="cancellationToken">Token that stops the server.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        listener = new TcpListener(IPAddress.Parse(Options.ListeningIpAddress), Port);
        listener.Start();
        Logger.LogInformation("[{LogPrefix}] Listening on port {Port}", LogPrefix, Port);

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException exception)
            {
                Logger.LogError("[{LogPrefix}] Accept failed: {Message}", LogPrefix, exception.Message);
                continue;
            }

            _ = HandleConnectionAsync(client, cancellationToken);
        }
    }

    /// <summary>Stops accepting new connections. Existing sessions finish on their own.</summary>
    public void Stop()
    {
        listener?.Stop();
        listener = null;
    }

    /// <summary>Called when a session is created; overridden to track the lobby it belongs to.</summary>
    /// <param name="session">Created session.</param>
    protected virtual void OnSessionCreated(TcpSession session)
    {
    }

    /// <summary>Called when a session is destroyed; overridden to release the resources it held.</summary>
    /// <param name="session">Destroyed session.</param>
    protected virtual void OnSessionDestroyed(TcpSession session)
    {
    }


    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
        var remoteAddress = $"{remoteEndpoint.Address}:{remoteEndpoint.Port}";

        var session = new TcpSession
        {
            ServerType = ServerType,
            LogPrefix = LogPrefix,
            RemoteAddress = remoteAddress,
            Connection = client.GetStream(),
            SequenceIn = 0,
        };

        TrafficLogger.LogConnection(Logger, LogPrefix, remoteAddress);
        OnSessionCreated(session);

        try
        {
            await RunReadLoopAsync(session, cancellationToken);
        }
        catch (Exception exception) when (IsConnectionClosed(exception))
        {
            // The client went away mid-packet; nothing to report.
        }
        catch (Exception exception)
        {
            Logger.LogError("[{LogPrefix}] Error for {RemoteAddress}: {Message}", LogPrefix, remoteAddress, exception.Message);
        }
        finally
        {
            OnSessionDestroyed(session);
            TrafficLogger.LogDisconnection(Logger, LogPrefix, remoteAddress);
            client.Dispose();
        }
    }

    private async Task RunReadLoopAsync(TcpSession session, CancellationToken cancellationToken)
    {
        using var accumulated = new MemoryStream();
        var chunk = new byte[4096];
        // The read cursor is kept apart from the stream's write cursor, which
        // Write advances: the bytes before it have already been dispatched.
        var readOffset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await session.Connection.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            accumulated.Write(chunk, 0, bytesRead);
            readOffset = await ProcessAccumulatedBytesAsync(session, accumulated, readOffset, cancellationToken);
            if (readOffset < 0)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Consumes complete packets from the accumulated buffer and dispatches each
    /// one.
    /// </summary>
    /// <param name="session">Connection the bytes belong to.</param>
    /// <param name="accumulated">Buffered bytes, consumed in place.</param>
    /// <param name="readOffset">Offset the unconsumed bytes start at.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The next read offset, or <c>-1</c> when the connection should be closed.</returns>
    private async Task<int> ProcessAccumulatedBytesAsync(
        TcpSession session,
        MemoryStream accumulated,
        int readOffset,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var buffer = accumulated.GetBuffer();
            var available = (int)accumulated.Length - readOffset;
            if (available < PacketConstants.HeaderSize)
            {
                break;
            }

            // Peek at the payload length before full decryption.
            var rawPayloadLength = (ushort)((buffer[readOffset + PacketConstants.PayloadLengthOffset] << 8) |
                buffer[readOffset + PacketConstants.PayloadLengthOffset + 1]);
            var payloadLength = (ushort)(rawPayloadLength ^ ExclusiveOrPayloadLengthMask);
            var totalPacketLength = PacketConstants.HeaderSize + payloadLength;

            if (available < totalPacketLength)
            {
                break;
            }

            var packetBytes = buffer.AsSpan(readOffset, totalPacketLength).ToArray();
            readOffset += totalPacketLength;

            var packet = PacketCodec.DecodePacket(packetBytes);
            if (packet is null)
            {
                var rawCommand = (ushort)((packetBytes[0] << 8) | packetBytes[1]);
                var command = (ushort)(rawCommand ^ ExclusiveOrCommandMask);
                Logger.LogWarning(
                    "[{LogPrefix}] 0x{Command} failed reason=decode from {RemoteAddress}",
                    LogPrefix,
                    FormatCommand(command),
                    session.RemoteAddress);
                continue;
            }

            TrafficLogger.LogInboundPacket(Logger, session, packetBytes);
            session.SequenceIn++;

            if (!await DispatchPacketAsync(session, packet, cancellationToken))
            {
                return -1;
            }
        }

        return Compact(accumulated, readOffset);
    }

    /// <summary>
    /// Drops the consumed prefix of the accumulation buffer once it dominates
    /// it, so a long-lived connection does not keep growing it.
    /// </summary>
    /// <param name="accumulated">Buffer to compact in place.</param>
    /// <param name="readOffset">Offset the unconsumed bytes start at.</param>
    /// <returns>The new read offset.</returns>
    private static int Compact(MemoryStream accumulated, int readOffset)
    {
        if (readOffset == 0)
        {
            return 0;
        }

        if (readOffset == accumulated.Length)
        {
            accumulated.SetLength(0);
            return 0;
        }

        if (readOffset < 64 * 1024)
        {
            return readOffset;
        }

        var buffer = accumulated.GetBuffer();
        var remaining = (int)accumulated.Length - readOffset;
        Buffer.BlockCopy(buffer, readOffset, buffer, 0, remaining);
        accumulated.SetLength(remaining);
        accumulated.Position = remaining;
        return 0;
    }

    /// <summary>
    /// Routes a decoded packet to its handler.
    /// </summary>
    /// <param name="session">Connection the packet arrived on.</param>
    /// <param name="packet">Decoded packet.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><c>false</c> when the connection should be closed.</returns>
    private async Task<bool> DispatchPacketAsync(
        TcpSession session,
        Packet packet,
        CancellationToken cancellationToken)
    {
        var command = packet.Header.Command;

        if (command == CommandConstants.Disconnect)
        {
            Logger.LogDebug("[{LogPrefix}] 0x{Command} disconnect requested {State}", LogPrefix, FormatCommand(command), FormatSessionState(session));
            return false;
        }

        if (command == CommandConstants.KeepAlive)
        {
            await SendKeepAliveAsync(session, cancellationToken);
            Logger.LogDebug("[{LogPrefix}] 0x{Command} keepalive ack {State}", LogPrefix, FormatCommand(command), FormatSessionState(session));
            return true;
        }

        var handlerType = CommandRegistry.ResolveHandlerType(ServerType, command);
        if (handlerType is null)
        {
            // A client may send commands this server does not implement; that
            // is ordinary traffic rather than a fault, so it stays at debug.
            Logger.LogDebug("[{LogPrefix}] 0x{Command} no-handler {State}", LogPrefix, FormatCommand(command), FormatSessionState(session));
            return true;
        }

        var handler = (Interfaces.ICommandHandler)serviceProvider.GetRequiredService(handlerType);

        Logger.LogDebug("[{LogPrefix}] 0x{Command} processing {State}", LogPrefix, FormatCommand(command), FormatSessionState(session));

        try
        {
            await handler.HandleAsync(session, packet, cancellationToken);
            Logger.LogDebug("[{LogPrefix}] 0x{Command} ok {State}", LogPrefix, FormatCommand(command), FormatSessionState(session));
        }
        catch (Exception exception)
        {
            Logger.LogDebug(
                "[{LogPrefix}] 0x{Command} failed reason={Reason} {State}",
                LogPrefix,
                FormatCommand(command),
                exception.Message,
                FormatSessionState(session));
            throw;
        }

        return true;
    }

    /// <summary>Answers a keep-alive with a keep-alive carrying no payload.</summary>
    /// <param name="session">Connection to answer.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    protected async Task SendKeepAliveAsync(TcpSession session, CancellationToken cancellationToken = default)
    {
        var sequenceOut = session.NextSequenceOut();
        var bytes = PacketCodec.EncodePacket(
            CommandConstants.KeepAlive,
            [],
            sequenceOut,
            session.LogPrefix);

        await session.WriteAsync(bytes, cancellationToken);
    }

    private static string FormatCommand(ushort command) => command.ToString("x4");

    private static string FormatSessionState(TcpSession session) =>
        $"auth={(session.UserIdentifier is null ? "missing" : "ok")} " +
        $"userId={session.UserIdentifier?.ToString() ?? "none"} " +
        $"characterId={session.CharacterIdentifier?.ToString() ?? "none"} " +
        $"lobbyId={session.LobbyIdentifier?.ToString() ?? "none"} " +
        $"gameId={session.GameIdentifier?.ToString() ?? "none"}";

    private static bool IsConnectionClosed(Exception exception) =>
        exception is IOException or SocketException or ObjectDisposedException;
}
