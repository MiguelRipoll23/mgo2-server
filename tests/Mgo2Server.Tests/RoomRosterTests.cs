using System.Net;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the answer a joining client gets: the room roster, the host's own entry
/// first and every joining player after it in slot order.
/// </summary>
/// <remarks>
/// The shape comes from the roster block a real host wrote into a recorded
/// match (<c>tools/replays/replay_360827_5.dat</c>): twelve back-to-back
/// <c>0x1001</c> records, the first at file offset <c>0x52</c> carrying the
/// host's own profile at roster index -1, then one per joining player at 0, 1,
/// 2 and so on.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class RoomRosterTests
{
    private static readonly IPEndPoint First = new(IPAddress.Loopback, 40001);
    private static readonly IPEndPoint Second = new(IPAddress.Loopback, 40002);

    /// <summary>Builds a roster over a host identity with known names.</summary>
    /// <param name="accountName">Account name the host announces.</param>
    /// <param name="clanName">Clan name the host announces; empty for none.</param>
    private static RoomRosterService CreateRoster(string accountName = "host", string? clanName = "clan") =>
        new(new HostIdentityService(Options.Create(new ServerOptions
        {
            GameplayServerAccountName = accountName,
            GameplayServerClanName = clanName,
        })));

    /// <summary>Builds a profile record as a joining client would send it.</summary>
    /// <param name="characterIdentifier">Character identifier of the player.</param>
    /// <param name="name">Account name of the player.</param>
    /// <param name="clanName">Clan name of the player; empty for none.</param>
    private static PlayerProfileRecord? JoinerProfile(byte characterIdentifier, string name, string clanName) =>
        PlayerProfileRecordUtility.Parse(
            PlayerProfileRecordUtility.Build(
                characterIdentifier,
                PlayerProfileRecordUtility.HostRosterIndex,
                0,
                0,
                name,
                clanName));

    /// <summary>Parses a built record, failing the test rather than returning null.</summary>
    /// <param name="body">Record body the roster produced.</param>
    private static PlayerProfileRecord Parse(byte[] body) =>
        PlayerProfileRecordUtility.Parse(body)
        ?? throw new InvalidOperationException("A built record was too short to hold the fixed fields.");

    [Fact]
    public void BuildRecords_puts_the_host_first_and_the_joiners_after_it_in_slot_order()
    {
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(0x50, "Celestia", "FiNAL BOSS"));
        roster.Register(Second, JoinerProfile(0x4c, "UbuntU", "Dolphins"));

        var records = roster.BuildRecords().Select(Parse).ToArray();

        Assert.Equal(3, records.Length);
        Assert.Equal("host", records[0].Name);
        Assert.Equal(PlayerProfileRecordUtility.HostRosterIndex, records[0].RosterIndex);
        Assert.Equal("Celestia", records[1].Name);
        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, records[1].RosterIndex);
        Assert.Equal("UbuntU", records[2].Name);
        Assert.Equal(1, records[2].RosterIndex);
    }

    [Fact]
    public void Register_keeps_the_slot_of_a_client_that_sends_its_profile_again()
    {
        // A joining client re-sends its profile byte for byte until it is
        // answered, so every repeat arrives here. Handing out a fresh slot on
        // each one would push the client along the roster on every retry.
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(0x50, "Celestia", "FiNAL BOSS"));

        var repeat = roster.Register(First, JoinerProfile(0x50, "Celestia", "FiNAL BOSS"));

        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, repeat.RosterIndex);
        Assert.Equal(2, roster.BuildRecords().Count);
    }

    [Fact]
    public void Remove_frees_the_slot_of_the_player_that_left()
    {
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(0x50, "Celestia", "FiNAL BOSS"));
        roster.Register(Second, JoinerProfile(0x4c, "UbuntU", "Dolphins"));

        Assert.True(roster.Remove(First));

        var records = roster.BuildRecords().Select(Parse).ToArray();
        Assert.Equal(2, records.Length);
        Assert.Equal("host", records[0].Name);
        Assert.Equal("UbuntU", records[1].Name);
    }

    [Fact]
    public void Register_fills_the_lowest_slot_left_by_a_player_that_left()
    {
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(0x50, "Celestia", "FiNAL BOSS"));
        roster.Register(Second, JoinerProfile(0x4c, "UbuntU", "Dolphins"));
        roster.Remove(First);

        var rejoined = roster.Register(
            new IPEndPoint(IPAddress.Loopback, 40003),
            JoinerProfile(0x50, "Celestia", "FiNAL BOSS"));

        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, rejoined.RosterIndex);
    }

    [Fact]
    public void BuildRecords_writes_a_host_without_a_clan_the_way_a_record_without_one_is_recorded()
    {
        // The one recorded roster entry that carries no clan name marks it with
        // a zero rather than the marker, so a host that announces no clan has
        // to be written the same way.
        var roster = CreateRoster("host", clanName: null);
        var record = Parse(roster.BuildRecords()[0]);

        Assert.False(record.HasClanName);
        Assert.Equal(string.Empty, record.ClanName);
    }
}
