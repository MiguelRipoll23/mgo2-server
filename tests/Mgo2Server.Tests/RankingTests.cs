using System.Text;
using Mgo2Server.Shared.Domain.Rankings;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the ranking reply, which is the one place the wire format is
/// little-endian and XOR-scrambled. A cleartext or wrongly-ordered reply is not
/// rejected by the client — it is silently misparsed — so the keystream and the
/// record layout are asserted against the client's own loop.
/// </summary>
public sealed class RankingScrambleUtilityTests
{
    /// <summary>
    /// The keystream, read off the client's scramble loop: byte <c>i</c> uses
    /// <c>key[((i / 4) % 5) + (i % 4)]</c>, which repeats with a period of twenty.
    /// </summary>
    [Fact]
    public void KeyIndex_follows_the_period_of_twenty()
    {
        int[] expected =
        [
            0, 1, 2, 3,
            1, 2, 3, 4,
            2, 3, 4, 5,
            3, 4, 5, 6,
            4, 5, 6, 7,
        ];

        for (var offset = 0; offset < expected.Length * 2; offset++)
        {
            Assert.Equal(expected[offset % expected.Length], RankingScrambleUtils.KeyIndex(offset));
        }
    }

    [Fact]
    public void Apply_xors_the_documented_key_bytes()
    {
        var body = new byte[8];

        RankingScrambleUtils.Apply(body);

        Assert.Equal<byte[]>([0x8B, 0x75, 0x2C, 0x90, 0x75, 0x2C, 0x90, 0x3A], body);
    }

    [Fact]
    public void Apply_is_its_own_inverse()
    {
        byte[] body = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21];
        var original = body.ToArray();

        RankingScrambleUtils.Apply(body);
        Assert.NotEqual(original, body);

        RankingScrambleUtils.Apply(body);
        Assert.Equal(original, body);
    }
}

/// <summary>Guards the header and record layout of a serialised board window.</summary>
public sealed class RankingBodyUtilityTests
{
    /// <summary>Deserialised view of a reply, produced by reversing the scramble.</summary>
    private sealed record Reply(byte[] Clear, int Count, int Total);

    [Fact]
    public void Encode_writes_the_header_count_and_total()
    {
        var page = new RankingPage(
            [
                new RankingEntry(1, 4242, "Snake", 900),
                new RankingEntry(2, 7, "Ocelot", 300),
            ],
            Total: 57);

        var reply = Decode(page);

        Assert.Equal(2, reply.Count);
        Assert.Equal(57, reply.Total);
        Assert.Equal(0u, BinaryUtility.ReadUInt32LittleEndian(reply.Clear, 8));
        Assert.Equal(
            RankingBodyUtils.HeaderSize + (RankingBodyUtils.RecordSize * 2),
            reply.Clear.Length);
    }

    [Fact]
    public void Encode_lays_out_each_record_in_order()
    {
        var page = new RankingPage([new RankingEntry(3, 99, "Eva", 0x0201)], Total: 3);

        var clear = Decode(page).Clear;
        Assert.Equal(3u, BinaryUtility.ReadUInt32LittleEndian(clear, RankingBodyUtils.HeaderSize));
        Assert.Equal(99u, BinaryUtility.ReadUInt32LittleEndian(clear, RankingBodyUtils.HeaderSize + 4));
        Assert.Equal(0x0201u, BinaryUtility.ReadUInt32LittleEndian(clear, RankingBodyUtils.HeaderSize + 24));
    }

    [Fact]
    public void Encode_pads_a_short_name_and_terminates_it()
    {
        var page = new RankingPage([new RankingEntry(1, 1, "Eva", 0)], Total: 1);

        var clear = Decode(page).Clear;
        var field = clear.AsSpan(RankingBodyUtils.HeaderSize + 8, RankingBodyUtils.NameSize);

        Assert.Equal("Eva", Encoding.Latin1.GetString(field[..3]));
        Assert.Equal(0, field[3]);
        Assert.All(field[4..].ToArray(), value => Assert.Equal(0, value));
    }

    /// <summary>
    /// A name that fills the field would leave the client's <c>strlen</c> running
    /// into whatever its stack frame held, so the last character is dropped.
    /// </summary>
    [Fact]
    public void Encode_truncates_a_full_length_name_and_keeps_a_terminator()
    {
        var page = new RankingPage([new RankingEntry(1, 1, "SixteenCharsName!", 0)], Total: 1);

        var clear = Decode(page).Clear;
        var field = clear.AsSpan(RankingBodyUtils.HeaderSize + 8, RankingBodyUtils.NameSize);

        Assert.Equal("SixteenCharsNam", Encoding.Latin1.GetString(field[..15]));
        Assert.Equal(0, field[15]);
    }

    /// <summary>Reverses the scramble and reads the header back.</summary>
    /// <param name="page">Window to serialise.</param>
    private static Reply Decode(RankingPage page)
    {
        var clear = RankingBodyUtils.Encode(page);
        RankingScrambleUtils.Apply(clear);

        return new Reply(
            clear,
            (int)BinaryUtility.ReadUInt32LittleEndian(clear, 0),
            (int)BinaryUtility.ReadUInt32LittleEndian(clear, 4));
    }
}
