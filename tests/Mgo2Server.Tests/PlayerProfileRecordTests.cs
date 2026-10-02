using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the <c>0x1001</c> player-profile record against the bytes a real
/// dedicated server wrote during a live game (<c>docs/mgo2-game.pcapng</c>,
/// written up in <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4). The capture
/// holds six roster records — the room record and five player entries at roster
/// indices -1 and 0 to 4 — and the vectors below are those record bodies
/// verbatim, so a field that moves in the builder fails here rather than on a
/// live client.
/// </summary>
/// <remarks>
/// The character names in the vectors are replaced with same-length
/// placeholders, so the byte layout is untouched while the capture's own names
/// stay out of the repository. The replacement is the same length as the name
/// it stands in for, which is what keeps every offset in these tests valid.
/// </remarks>
[Trait("Category", "Shared")]
public sealed class PlayerProfileRecordTests
{
    /// <summary>
    /// The host's own record, sent under <c>0x9001</c> as the head of the
    /// roster. It is the only record whose offset-4 field is not
    /// <c>0xe2 + index</c>, and the only one whose sub-type is not a player
    /// entry's.
    /// </summary>
    private const string CapturedHostBody =        "074c00000000000001a0000001020000010002634283b163160a680a1c63160000000000000000000000000000000000000000000000000000000000000000000000000044656469636174656420686f737400";

    /// <summary>
    /// The five joining players' records, in roster order, at indices 0 to 4.
    /// The last is the same character as index 1 rejoining, which is why two
    /// records carry the same name at different indices.
    /// </summary>
    private static readonly string[] CapturedBodies =
    [
        "07480000e21400140100010000000000020002d98ad50562160a0200026216000000000000000000000000000000000000000000000000000000000000000000000000004e696768744f776c373700",
        "07480000e30f000f0200010000000000020002621aa81b6216c0a8011662160000000000000000000000000000000000000000000000000000000000000000000000000049726f6e46616c636f6e00",
        "07470000e404000403000100000000000100025998e2296216c0a8014d621600000000000000000000000000000000000000000000000000000000000000000000000000537465656c4861727400",
        "07480000e50b000b04000100000000000200025cbfb2676216c0a8015a621600000000000000000000000000000000000000000000000000000000000000000000000000436f707065724b69746500",
        "07480000e60600060200010000000000020002621aa81b6216c0a8011662160000000000000000000000000000000000000000000000000000000000000000000000000049726f6e46616c636f6e00",
    ];

    /// <summary>Every captured record, the room record first.</summary>
    private static readonly byte[][] AllCapturedBodies =
        [Convert.FromHexString(CapturedHostBody), .. CapturedBodies.Select(Convert.FromHexString)];

    /// <summary>The player entries, the room record left out.</summary>
    private static readonly byte[][] CapturedPlayerBodies = CapturedBodies.Select(Convert.FromHexString).ToArray();

    /// <summary>The character name each captured record carries, in the same order.</summary>
    private static readonly string[] CapturedNames =
        ["Dedicated host", "NightOwl77", "IronFalcon", "SteelHart", "CopperKite", "IronFalcon"];

    /// <summary>
    /// The columns of the per-player block the six captured records disagree
    /// on. Every other column of the block is the same byte in all six.
    /// </summary>
    private static readonly HashSet<int> PerPlayerBlockColumns =
        [0x0c, 0x0d, 0x10, 0x13, 0x14, 0x15, 0x16, 0x17, 0x19, 0x1a, 0x1b, 0x1c, 0x1d];

    /// <summary>
    /// Offsets 5 and 7, which carry the same per-player value in every
    /// captured record and which nothing decoded. The builder writes them as
    /// zero, so they are excluded from the byte-for-byte comparison below
    /// alongside the per-player block.
    /// </summary>
    private static readonly HashSet<int> UnsuppliedColumns = [0x05, 0x07, .. PerPlayerBlockColumns];

    [Fact]
    public void Parse_reads_the_name_of_every_captured_record()
    {
        for (var index = 0; index < AllCapturedBodies.Length; index++)
        {
            var record = PlayerProfileRecordParseUtils.Parse(AllCapturedBodies[index]);
            Assert.NotNull(record);
            Assert.Equal(CapturedNames[index], record.Name);

            // No captured record carries a clan, so none of them ends with one.
            Assert.Equal(string.Empty, record.ClanName);
            Assert.False(record.HasClanName);
        }
    }

