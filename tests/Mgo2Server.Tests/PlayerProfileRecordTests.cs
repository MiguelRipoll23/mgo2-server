using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the <c>0x1001</c> player-profile record against the bytes a real host
/// wrote into a recorded survival match. The vectors below are the record
/// bodies copied verbatim from <c>tools/replays/replay_360827_5.dat</c>, so a
/// field that moves in the builder fails here rather than on a live client.
/// </summary>
[Trait("Category", "Shared")]
public sealed class PlayerProfileRecordTests
{
    /// <summary>
    /// The host's own record, the one at file offset 0x52. It is the only
    /// record in the roster written before the roster itself, and the only one
    /// whose roster index is below zero.
    /// </summary>
    private const string RecordedHostBody =
        "074d00003105000528be000000000000010002000000000000000000000000211c00000000000000000000000000000000000000000000000000000000000000000000034465616473686f740047756e73686970";

    /// <summary>
    /// The ten full-length records the host recorded, in roster order. Each is
    /// the profile of the player whose record follows it in the file.
    /// </summary>
    private static readonly string[] RecordedBodies =
    [
        "0750000032060006138d0000000000000200020000000000000000000000004d0e000000000000000000000000000000000000000000000000000000000000000000000343656c65737469610046694e414c20424f5353",
        "074c000033070007b111010000000000010002000000000000000000000000742100000000000000000000000000000000000000000000000000000000000000000000035562756e745500446f6c7068696e73",
        "074e0000340800083dbe000000000000010002000000000000000000000000981400003800000000000000000002000000000000000000000000000000000000000000035369636b00546572726f72205371756164",
        "07530000350900095908000000000000010002000000000000000000000000eb0d00000000000000000000000000000000000000000000000000000000000000000000034b696d6964616b69004d474f50432041636164656d79",
        "074d0000360a000ab46e000000000000010002000000000000000000000000cb120000000000000000000000000000000000000000000000000000000000000000000003476a6572676a00504f50532f504c5553",
        "074d0000370b000bd3190100000000000200020000000000000000000000001b230000000000000000000000000000000000000000000000000000000000000000000003467265657a6500466c6173684261636b",
        "07460000380c000c4f14010000000000010002000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002a67616d6269742a00",
        "07560000390d000db8bb000000000000010002000000000000000000000000ae1a0000000000000000000000000600000000000000000000000000000000000000000003546f6d6d792053686c75670053686c75672045697265616e6e",
        "075200003a0e000e46c9000000000000010002000000000000000000000000742100000000000000000000000026000000000000000000080000000000000000000000037a656b6f2d64657374726f7900446f6c7068696e73",
        "074d00003b0f000ff1e100000000000002000200000000000000000000000074210000280000000000000000000809000000000000000000000800000000000000000003414d4720446f4e00446f6c7068696e73",
        "074b00003c10001051c2000000000000020002000000000000000000000000211c00000000000000000000000000000000000000000000000000000000000000000000034d616c656e610047756e73686970",
    ];

    /// <summary>
    /// The eight columns of the per-player block that differ between the
    /// recorded records. Every other column of the block is the same byte in all
    /// twelve, so the builder is expected to reproduce those.
    /// </summary>
    private static readonly HashSet<int> PerPlayerColumns = [0x10, 0x1f, 0x20, 0x23, 0x2d, 0x2e, 0x37, 0x39];

    [Fact]
    public void Parse_reads_the_name_and_clan_of_every_recorded_record()
    {
        var expected = new (string Name, string Clan)[]
        {
            ("Celestia", "FiNAL BOSS"),
            ("UbuntU", "Dolphins"),
            ("Sick", "Terror Squad"),
            ("Kimidaki", "MGOPC Academy"),
            ("Gjergj", "POPS/PLUS"),
            ("Freeze", "FlashBack"),
            ("*gambit*", string.Empty),
            ("Tommy Shlug", "Shlug Eireann"),
            ("zeko-destroy", "Dolphins"),
            ("AMG DoN", "Dolphins"),
            ("Malena", "Gunship"),
        };

        Assert.Equal(expected.Length, RecordedBodies.Length);
        for (var index = 0; index < RecordedBodies.Length; index++)
        {
            var record = PlayerProfileRecordUtility.Parse(Convert.FromHexString(RecordedBodies[index]));
            Assert.NotNull(record);
            Assert.Equal(expected[index].Name, record.Name);
            Assert.Equal(expected[index].Clan, record.ClanName);
        }
    }

