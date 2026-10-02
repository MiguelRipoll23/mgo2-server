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

    /// <summary>
    /// The seven bytes the recorded host closes a roster run with, on the wire
    /// in the live session as well as in the replay.
    /// </summary>
    private static readonly byte[] RosterClose = PlayerProfileRecordUtility.BuildRosterClose();

    /// <summary>Runs the handler over one inbound profile and reports what went out.</summary>
    /// <param name="roster">Roster to answer from.</param>
    /// <param name="remote">Endpoint the profile came from.</param>
    /// <param name="body">Body the message carries; a profile unless stated otherwise.</param>
    /// <param name="logs">Collects the handler's log lines when supplied.</param>
    /// <returns>The records sent to the joiner, and the records broadcast to the room.</returns>
    private static async Task<(List<RosterRecord> Sent, List<RosterRecord> Broadcast)> RunAsync(
        RoomRosterService roster,
        IPEndPoint remote,
        byte[]? body = null,
        List<string>? logs = null)
    {
        List<RosterRecord> sent = [];
        List<RosterRecord> broadcast = [];
        var logger = logs is null
            ? NullLogger<PlayerProfileHandler>.Instance
            : logs.Collecting<PlayerProfileHandler>();
        var handler = new PlayerProfileHandler(roster, logger);

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
            (type, body) =>
            {
                sent.Add(new RosterRecord(type, body));
                return Task.CompletedTask;
            },
            (type, body) =>
            {
                broadcast.Add(new RosterRecord(type, body));
                return Task.CompletedTask;
            });

        await handler.HandleAsync(context);
        return (sent, broadcast);
    }

    /// <summary>Builds the body a joining client opens the exchange with.</summary>
    /// <param name="name">Character name the client announces.</param>
    /// <param name="clanName">Clan name the client announces; empty for none.</param>
    private static byte[] JoinRequestBody(string name = "Celestia", string clanName = "FiNAL BOSS")
    {
        var body = PlayerProfileRecordUtility.Build(0x50, 0, 0, 0, name, clanName);

        // The client opens with a request rather than a roster entry, so the
        // leading byte differs from the one a roster record carries.
        body[0] = PlayerProfileRecordUtility.JoinRequestVersion;
        return body;
    }

    private static RoomRosterService CreateRoster(string characterName = "host", string? clanName = "clan") =>
        new(new HostIdentityService(Options.Create(new ServerOptions
        {
            GameplayServerCharacterName = characterName,
            GameplayServerClanName = clanName,
        })));

    [Fact]
    public async Task HandleAsync_writes_the_run_at_the_lengths_the_capture_shows()
    {
        // The live server's answer measured 83 bytes for its own 14-character
        // name, 79 for the 10-character joiner and 7 for the close. The name is
        // what makes a record its length, so these are the sizes a client that
        // has seen the real exchange expects. The host record carries no clan
        // name, so the roster here is built without one. The run opens with the
        // empty record the live host sends first, so the entries start at one.
        var roster = CreateRoster("Dedicated host", clanName: null);
        var (sent, _) = await RunAsync(roster, Joiner, JoinRequestBody("NightOwl77", clanName: string.Empty));

        Assert.Equal(83, sent[1].Body.Length);
        Assert.Equal(79, sent[2].Body.Length);
        Assert.Equal(7, sent[3].Body.Length);
    }

    [Fact]
    public async Task HandleAsync_answers_the_joiner_with_the_whole_roster()
    {
        var (sent, _) = await RunAsync(CreateRoster(), Joiner);

        var names = sent
            .Select(record => PlayerProfileRecordUtility.Parse(record.Body)?.Name)
            .OfType<string>()
            .ToArray();
        Assert.Equal(["host", "Celestia"], names);
    }

    [Fact]
    public async Task HandleAsync_opens_the_run_with_the_empty_record_the_capture_shows()
    {
        // The live server's roster answer opens with a zero-length 0x5001 record
        // in both of its roster frames. What it says is unresolved - the same
        // type is shown not to be the acknowledgement despite the resemblance -
        // but the host does not invent records a peer never wrote.
        var (sent, broadcast) = await RunAsync(CreateRoster(), Joiner);

        Assert.Equal(UdpCommandConstants.RosterHead, sent[0].Type);
        Assert.Empty(sent[0].Body);
        Assert.Equal(sent[0], broadcast[0]);
    }

    [Fact]
    public async Task HandleAsync_tags_the_hosts_own_entry_with_the_join_type_and_the_rest_with_the_roster_type()
    {
        // The recorded host sends its own entry under 0x9001 - the tag the joiner
        // itself opened the exchange with - and follows it with the joining
        // players and the close under 0x1001. Both roster frames of the live
        // session do this, so the head of the run is not merely a 0x1001 record
        // like the rest of it.
        var (sent, broadcast) = await RunAsync(CreateRoster(), Joiner);

        Assert.Equal(
            [
                UdpCommandConstants.RosterHead,
                UdpCommandConstants.JoinRequest,
                UdpCommandConstants.PlayerProfile,
                UdpCommandConstants.PlayerProfile,
            ],
            sent.Select(record => record.Type).ToArray());
        Assert.Equal(RoomRosterService.HostEntryType, sent[1].Type);

        // The host's own entry is the one under the join tag.
        var host = PlayerProfileRecordUtility.Parse(sent[1].Body);
        Assert.NotNull(host);
        Assert.Equal("host", host.Name);
        Assert.Equal(PlayerProfileRecordUtility.HostRosterIndex, host.RosterIndex);

        Assert.Equal(sent.Select(record => record.Type), broadcast.Select(record => record.Type));
    }

    [Fact]
    public async Task HandleAsync_closes_the_roster_with_the_record_the_capture_shows()
    {
        // The recorded host ends a roster run with a seven-byte 0x1001 record
        // after the last entry: 07 00 00 00 00 00 03, on the wire in the live
        // session as well as in the replay. The joiner has no way to see where
        // the roster stops without it.
        var (sent, broadcast) = await RunAsync(CreateRoster(), Joiner);

        Assert.Equal(UdpCommandConstants.PlayerProfile, sent[^1].Type);
        Assert.Equal("07000000000003", Convert.ToHexString(sent[^1].Body).ToLowerInvariant());
        Assert.Equal(RosterClose, sent[^1].Body);
        Assert.Null(PlayerProfileRecordUtility.Parse(sent[^1].Body));
        Assert.Equal(sent[^1].Body, broadcast[^1].Body);
    }

    [Fact]
    public async Task HandleAsync_announces_the_host_entry_under_the_join_tag_in_the_log()
    {
        // The host's own entry going out under 0x9001 is the oddity in this
        // exchange, and it is logged at information level rather than left to be
        // discovered from a client that ignores the roster.
        var logs = new List<string>();

        await RunAsync(CreateRoster(), Joiner, logs: logs);

        Assert.Contains(logs, line =>
            line.Contains("0x9001", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("join", StringComparison.OrdinalIgnoreCase));
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

        // The empty head, the host, the joiner, and the record that closes the
        // roster.
        Assert.Equal(4, sent.Count);
        var record = PlayerProfileRecordUtility.Parse(sent[2].Body);
        Assert.NotNull(record);
        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, record.RosterIndex);
    }
}
