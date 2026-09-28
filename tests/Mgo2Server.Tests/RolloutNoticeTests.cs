using System.Net;
using System.Net.Sockets;
using Mgo2Server.GameLobbyServer.Servers;
using Mgo2Server.Shared.Domain.News;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mgo2Server.Tests;

/// <summary>
/// A lobby that is being replaced by a rollout tells the players it is still
/// holding that the rollout is coming, so that one who is disconnected by it
/// knows to come back instead of assuming the lobby threw them out. The other
/// half is the half that matters as much: a lobby stopped by hand is not a
/// rollout, and its players are not told about one.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class RolloutNoticeTests
{
    /// <summary>Command the ticker is written with, as the client reads it.</summary>
    private const ushort FlashNewsCommand = 0x4a50;

    /// <summary>How long a step of a test waits for the lobby to write.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static readonly PacketCodecService Codec =
        new(NullLogger<PacketCodecService>.Instance);

    [Fact]
    public async Task ThePlayersStillConnectedAreToldToReconnect()
    {
        var sessions = new ActiveGameSessionsService();
        var notice = CreateNotice(sessions);
        notice.RecordPlatformStop();

        using var player = await ConnectedPlayer.ConnectAsync(sessions);

        Assert.Equal(1, await notice.AnnounceAsync());

        var packet = await player.ReadPacketAsync();

        Assert.NotNull(packet);
        Assert.Equal(FlashNewsCommand, packet.Header.Command);

        // The payload is the ticker the client displays, byte for byte, rather
        // than a packet of the same command that happens to carry text.
        Assert.Equal(
            FlashNewsService.BuildPayload(new FlashNewsAnnouncement(RolloutNoticeService.Message)),
            packet.Payload);
    }

    /// <summary>
    /// The notice is a promise about time, so the time it names is pinned to the
    /// grace period every deployment in <c>deploy/</c> gives a pod
    /// (<c>terminationGracePeriodSeconds: 21600</c>): six hours is how long a
    /// drained lobby is kept, so six hours is what the players are told.
    /// </summary>
    [Fact]
    public void TheNoticeNamesTheHoursTheRolloutTakes()
    {
        Assert.Contains("rollout", RolloutNoticeService.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("6 hours", RolloutNoticeService.Message);
        Assert.Contains("reconnect", RolloutNoticeService.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ALobbyStoppedByHandAnnouncesNothing()
    {
        var sessions = new ActiveGameSessionsService();
        var notice = CreateNotice(sessions);

        // Nobody recorded a platform stop, so this instance is a person's stop
        // and not a rollout — even with a player connected to hear about one.
        using var player = await ConnectedPlayer.ConnectAsync(sessions);

        Assert.Equal(0, await notice.AnnounceAsync());
        Assert.Null(await player.ReadPacketAsync(TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public async Task ARolledOutLobbyWithNobodyInItAnnouncesNothing()
    {
        var notice = CreateNotice(new ActiveGameSessionsService());
        notice.RecordPlatformStop();

        Assert.Equal(0, await notice.AnnounceAsync());
    }

    /// <summary>Builds the notice a lobby drains with.</summary>
    /// <param name="sessions">Sessions the notice is written to.</param>
    private static RolloutNoticeService CreateNotice(ActiveGameSessionsService sessions) =>
        new(
            new FlashNewsService(sessions, new SessionHelper(Codec)),
            NullLogger<RolloutNoticeService>.Instance);

    /// <summary>
    /// A client connected to the lobby's session set, holding both ends of the
    /// connection: the lobby writes to the session, and the test reads what it
    /// wrote from the other one.
    /// </summary>
    private sealed class ConnectedPlayer : IDisposable
    {
        private readonly TcpClient readerSide;
        private readonly TcpClient writerSide;

        private ConnectedPlayer(TcpClient readerSide, TcpClient writerSide)
        {
            this.readerSide = readerSide;
            this.writerSide = writerSide;
        }

        /// <summary>Connects a client and records its session with the lobby.</summary>
        /// <param name="sessions">Sessions the connection is recorded in.</param>
        public static async Task<ConnectedPlayer> ConnectAsync(ActiveGameSessionsService sessions)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var accepted = listener.AcceptTcpClientAsync();
            var writerSide = new TcpClient();
            await writerSide.ConnectAsync(
                IPAddress.Loopback,
                ((IPEndPoint)listener.LocalEndpoint).Port);
            var readerSide = await accepted;
            listener.Stop();

            sessions.Add(new TcpSession
            {
                ServerType = ServerType.GameplayLobby,
                LogPrefix = "test",
                RemoteAddress = "127.0.0.1:1",
                Connection = writerSide.GetStream(),
                CharacterIdentifier = 42,
            });

            return new ConnectedPlayer(readerSide, writerSide);
        }

        /// <summary>
        /// Reads until one whole packet has arrived. The bytes are accumulated
        /// and asked of the codec the way the read loop does it, because a
        /// single read is never a promise that a whole packet was delivered.
        /// </summary>
        /// <param name="patience">How long the read is given, ten seconds by default.</param>
        /// <returns>The packet that arrived, or <c>null</c> when none did.</returns>
        public async Task<Packet?> ReadPacketAsync(TimeSpan? patience = null)
        {
            using var deadline = new CancellationTokenSource(patience ?? Patience);
            var accumulated = new byte[1024];
            var received = 0;

            while (received < accumulated.Length)
            {
                int read;
                try
                {
                    read = await readerSide.GetStream().ReadAsync(
                        accumulated.AsMemory(received),
                        deadline.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }

                if (read == 0)
                {
                    return null;
                }

                received += read;
                var packet = Codec.DecodePacket(accumulated.AsSpan(0, received));
                if (packet is not null)
                {
                    return packet;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            readerSide.Dispose();
            writerSide.Dispose();
        }
    }
}