    [Fact]
    public void Parse_reads_the_fixed_fields_of_every_recorded_record()
    {
        for (var index = 0; index < RecordedBodies.Length; index++)
        {
            var body = Convert.FromHexString(RecordedBodies[index]);
            var record = PlayerProfileRecordUtility.Parse(body);

            Assert.NotNull(record);
            Assert.Equal(PlayerProfileRecordUtility.RecordVersion, record.Version);

            // The field at offset 4 is the roster index biased by 0x32, and the
            // field at offset 5 is the same index biased by 6. Both tie the
            // record to its position in the room.
            Assert.Equal((byte)(0x32 + index), record.RosterBaseField);
            Assert.Equal((sbyte)index, record.RosterIndex);
            // The field at offset 5 carries the same index under a different
            // base, so it recovers to the same value.
            Assert.Equal((sbyte)index, record.PlayerNumber);
        }
    }

    [Fact]
    public void Parse_places_the_host_below_the_first_joining_player()
    {
        // The host is not a joining player, so its slot sits below the first one:
        // the recorded host record carries 0x31 and 0x05 where the first joining
        // player's record carries 0x32 and 0x06. Reading the index as unsigned
        // would wrap it to 255 and put the host at the end of the room instead.
        var record = PlayerProfileRecordUtility.Parse(Convert.FromHexString(RecordedHostBody));

        Assert.NotNull(record);
        Assert.Equal(PlayerProfileRecordUtility.HostRosterIndex, record.RosterIndex);
        Assert.Equal(0x31, record.RosterBaseField);
        Assert.Equal(PlayerProfileRecordUtility.HostRosterIndex, record.PlayerNumber);
        Assert.Equal("Deadshot", record.Name);
        Assert.Equal("Gunship", record.ClanName);
    }

    [Fact]
    public void Build_places_the_host_below_the_first_joining_player()
    {
        var body = PlayerProfileRecordUtility.Build(
            characterIdentifier: 0x4d,
            rosterIndex: PlayerProfileRecordUtility.HostRosterIndex,
            perPlayerValue: 0xbe28,
            teamFlag: 0,
            name: "Deadshot",
            clanName: "Gunship");

        var recorded = Convert.FromHexString(RecordedHostBody);
        Assert.Equal(recorded[..0x10], body[..0x10]);
        Assert.Equal(
            recorded[PlayerProfileRecordUtility.NameMarkerOffset..],
            body[PlayerProfileRecordUtility.NameMarkerOffset..]);
    }

    [Fact]
    public void Parse_rejects_a_body_shorter_than_the_fixed_fields()
    {
        Assert.Null(PlayerProfileRecordUtility.Parse(new byte[PlayerProfileRecordUtility.NameOffset]));
        Assert.Null(PlayerProfileRecordUtility.Parse(new byte[PlayerProfileRecordUtility.NameOffset + 1]));
    }    [Fact]
    public void Build_reproduces_the_header_and_names_of_every_recorded_record()
    {
        foreach (var hex in RecordedBodies)
        {
            var recorded = Convert.FromHexString(hex);
            var record = PlayerProfileRecordUtility.Parse(recorded);
            Assert.NotNull(record);

            var rebuilt = PlayerProfileRecordUtility.Build(
                characterIdentifier: record.CharacterIdentifier,
                rosterIndex: record.RosterIndex,
                perPlayerValue: record.PerPlayerValue,
                teamFlag: record.TeamFlag,
                name: record.Name,
                clanName: record.ClanName);

            // The builder covers everything but the eight columns of the
            // per-player block that differ between players. Those are compared
            // separately below, because nothing decoded what the host puts in
            // them yet.
            Assert.Equal(recorded.Length, rebuilt.Length);
            foreach (var offset in Enumerable.Range(0, rebuilt.Length))
            {
                if (PerPlayerColumns.Contains(offset))
                {
                    continue;
                }

                Assert.Equal(recorded[offset], rebuilt[offset]);
            }
            Assert.Equal(recorded[PlayerProfileRecordUtility.NameMarkerOffset..],
                rebuilt[PlayerProfileRecordUtility.NameMarkerOffset..]);
        }
    }

