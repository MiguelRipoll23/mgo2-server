using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the two fields of the <c>0x1001</c> roster entry that belong to the
/// character rather than to the slot it sits in: the character id at offset
/// <c>0x08</c> and the appearance block at <c>0x13</c>–<c>0x1e</c>.
/// </summary>
/// <remarks>
/// The vectors are the captured player entries from
/// <c>docs/mgo2-game.pcapng</c>, with same-length placeholder names so the
/// capture's own names stay out of the repository while every offset stays
/// valid. The one that matters is the pair at roster indices 1 and 4: the same
/// character, rejoined, written at a different index under a different handle —
/// and carrying the same id and the same twelve bytes, which is what separates
/// both fields from the per-occurrence values beside them.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class PlayerProfileCharacterTests
{
    /// <summary>
    /// The five captured player entries, in roster order, at indices 0 to 4.
    /// The last is the same character as index 1 rejoining.
    /// </summary>
    private static readonly string[] CapturedBodies =
    [
        "07480000e21400140100010000000000020002d98ad50562160a0200026216000000000000000000000000000000000000000000000000000000000000000000000000004e696768744f776c373700",
        "07480000e30f000f0200010000000000020002621aa81b6216c0a8011662160000000000000000000000000000000000000000000000000000000000000000000000000049726f6e46616c636f6e00",
        "07470000e404000403000100000000000100025998e2296216c0a8014d621600000000000000000000000000000000000000000000000000000000000000000000000000537465656c4861727400",
        "07480000e50b000b04000100000000000200025cbfb2676216c0a8015a621600000000000000000000000000000000000000000000000000000000000000000000000000436f707065724b69746500",
        "07480000e60600060200010000000000020002621aa81b6216c0a8011662160000000000000000000000000000000000000000000000000000000000000000000000000049726f6e46616c636f6e00",
    ];

    /// <summary>
    /// The room's own record, the one captured entry that is not a player's.
    /// </summary>
    private const string CapturedRoomBody =
        "074c00000000000001a0000001020000010002634283b163160a680a1c63160000000000000000000000000000000000000000000000000000000000000000000000000044656469636174656420686f737400";

    private static readonly byte[][] CapturedPlayerBodies = CapturedBodies.Select(Convert.FromHexString).ToArray();

    /// <summary>
    /// The character id each captured player entry carries at offset 8. The two
    /// entries of the character that rejoined agree, which is the whole point.
    /// </summary>
    private static readonly int[] CapturedCharacterIds = [65537, 65538, 65539, 65540, 65538];

    /// <summary>
    /// The appearance block each captured player entry carries, in the same
    /// order. The character at index 1 and the one rejoining at index 4 carry
    /// the same twelve bytes at different roster indices.
    /// </summary>
    private static readonly string[] CapturedAppearanceBlocks =
    [
        "d98ad50562160a0200026216",
        "621aa81b6216c0a801166216",
        "5998e2296216c0a8014d6216",
        "5cbfb2676216c0a8015a6216",
        "621aa81b6216c0a801166216",
    ];

    /// <summary>Reads a captured appearance block back out of its hex form.</summary>
    private static byte[] Block(int index) => Convert.FromHexString(CapturedAppearanceBlocks[index]);

    [Fact]
    public void Parse_reads_the_character_id_off_offset_eight()
    {
        // Offset 8 is a u32 little-endian, not the u16 of unresolved value an
        // earlier reading had there. Every entry of one character carries the
        // same id, and the ids are character ids rather than roster numbers:
        // 65537 is the local player's own, which the TCP character record of the
        // same session carries as 0x0001232f.
        for (var index = 0; index < CapturedPlayerBodies.Length; index++)
        {
            var record = PlayerProfileRecordParseUtils.Parse(CapturedPlayerBodies[index]);
            Assert.NotNull(record);
            Assert.Equal(CapturedCharacterIds[index], record.CharacterId);
        }

        Assert.Equal(65537, CapturedCharacterIds[0]);
    }

    [Fact]
    public void Build_writes_the_character_id_as_four_little_endian_bytes_at_offset_eight()
    {
        var body = PlayerProfileRecordUtility.Build(
            recordSubType: PlayerProfileRecordUtility.PlayerEntrySubType,
            rosterIndex: 0,
            characterId: 65537,
            name: "NightOwl77",
            clanName: string.Empty);

        Assert.Equal("01000100", Convert.ToHexString(body, 8, 4).ToLowerInvariant());
        Assert.Equal(65537, PlayerProfileRecordParseUtils.Parse(body)!.CharacterId);
    }

    [Fact]
    public void Parse_reads_the_appearance_block_that_belongs_to_the_character_not_the_slot()
    {
        // Twelve bytes at 0x13..0x1e, and the same twelve on every entry of one
        // character. This is the copy of a player's appearance every peer is
        // given in the roster itself; a separate record carries more of it, but
        // this is what travels with the name and the id.
        for (var index = 0; index < CapturedPlayerBodies.Length; index++)
        {
            var record = PlayerProfileRecordParseUtils.Parse(CapturedPlayerBodies[index]);
            Assert.NotNull(record);
            Assert.Equal(Block(index), record.Appearance);
            Assert.Equal(PlayerProfileRecordUtility.AppearanceLength, record.Appearance.Length);
        }

        Assert.Equal(Block(1), Block(4));
    }

    [Fact]
    public void Every_character_carries_an_appearance_block_of_its_own()
    {
        // Four characters, four blocks, no two alike — so the block identifies
        // the character rather than describing the room or the slot.
        var blocks = CapturedPlayerBodies
            .Select(body => Convert.ToHexString(Parse(body).Appearance))
            .ToArray();

        Assert.Equal(4, blocks.Distinct().Count());
    }

    [Fact]
    public void Build_writes_the_appearance_block_it_is_given()
    {
        var withBlock = PlayerProfileRecordUtility.Build(
            recordSubType: PlayerProfileRecordUtility.PlayerEntrySubType,
            rosterIndex: 0,
            characterId: 65538,
            name: "IronFalcon",
            clanName: string.Empty,
            appearance: Block(1));

        Assert.Equal(Block(1), Parse(withBlock).Appearance);
        Assert.Equal(
            CapturedAppearanceBlocks[1],
            Convert.ToHexString(withBlock, 0x13, 12).ToLowerInvariant());
    }

    [Fact]
    public void Build_writes_the_blocks_measured_constants_when_it_has_no_block_to_copy()
    {
        // Without a character to copy from, the builder writes the marker and the
        // trailer of each half of the block and zeros between them. Both are the
        // same byte in every captured player entry.
        var body = PlayerProfileRecordUtility.Build(
            recordSubType: PlayerProfileRecordUtility.PlayerEntrySubType,
            rosterIndex: 0,
            characterId: 65538,
            name: "IronFalcon",
            clanName: string.Empty);

        Assert.Equal(0x62, body[0x17]);
        Assert.Equal(0x16, body[0x18]);
        Assert.Equal(0x62, body[0x1d]);
        Assert.Equal(0x16, body[0x1e]);
        Assert.All(
            new[] { body[0x13], body[0x14], body[0x15], body[0x16], body[0x19], body[0x1a], body[0x1b], body[0x1c] },
            value => Assert.Equal(0, value));
    }

    [Fact]
    public void The_block_marker_tells_a_player_entry_from_the_room_record()
    {
        // The same twelve bytes carry 0x62 on every player entry and 0x63 on the
        // room's own record, with the 0x16 trailer unchanged either way — which
        // is the marker doing one job and one job only.
        foreach (var body in CapturedPlayerBodies)
        {
            Assert.Equal(0x62, body[0x17]);
            Assert.Equal(0x16, body[0x18]);
            Assert.Equal(0x62, body[0x1d]);
            Assert.Equal(0x16, body[0x1e]);
        }

        var room = Convert.FromHexString(CapturedRoomBody);
        Assert.Equal(0x63, room[0x17]);
        Assert.Equal(0x16, room[0x18]);
        Assert.Equal(0x63, room[0x1d]);
        Assert.Equal(0x16, room[0x1e]);

        var built = PlayerProfileRecordUtility.Build(
            PlayerProfileRecordUtility.RoomRecordSubType,
            PlayerProfileRecordUtility.HostRosterIndex,
            1,
            "Dedicated host",
            string.Empty);

        Assert.Equal(0x63, built[0x17]);
        Assert.Equal(0x63, built[0x1d]);
    }

    [Fact]
    public void Parse_tells_a_player_entry_from_the_room_record_by_its_sub_type()
    {
        // The byte at offset 1 says what kind of record this is, not who it is
        // about: the id moved to offset 8, so a record naming a character is
        // recognisable by its own sub-type.
        var room = Parse(Convert.FromHexString(CapturedRoomBody));
        var player = Parse(CapturedPlayerBodies[0]);

        Assert.Equal(PlayerProfileRecordUtility.RoomRecordSubType, room.RecordSubType);
        Assert.Equal(PlayerProfileRecordUtility.PlayerEntrySubType, player.RecordSubType);
        Assert.NotEqual(room.CharacterId, player.CharacterId);
    }

    /// <summary>Parses a captured record, failing the test rather than returning null.</summary>
    private static PlayerProfileRecord Parse(byte[] body) =>
        PlayerProfileRecordParseUtils.Parse(body)
        ?? throw new InvalidOperationException("A captured record was too short to hold the fixed fields.");
}