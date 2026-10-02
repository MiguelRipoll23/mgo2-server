using System.Net;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
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
/// The shape comes from a real dedicated server's roster during a live game
/// (<c>docs/mgo2-game.pcapng</c>, written up in
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4): back-to-back records, the
/// host's own entry first at roster index -1, then one per joining player at
/// 0, 1, 2 and so on.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class RoomRosterTests
{
    private static readonly IPEndPoint First = new(IPAddress.Loopback, 40001);
    private static readonly IPEndPoint Second = new(IPAddress.Loopback, 40002);

    /// <summary>Builds a roster over a host identity with known names.</summary>
    /// <param name="characterName">Character name the host announces in its profile record.</param>
    /// <param name="clanName">Clan name the host announces; empty for none.</param>
    private static RoomRosterService CreateRoster(string characterName = "host", string? clanName = "clan") =>
        new(new HostIdentityService(Options.Create(new ServerOptions
        {
            GameplayServerCharacterName = characterName,
            GameplayServerClanName = clanName,
        })));

    /// <summary>Builds a profile record as a joining client would send it.</summary>
    /// <param name="characterId">Character identifier the player carries at offset 8.</param>
    /// <param name="name">Character name of the player.</param>
    /// <param name="clanName">Clan name of the player; empty for none.</param>
    /// <param name="appearance">Appearance block the player carries; empty for none.</param>
    private static PlayerProfileRecord? JoinerProfile(
        int characterId,
        string name,
        string clanName,
        byte[]? appearance = null) =>
        PlayerProfileRecordParseUtils.Parse(
            PlayerProfileRecordUtility.Build(
                PlayerProfileRecordUtility.PlayerEntrySubType,
                RoomRosterService.FirstJoinerRosterIndex,
                characterId,
                name,
                clanName,
                appearance));

    /// <summary>Parses a built record, failing the test rather than returning null.</summary>
    /// <param name="body">Record body the roster produced.</param>
    private static PlayerProfileRecord Parse(byte[] body) =>
        PlayerProfileRecordParseUtils.Parse(body)
        ?? throw new InvalidOperationException("A built record was too short to hold the fixed fields.");

    [Fact]
    public void BuildRecords_puts_the_host_first_and_the_joiners_after_it_in_slot_order()
    {
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(65537, "Celestia", "FiNAL BOSS"));
        roster.Register(Second, JoinerProfile(65540, "UbuntU", "Dolphins"));

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
    public void BuildRecords_answers_with_the_character_id_and_appearance_the_player_announced()
    {
        // Both belong to the character rather than to the slot, and the capture
        // shows the same character carrying the same two on entries written at
        // different roster indices. So a peer is answered with what the player
        // arrived with rather than with something this host made up, and a peer
        // that never announced either gets the record's constants and zeros.
        var appearance = Convert.FromHexString("621aa81b6216c0a801166216");
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(65538, "Celestia", "FiNAL BOSS", appearance));
        roster.Register(Second, JoinerProfile(65540, "UbuntU", "Dolphins"));

        var records = roster.BuildRecords().Select(Parse).ToArray();

        Assert.Equal(65538, records[1].CharacterId);
        Assert.Equal(appearance, records[1].Appearance);
        Assert.Equal(PlayerProfileRecordUtility.PlayerEntrySubType, records[1].RecordSubType);

        // A peer that announced no appearance gets the block's measured
        // constants and zeros between them, which is what the builder writes
        // when it has no character to copy the block from.
        Assert.Equal(65540, records[2].CharacterId);
        Assert.Equal(
            "000000006216000000006216",
            Convert.ToHexString(records[2].Appearance).ToLowerInvariant());

        // The host's own entry is the room's record, not a player's, so it
        // carries the room's sub-type and the host's own character id.
        Assert.Equal(PlayerProfileRecordUtility.RoomRecordSubType, records[0].RecordSubType);
        Assert.Equal((int)UdpHostIdentityConstants.HostPeerIdentifier, records[0].CharacterId);
    }

    [Fact]
    public void Register_keeps_the_character_id_and_appearance_across_a_re_sent_profile()
    {
        // The client re-sends its profile byte for byte until it is answered, so
        // the repeats are the same record and must not clear what the first one
        // established.
        var appearance = Convert.FromHexString("5cbfb2676216c0a8015a6216");
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(65540, "Celestia", "FiNAL BOSS", appearance));

        roster.Register(First, JoinerProfile(65540, "Celestia", "FiNAL BOSS", appearance));

        var record = Parse(roster.BuildRecords()[1]);
        Assert.Equal(65540, record.CharacterId);
        Assert.Equal(appearance, record.Appearance);
    }

    [Fact]
    public void Register_keeps_the_slot_of_a_client_that_sends_its_profile_again()
    {
        // A joining client re-sends its profile byte for byte until it is
        // answered, so every repeat arrives here. Handing out a fresh slot on
        // each one would push the client along the roster on every retry.
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(65537, "Celestia", "FiNAL BOSS"));

        var repeat = roster.Register(First, JoinerProfile(65537, "Celestia", "FiNAL BOSS"));

        Assert.Equal(RoomRosterService.FirstJoinerRosterIndex, repeat.RosterIndex);
        Assert.Equal(2, roster.BuildRecords().Count);
    }

    [Fact]
    public void Remove_frees_the_slot_of_the_player_that_left()
    {
        var roster = CreateRoster();
        roster.Register(First, JoinerProfile(65537, "Celestia", "FiNAL BOSS"));
        roster.Register(Second, JoinerProfile(65540, "UbuntU", "Dolphins"));

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
        roster.Register(First, JoinerProfile(65537, "Celestia", "FiNAL BOSS"));
        roster.Register(Second, JoinerProfile(65540, "UbuntU", "Dolphins"));
        roster.Remove(First);

        var rejoined = roster.Register(
            new IPEndPoint(IPAddress.Loopback, 40003),
            JoinerProfile(65537, "Celestia", "FiNAL BOSS"));

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
