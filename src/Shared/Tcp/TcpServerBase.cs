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

    private readonly Dictionary<string, TcpSession> sessions = [];
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

    /// <summary>Connections currently accepted by this server.</summary>
    protected IReadOnlyCollection<TcpSession> Sessions => sessions.Values;

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
            SequenceOut = 1,
        };

        sessions[remoteAddress] = session;
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
            sessions.Remove(remoteAddress);
            OnSessionDestroyed(session);
            TrafficLogger.LogDisconnection(Logger, LogPrefix, remoteAddress);
            client.Dispose();
        }
    }

    private async Task RunReadLoopAsync(TcpSession session, CancellationToken cancellationToken)
    {
        var accumulated = new List<byte>();
        var chunk = new byte[4096];

        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await session.Connection.ReadAsync(chunk, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            accumulated.AddRange(chunk.AsSpan(0, bytesRead));
            if (!await ProcessAccumulatedBytesAsync(session, accumulated, cancellationToken))
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
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns><c>false</c> when the connection should be closed.</returns>
    private async Task<bool> ProcessAccumulatedBytesAsync(
        TcpSession session,
        List<byte> accumulated,
        CancellationToken cancellationToken)
    {
        while (accumulated.Count >= PacketConstants.HeaderSize)
        {
            // Peek at the payload length before full decryption.
            var rawPayloadLength = (ushort)((accumulated[PacketConstants.PayloadLengthOffset] << 8) |
                accumulated[PacketConstants.PayloadLengthOffset + 1]);
            var payloadLength = (ushort)(rawPayloadLength ^ ExclusiveOrPayloadLengthMask);
            var totalPacketLength = PacketConstants.HeaderSize + payloadLength;

            if (accumulated.Count < totalPacketLength)
            {
                break;
            }

            var packetBytes = accumulated.GetRange(0, totalPacketLength).ToArray();
            accumulated.RemoveRange(0, totalPacketLength);

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
                return false;
            }
        }

        return true;
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
            Logger.LogWarning("[{LogPrefix}] 0x{Command} no-handler {State}", LogPrefix, FormatCommand(command), FormatSessionState(session));
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
        var bytes = PacketCodec.EncodePacket(
            CommandConstants.KeepAlive,
            [],
            session.SequenceOut,
            session.LogPrefix);

        session.SequenceOut++;
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