    [Fact]
    public void Parse_reads_the_roster_index_of_every_captured_record()
    {
        // Offset 4 is 0xe2 plus the roster index for a joining player, and a
        // plain zero for the host. Index -1 recovers as 0xe1 under the base
        // alone, which is not what the host carries, so the zero is read as the
        // host rather than as a wrapped unsigned index.
        for (var index = 0; index < AllCapturedBodies.Length; index++)
        {
            var record = PlayerProfileRecordParseUtils.Parse(AllCapturedBodies[index]);
            Assert.NotNull(record);
            Assert.Equal((sbyte)index - 1, record.RosterIndex);
        }

        Assert.Equal(0xe2, AllCapturedBodies[1][4]);
        Assert.Equal(0xe6, AllCapturedBodies[5][4]);
        Assert.Equal(0x00, AllCapturedBodies[0][4]);
    }

    [Fact]
    public void Parse_reports_the_value_at_offset_five_as_a_per_player_value_not_an_index()
    {
        // The six captured records carry 0x00, 0x14, 0x0f, 0x04, 0x0b and 0x06
        // at offset 5, repeated at offset 7. That does not follow roster order,
        // so it is not the index under any base.
        var values = AllCapturedBodies
            .Select(body => PlayerProfileRecordParseUtils.Parse(body))
            .Select(record => record!.PlayerValue)
            .ToArray();

        Assert.Equal([0x00, 0x14, 0x0f, 0x04, 0x0b, 0x06], values);
    }

    [Fact]
    public void Every_captured_record_repeats_offset_five_at_offset_seven()
    {
        foreach (var body in AllCapturedBodies)
        {
            Assert.Equal(body[5], body[7]);
        }
    }

    [Fact]
    public void Build_places_the_host_on_its_own_value_rather_than_one_below_the_first_player()
    {
        // The host is not a joining player, so it is not on the players' scale:
        // the captured host record carries 0x00 at offset 4 where the first
        // joining player carries 0xe2, and 0xe2 - 1 is 0xe1, not zero.
        var record = PlayerProfileRecordParseUtils.Parse(Convert.FromHexString(CapturedHostBody));

        Assert.NotNull(record);
        Assert.Equal(PlayerProfileRecordUtility.HostRosterIndex, record.RosterIndex);
        Assert.Equal(0x00, record.RosterBaseField);
        Assert.Equal("Dedicated host", record.Name);
    }

    [Fact]
    public void Build_writes_the_host_field_and_the_player_base_the_capture_shows()
    {
        var host = PlayerProfileRecordUtility.Build(
            recordSubType: PlayerProfileRecordUtility.RoomRecordSubType,
            rosterIndex: PlayerProfileRecordUtility.HostRosterIndex,
            characterId: 0x0000a001,
            name: "Dedicated host",
            clanName: string.Empty);

        var joiner = PlayerProfileRecordUtility.Build(
            recordSubType: PlayerProfileRecordUtility.PlayerEntrySubType,
            rosterIndex: 0,
            characterId: 65537,
            name: "NightOwl77",
            clanName: string.Empty);

        Assert.Equal(0x00, host[4]);
        Assert.Equal(0xe2, joiner[4]);
        Assert.Equal(
            0xe6,
            PlayerProfileRecordUtility.Build(
                PlayerProfileRecordUtility.PlayerEntrySubType, 4, 65538, "IronFalcon", string.Empty)[4]);

        // Both records are as long as the captured ones with those names.
        Assert.Equal(Convert.FromHexString(CapturedHostBody).Length, host.Length);
        Assert.Equal(Convert.FromHexString(CapturedBodies[0]).Length, joiner.Length);
    }

    [Fact]
    public void BuildRosterClose_writes_the_seven_bytes_that_end_a_roster()
    {
        // The recorded close, byte for byte: the record version, five zeros and
        // the marker byte. It is seen in the replay and on the live wire, where
        // it closes a two-entry roster, so it is not tied to a roster size.
        var close = PlayerProfileRecordUtility.BuildRosterClose();

        Assert.Equal(7, close.Length);
        Assert.Equal("07000000000003", Convert.ToHexString(close).ToLowerInvariant());

        // It is too short to hold a name, so a parser reading it as a profile
        // finds no player rather than an empty one.
        Assert.Null(PlayerProfileRecordParseUtils.Parse(close));
    }

    [Fact]
    public void Parse_rejects_a_body_shorter_than_the_fixed_fields()
    {
        Assert.Null(PlayerProfileRecordParseUtils.Parse(new byte[PlayerProfileRecordUtility.NameOffset]));
        Assert.Null(PlayerProfileRecordParseUtils.Parse(new byte[PlayerProfileRecordUtility.NameOffset + 1]));
    }

