using System.Net;
using Mgo2Server.GameplayServer.Commands;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the answer a joining client actually receives: the room roster, the
/// host's own entry first and every joining player after it — sent to the
/// joiner, and sent again to the peers already in the room.
/// </summary>
[Trait("Category", "Shared")]
public sealed class PlayerProfileHandlerTests
{
    private static readonly IPEndPoint Joiner = new(IPAddress.Loopback, 40001);

    /// <summary>Runs the handler over one inbound profile and reports what went out.</summary>
    /// <param name="roster">Roster to answer from.</param>
    /// <param name="remote">Endpoint the profile came from.</param>
    /// <param name="body">Body the message carries; a profile unless stated otherwise.</param>
    /// <returns>The bodies sent to the joiner, and the bodies broadcast to the room.</returns>
    private static async Task<(List<byte[]> Sent, List<byte[]> Broadcast)> RunAsync(
        RoomRosterService roster,
        IPEndPoint remote,
        byte[]? body = null)
    {
        List<byte[]> sent = [];
        List<byte[]> broadcast = [];
        var handler = new PlayerProfileHandler(roster, NullLogger<PlayerProfileHandler>.Instance);

        var context = new PeerContext(
            new PeerSession
            {
                RemoteAddress = remote.ToString(),
                DialBack = remote,
                CounterBase = 0x11223344,
                OutboundCounter = 0,
                SessionKey = 0,
                PeerIdentifier = 2,
                HostPeerIdentifier = 1,
                Established = true,
                LastSeenAt = 0,
                LastInboundSequence = 0,
            },
            UdpMessage.Create(UdpCommandConstants.PlayerProfile, body ?? JoinRequestBody()),
            remote,
            5730,
            (_, body) =>
            {
                sent.Add(body);
                return Task.CompletedTask;
            },
            (_, body) =>
            {
                broadcast.Add(body);
                return Task.CompletedTask;
            });

        await handler.HandleAsync(context);
        return (sent, broadcast);
    }

    /// <summary>Builds the body a joining client opens the exchange with.</summary>
    /// <param name="name">Account name the client announces.</param>
    private static byte[] JoinRequestBody(string name = "Celestia")
    {
        var body = PlayerProfileRecordUtility.Build(0x50, 0, 0, 0, name, "FiNAL BOSS");

        // The client opens with a request rather than a roster entry, so the
        // leading byte differs from the one a roster record carries.
        body[0] = PlayerProfileRecordUtility.JoinRequestVersion;
        return body;
    }

    private static RoomRosterService CreateRoster() =>
        new(new HostIdentityService(Options.Create(new ServerOptions
        {
            GameplayServerAccountName = "host",
            GameplayServerClanName = "clan",
        })));

    [Fact]
    public async Task HandleAsync_answers_the_joiner_with_the_whole_roster()
    {
        var (sent, _) = await RunAsync(CreateRoster(), Joiner);

        var names = sent.Select(body => PlayerProfileRecordUtility.Parse(body)?.Name ?? string.Empty).ToArray();
        Assert.Equal(["host", "Celestia"], names);
    }

    [Fact]
    public async Task HandleAsync_tells_the_peers_already_in_the_room_as_well()
    {
        var (sent, broadcast) = await RunAsync(CreateRoster(), Joiner);

        // A player announced only to the peer that just joined is never
        // announced to the ones that were there first, and they would go on
        // playing without knowing the room has grown.
        Assert.NotEmpty(broadcast);
        Assert.Equal(sent, broadcast);
    }

    [Fact]
    public async Task HandleAsync_does_not_answer_the_one_byte_messages_that_share_the_type()
    {
        // A joining client sends one-byte 0x1001 messages as well as profiles —
        // 23 of them against 11 profiles in the live capture. They are not
        // profiles, and answering each with the whole roster to the sender and
        // to every peer in the room turns a quiet host into a flood.
        var roster = CreateRoster();
        roster.Register(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 40002), null);

        var (sent, broadcast) = await RunAsync(roster, Joiner, body: [0x00]);

        Assert.Empty(sent);
        Assert.Empty(broadcast);
    }

    [Fact]
    public async Task HandleAsync_keeps_the_joiner_in_the_same_slot_across_retries()
    {
        var roster = CreateRoster();

        await RunAsync(roster, Joiner);
        var (sent, _) = await RunAsync(roster, Joiner);

        Assert.Equal(2, sent.Count);
        var record = PlayerProfileRecordUtility.Parse(sent[1]);
        Assert.NotNull(record);
        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, record.RosterIndex);
    }
}
