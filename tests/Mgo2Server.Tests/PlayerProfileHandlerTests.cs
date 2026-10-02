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
    /// <returns>
    /// The records sent to the joiner, and the records broadcast to the room. The
    /// joiner's copy is the roster run followed by the host's second copy of it,
    /// so a test that means one run takes the records up to its close.
    /// </returns>
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
    /// <param name="characterId">Character id the client announces at offset 8.</param>
    /// <param name="name">Character name the client announces.</param>
    /// <param name="clanName">Clan name the client announces; empty for none.</param>
    private static byte[] JoinRequestBody(
        int characterId = 65537,
        string name = "Celestia",
        string clanName = "FiNAL BOSS")
    {
        var body = PlayerProfileRecordUtility.Build(
            PlayerProfileRecordUtility.PlayerEntrySubType,
            RoomRosterService.FirstJoinerRosterIndex,
            characterId,
            name,
            clanName);

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
        var (sent, _) = await RunAsync(roster, Joiner, JoinRequestBody(name: "NightOwl77", clanName: string.Empty));

        Assert.Equal(83, sent[1].Body.Length);
        Assert.Equal(79, sent[2].Body.Length);
        Assert.Equal(7, sent[3].Body.Length);

        // And the second copy the host sends carries the same three, without the
        // head, so the lengths come out again a few records further on.
        Assert.Equal(83, sent[4].Body.Length);
        Assert.Equal(79, sent[5].Body.Length);
        Assert.Equal(7, sent[6].Body.Length);
    }

    [Fact]
    public async Task HandleAsync_answers_the_joiner_with_the_whole_roster()
    {
        var (sent, _) = await RunAsync(CreateRoster(), Joiner);

        var names = sent
            .Select(record => PlayerProfileRecordParseUtils.Parse(record.Body)?.Name)
            .OfType<string>()
            .ToArray();

        // The host and the joiner, then the same two again in the host's second
        // copy of the run.
        Assert.Equal(["host", "Celestia", "host", "Celestia"], names);
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
                UdpCommandConstants.JoinRequest,
                UdpCommandConstants.PlayerProfile,
                UdpCommandConstants.PlayerProfile,
            ],
            sent.Select(record => record.Type).ToArray());
        Assert.Equal(RoomRosterService.HostEntryType, sent[1].Type);

        // The host's own entry is the one under the join tag.
        var host = PlayerProfileRecordParseUtils.Parse(sent[1].Body);
        Assert.NotNull(host);
        Assert.Equal("host", host.Name);
        Assert.Equal(PlayerProfileRecordUtility.HostRosterIndex, host.RosterIndex);

        // One run is what the room is told; the second copy is the joiner's.
        Assert.Equal(
            broadcast.Select(record => record.Type),
            sent.Take(broadcast.Count).Select(record => record.Type));
    }

    [Fact]
    public async Task HandleAsync_sends_the_run_a_second_time_without_its_head()
    {
        // The recorded host sends every roster run twice. The second copy is the
        // same records in the same order, opening on the host's own entry rather
        // than on the empty 0x5001 head: at t+2475.561 the host put
        // 0x5001, 0x9001/83, 0x1001/79, 0x1001/7 on the wire, and at t+2475.678,
        // 117 ms later, the same three records without the head.
        var (sent, _) = await RunAsync(CreateRoster(), Joiner);

        var repeat = sent.Skip(4).ToArray();

        Assert.Equal(
            [
                UdpCommandConstants.JoinRequest,
                UdpCommandConstants.PlayerProfile,
                UdpCommandConstants.PlayerProfile,
            ],
            repeat.Select(record => record.Type));

        // The same bodies, so the second copy is the run rather than a fresh
        // rendering of it.
        Assert.Equal(sent[1].Body, repeat[0].Body);
        Assert.Equal(sent[2].Body, repeat[1].Body);
        Assert.Equal(sent[3].Body, repeat[2].Body);
        Assert.DoesNotContain(repeat, record => record.Type == UdpCommandConstants.RosterHead);
    }

    [Fact]
    public void The_second_copy_is_the_run_the_capture_repeats()
    {
        // Built straight from the capture's own body for the host's own entry: a
        // 90-byte run of head, entry, joiner and close becomes a 83-byte one
        // without the head, and the three bodies are byte for byte the same.
        var roster = CreateRoster("Dedicated host", clanName: null);
        roster.Register(Joiner, PlayerProfileRecordParseUtils.Parse(
            JoinRequestBody(name: "NightOwl77", clanName: string.Empty)));

        var run = roster.BuildRosterRun();
        var repeat = roster.BuildRosterRunRepeat();

        Assert.Equal(run.Count - 1, repeat.Count);
        Assert.Equal(UdpCommandConstants.RosterHead, run[0].Type);
        Assert.DoesNotContain(repeat, record => record.Type == UdpCommandConstants.RosterHead);
        Assert.Equal(run.Skip(1).Select(record => record.Type), repeat.Select(record => record.Type));
        Assert.Equal(run.Skip(1).Select(record => record.Body), repeat.Select(record => record.Body));
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
        Assert.Null(PlayerProfileRecordParseUtils.Parse(sent[^1].Body));
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
        // playing without knowing the room has grown. The room is told the run
        // once: the host's second copy is addressed to the peer that asked for
        // the roster, which is all the capture's single peer can show.
        Assert.NotEmpty(broadcast);
        Assert.Equal(sent.Take(broadcast.Count), broadcast);
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
        // roster — and then the host's second copy of those three.
        Assert.Equal(7, sent.Count);
        var record = PlayerProfileRecordParseUtils.Parse(sent[2].Body);
        Assert.NotNull(record);
        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, record.RosterIndex);
        Assert.Equal(sent[2].Body, sent[5].Body);
    }
}