    [Fact]
    public void Build_reproduces_every_captured_record_outside_the_columns_it_cannot_supply()
    {
        foreach (var body in AllCapturedBodies)
        {
            var captured = PlayerProfileRecordParseUtils.Parse(body);
            Assert.NotNull(captured);

            var rebuilt = PlayerProfileRecordUtility.Build(
                recordSubType: captured.RecordSubType,
                rosterIndex: captured.RosterIndex,
                characterId: captured.CharacterId,
                name: captured.Name,
                clanName: captured.ClanName,
                appearance: captured.Appearance);

            Assert.Equal(body.Length, rebuilt.Length);
            foreach (var offset in Enumerable.Range(0, rebuilt.Length))
            {
                if (UnsuppliedColumns.Contains(offset))
                {
                    continue;
                }

                Assert.Equal(body[offset], rebuilt[offset]);
            }
        }
    }

    [Fact]
    public void Build_leaves_the_columns_it_cannot_supply_at_zero()
    {
        // Offsets 5 and 7 carry a per-player value the host fills from its own
        // state, as do thirteen columns of the block. Nothing decoded them, so
        // the builder writes zeros and this test states that rather than
        // letting the gap pass unnoticed.
        var body = PlayerProfileRecordUtility.Build(
            PlayerProfileRecordUtility.PlayerEntrySubType, 0, 65538, "host", "clan");

        Assert.Equal(0, body[5]);
        Assert.Equal(0, body[7]);
        foreach (var offset in PerPlayerBlockColumns.Where(offset => offset < 0x13 || offset > 0x1e))
        {
            Assert.Equal(0, body[offset]);
        }
    }

    [Fact]
    public void Every_captured_record_agrees_on_every_block_column_they_do_not_disagree_on()
    {
        // Offsets 0x0b..0x42 are not one opaque block. Across all six captured
        // records only thirteen columns carry anything per player, and every
        // other column is the same byte in all six. Of those, four are not
        // zero — 0x02 at 0x12, and the marker and trailer of each half of the
        // appearance block — and the builder writes them; the rest are zero,
        // which is what the builder writes too. This is what makes writing
        // zeros across the block right rather than merely a placeholder.
        for (var offset = 0x0b; offset < PlayerProfileRecordUtility.NameMarkerOffset; offset++)
        {
            if (PerPlayerBlockColumns.Contains(offset))
            {
                continue;
            }

            var values = AllCapturedBodies.Select(body => body[offset]).Distinct().ToArray();
            Assert.Single(values);
        }
    }

    [Fact]
    public void Build_writes_the_constants_the_captured_records_agree_are_not_zero()
    {
        var body = PlayerProfileRecordUtility.Build(
            PlayerProfileRecordUtility.PlayerEntrySubType, 0, 65538, "host", "clan");

        // Four columns of the block are the same byte in all six captured
        // records without being zero; the builder writes all four and zero
        // everywhere else in the block outside the appearance block itself.
        Assert.Equal(0x02, body[0x12]);
        Assert.Equal(0x16, body[0x18]);
        Assert.Equal(0x16, body[0x1e]);
        Assert.All(
            body[0x0b..PlayerProfileRecordUtility.AppearanceOffset]
                .Where((_, index) => index + 0x0b is not (0x12)),
            value => Assert.Equal(0, value));
    }

    [Fact]
    public void The_columns_that_carry_a_player_value_are_the_ones_the_records_disagree_on()
    {
        // The counterpart of the test above: the columns left out of it are
        // exactly the ones the six records disagree on, so the two together
        // account for the whole block and nothing is left unexplained.
        var varying = new List<int>();

        for (var offset = 0x0b; offset < PlayerProfileRecordUtility.NameMarkerOffset; offset++)
        {
            if (AllCapturedBodies.Select(body => body[offset]).Distinct().Count() > 1)
            {
                varying.Add(offset);
            }
        }

        Assert.Equal(PerPlayerBlockColumns.Order(), varying.Order());
    }

    [Fact]
    public void Build_truncates_a_name_rather_than_overrunning_the_frame()
    {
        var body = PlayerProfileRecordUtility.Build(
            recordSubType: PlayerProfileRecordUtility.PlayerEntrySubType,
            rosterIndex: 0,
            characterId: 65538,
            name: new string('x', PlayerProfileRecordUtility.MaximumNameLength + 10),
            clanName: string.Empty);

        var record = PlayerProfileRecordParseUtils.Parse(body);
        Assert.NotNull(record);
        Assert.Equal(new string('x', PlayerProfileRecordUtility.MaximumNameLength), record.Name);
    }
}