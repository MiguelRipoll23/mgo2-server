using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.Shared.Domain.Presence;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the location block three packets carry — the friends and blocked roster
/// (0x4582), the player search (0x4602) and the clan roster — and the record
/// around it. All three records are the same width and the client counts them
/// itself, so a block of the wrong width does not fail a parse: it shifts every
/// field of every row that follows it, which reads as a screen full of columns
/// belonging to the wrong players.
/// </summary>
[Trait("Category", "GameLobby")]
public sealed class CharacterLocationTests
{
    /// <summary>Size of one roster or search entry, header included.</summary>
    private const int EntrySize = 59;

    /// <summary>Size of one clan roster entry, header included.</summary>
    private const int ClanEntrySize = 68;

    /// <summary>Offset the location block starts at in a roster or search entry.</summary>
    private const int BlockOffset = 4 + 16;

    [Fact]
    public void A_connected_character_is_written_with_their_lobby_and_room()
    {
        var location = new CharacterLocation(42, "Free Battle 01", 1, 7, "Deathmatch");

        var writer = new PacketWriter();
        CharacterLocationWriter.Write(writer, location);
        var payload = writer.Build();

        Assert.Equal(CharacterLocationWriter.Size, payload.Length);
        Assert.Equal((ushort)42, BinaryUtility.ReadUInt16BigEndian(payload, 0));
        Assert.Equal("Free Battle 01", ReadFixedString(payload, 2, 16));
        Assert.Equal(7u, BinaryUtility.ReadUInt32BigEndian(payload, 18));
        Assert.Equal("Deathmatch", ReadFixedString(payload, 22, 16));
        // The label is the game type of the lobby, decoded by the client's own
        // eight-arm table; a free battle lobby is 1.
        Assert.Equal((byte)1, payload[38]);
    }