    [Fact]
    public void Build_leaves_the_per_player_columns_it_cannot_still_supply_at_zero()
    {
        // Eight columns of the block carry a per-player value the host fills
        // from its own state, and nothing decoded them yet. The builder writes
        // zeros there and this test states that, rather than letting the gap
        // pass unnoticed.
        var body = PlayerProfileRecordUtility.Build(
            characterIdentifier: 1,
            rosterIndex: 0,
            perPlayerValue: 0,
            teamFlag: 0,
            name: "host",
            clanName: "clan");

        foreach (var offset in new[] { 0x10, 0x1f, 0x20, 0x23, 0x2d, 0x2e, 0x37, 0x39 })
        {
            Assert.Equal(0, body[offset]);
        }
    }

    [Fact]
    public void Every_recorded_record_agrees_on_every_column_they_do_not_disagree_on()
    {
        // Offsets 0x0b..0x42 are not one opaque block. Across all twelve
        // recorded records only eight columns carry anything per player, and
        // every other column is the same byte in all twelve. Of those, one is
        // not zero — 0x02 at 0x12 — and the builder writes that one; the rest
        // are zero, which is what the builder writes too. This is what makes
        // writing zeros across the block right rather than merely a
        // placeholder, and a record that starts filling those columns fails
        // here.
        var varying = PerPlayerColumns;
        var bodies = RecordedBodies.Append(RecordedHostBody).Select(Convert.FromHexString).ToArray();

        for (var offset = 0x0b; offset < PlayerProfileRecordUtility.NameMarkerOffset; offset++)
        {
            if (varying.Contains(offset))
            {
                continue;
            }

            var values = bodies.Select(body => body[offset]).Distinct().ToArray();
            Assert.Single(values);
        }
    }

    [Fact]
    public void Build_writes_the_one_constant_the_recorded_records_agree_is_not_zero()
    {
        var body = PlayerProfileRecordUtility.Build(
            characterIdentifier: 1,
            rosterIndex: 0,
            perPlayerValue: 0,
            teamFlag: 0,
            name: "host",
            clanName: "clan");

        // The other forty-seven columns outside the eight that vary per player
        // are zero in every recorded record, and zero is what the builder writes.
        Assert.All(
            body[0x0b..PlayerProfileRecordUtility.NameMarkerOffset].Where((_, index) => index + 0x0b != 0x12),
            value => Assert.Equal(0, value));
    }

    [Fact]
    public void The_columns_that_carry_a_player_value_are_the_ones_the_records_disagree_on()
    {
        // The counterpart of the test above: the eight columns left out of it
        // are exactly the ones the twelve records disagree on, so the two
        // together account for the whole block and nothing is left unexplained.
        var bodies = RecordedBodies.Append(RecordedHostBody).Select(Convert.FromHexString).ToArray();
        var varying = new List<int>();

        for (var offset = 0x0b; offset < PlayerProfileRecordUtility.NameMarkerOffset; offset++)
        {
            if (bodies.Select(body => body[offset]).Distinct().Count() > 1)
            {
                varying.Add(offset);
            }
        }

        Assert.Equal([0x10, 0x1f, 0x20, 0x23, 0x2d, 0x2e, 0x37, 0x39], varying);
    }

    [Fact]
    public void Build_truncates_a_name_rather_than_overrunning_the_frame()
    {
        var body = PlayerProfileRecordUtility.Build(
            characterIdentifier: 1,
            rosterIndex: 0,
            perPlayerValue: 0,
            teamFlag: 0,
            name: new string('x', PlayerProfileRecordUtility.MaximumNameLength + 10),
            clanName: string.Empty);

        var record = PlayerProfileRecordUtility.Parse(body);
        Assert.NotNull(record);
        Assert.Equal(new string('x', PlayerProfileRecordUtility.MaximumNameLength), record.Name);
    }
}