    [Fact]
    public void A_character_who_is_nowhere_gets_the_empty_block()
    {
        var writer = new PacketWriter();
        CharacterLocationWriter.Write(writer, null);
        var payload = writer.Build();

        Assert.Equal(CharacterLocationWriter.Size, payload.Length);
        Assert.Equal((ushort)0, BinaryUtility.ReadUInt16BigEndian(payload, 0));
        Assert.Equal(string.Empty, ReadFixedString(payload, 2, 16));
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, 18));
        Assert.Equal(string.Empty, ReadFixedString(payload, 22, 16));
        // Zero is not a game type the client names, so it draws the column blank
        // rather than inventing a label for a player who is not connected.
        Assert.Equal((byte)0, payload[38]);
    }

    [Fact]
    public void A_character_in_a_lobby_without_a_room_names_the_lobby_only()
    {
        var location = new CharacterLocation(3, "Automatching", 2, 0, string.Empty);

        var writer = new PacketWriter();
        CharacterLocationWriter.Write(writer, location);
        var payload = writer.Build();

        Assert.Equal((ushort)3, BinaryUtility.ReadUInt16BigEndian(payload, 0));
        Assert.Equal("Automatching", ReadFixedString(payload, 2, 16));
        Assert.Equal(0u, BinaryUtility.ReadUInt32BigEndian(payload, 18));
        Assert.Equal(string.Empty, ReadFixedString(payload, 22, 16));
        Assert.Equal((byte)2, payload[38]);
    }

    [Fact]
    public void A_roster_entry_is_the_target_then_the_location_block()
    {
        var location = new CharacterLocation(42, "Free Battle 01", 1, 7, "Deathmatch");

        var writer = new PacketWriter();
        writer.WriteUInt32(99u);
        writer.WriteFixedString("Somebody", 16);
        CharacterLocationWriter.Write(writer, location);
        var payload = writer.Build();

        Assert.Equal(EntrySize, payload.Length);
        Assert.Equal(99u, BinaryUtility.ReadUInt32BigEndian(payload, 0));
        Assert.Equal("Somebody", ReadFixedString(payload, 4, 16));
        // The lobby the block names sits where the client's compaction looks for
        // it: a record whose word at 0x14 is zero is the one it treats as
        // absent, so the word has to be the lobby and not a flag of our own.
        Assert.Equal((ushort)42, BinaryUtility.ReadUInt16BigEndian(payload, BlockOffset));
    }

    [Fact]
    public void Two_location_blocks_in_one_packet_stay_on_their_own_records()
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(1u);
        writer.WriteFixedString("First", 16);
        CharacterLocationWriter.Write(writer, new CharacterLocation(42, "Free Battle 01", 1, 7, "Deathmatch"));
        writer.WriteUInt32(2u);
        writer.WriteFixedString("Second", 16);
        CharacterLocationWriter.Write(writer, null);
        var payload = writer.Build();

        Assert.Equal(EntrySize * 2, payload.Length);
        Assert.Equal(1u, BinaryUtility.ReadUInt32BigEndian(payload, 0));
        Assert.Equal((ushort)42, BinaryUtility.ReadUInt16BigEndian(payload, BlockOffset));
        Assert.Equal("Free Battle 01", ReadFixedString(payload, BlockOffset + 2, 16));
        // The second record's block follows its own header and not the first
        // record's, which is the whole point of pinning the width.
        Assert.Equal(2u, BinaryUtility.ReadUInt32BigEndian(payload, EntrySize));
        Assert.Equal((ushort)0, BinaryUtility.ReadUInt16BigEndian(payload, EntrySize + BlockOffset));
        Assert.Equal(string.Empty, ReadFixedString(payload, EntrySize + BlockOffset + 2, 16));
    }

    [Fact]
    public void A_clan_roster_entry_carries_the_same_block_after_its_own_header()
    {
        var location = new CharacterLocation(42, "Free Battle 01", 4, 7, "Survival");

        var writer = new PacketWriter();
        writer.WriteUInt32(99u);
        writer.WriteFixedString("Somebody", 16);
        writer.WriteUInt8(1);
        writer.WriteUInt32(0u);
        writer.WriteUInt32(99u);
        CharacterLocationWriter.Write(writer, location);
        var payload = writer.Build();

        Assert.Equal(ClanEntrySize, payload.Length);
        Assert.Equal((ushort)42, BinaryUtility.ReadUInt16BigEndian(payload, 29));
        Assert.Equal("Free Battle 01", ReadFixedString(payload, 31, 16));
        Assert.Equal(7u, BinaryUtility.ReadUInt32BigEndian(payload, 47));
        Assert.Equal("Survival", ReadFixedString(payload, 51, 16));
        Assert.Equal((byte)4, payload[67]);
    }

    [Fact]
    public void The_block_is_the_width_the_three_records_leave_for_it()
    {
        // Both records are a fixed width the client counts on: 59 for the roster and
        // the search (a u32 identifier and a 16-byte name in front of the block) and 68
        // for the clan roster (a 29-byte header). A block of another width does not
        // fail a parse, it moves every field of every row that follows it.
        Assert.Equal(EntrySize - (4 + 16), CharacterLocationWriter.Size);
        Assert.Equal(ClanEntrySize - 29, CharacterLocationWriter.Size);
    }

    [Fact]
    public void The_stamp_keeps_a_live_player_inside_the_window_through_a_missed_beat()
    {
        // The window is the client's own cadence — a game client is heard from every
        // half minute — so it is tight on purpose: the beat has to land once inside it
        // or a reader that hides a row past the window would lose a player who is only
        // quiet. Two beats, so one missed beat is still survived.
        Assert.True(CharacterPresenceService.HeartbeatInterval < CharacterPresenceService.StaleAfter);
        Assert.True(CharacterPresenceService.HeartbeatInterval * 2 <= CharacterPresenceService.StaleAfter);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(8, 8)]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(-1, 0)]
    public void The_label_table_stops_at_the_eighth_subtype(int lobbySubtype, int expected)
    {
        // The ninth value is the match history's own game type, decoded by a
        // different table; a location block has no name for it and says so by
        // labelling nothing rather than by borrowing the history's label.
        Assert.Equal(expected, CharacterLocationUtils.LobbyLabel(lobbySubtype));
    }

    private static string ReadFixedString(byte[] payload, int offset, int length)
    {
        var text = System.Text.Encoding.ASCII.GetString(payload, offset, length);
        var terminator = text.IndexOf('\0');

        return terminator >= 0 ? text[..terminator] : text;
    }
}
