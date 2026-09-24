using System.Text;
using Mgo2Server.Shared.Domain.Rankings;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Guards the header and record layout of a serialised board window, which is the
/// one reply in the server that is little-endian. The body is sent in the clear —
/// the retail service replies that way, and a scrambled body is read as a plain
/// record count, which the client rejects when it exceeds what was asked for.
/// </summary>
[Trait("Category", "Shared")]
public sealed class RankingBodyUtilityTests
{
    /// <summary>Deserialised view of a reply.</summary>
    private sealed record Reply(byte[] Body, int Count, int Total);

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
        Assert.Equal(0u, BinaryUtility.ReadUInt32LittleEndian(reply.Body, 8));
        Assert.Equal(
            RankingBodyUtils.HeaderSize + (RankingBodyUtils.RecordSize * 2),
            reply.Body.Length);
    }

    [Fact]
    public void Encode_lays_out_each_record_in_order()
    {
        var page = new RankingPage([new RankingEntry(3, 99, "Eva", 0x0201)], Total: 3);

        var body = Decode(page).Body;
        Assert.Equal(3u, BinaryUtility.ReadUInt32LittleEndian(body, RankingBodyUtils.HeaderSize));
        Assert.Equal(99u, BinaryUtility.ReadUInt32LittleEndian(body, RankingBodyUtils.HeaderSize + 4));
        Assert.Equal(0x0201u, BinaryUtility.ReadUInt32LittleEndian(body, RankingBodyUtils.HeaderSize + 24));
    }

    [Fact]
    public void Encode_pads_a_short_name_and_terminates_it()
    {
        var page = new RankingPage([new RankingEntry(1, 1, "Eva", 0)], Total: 1);

        var body = Decode(page).Body;
        var field = body.AsSpan(RankingBodyUtils.HeaderSize + 8, RankingBodyUtils.NameSize);

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

        var field = Decode(page).Body.AsSpan(RankingBodyUtils.HeaderSize + 8, RankingBodyUtils.NameSize);

        Assert.Equal("SixteenCharsNam", Encoding.Latin1.GetString(field[..15]));
        Assert.Equal(0, field[15]);
    }

    /// <summary>Reads the header back out of a serialised window.</summary>
    /// <param name="page">Window to serialise.</param>
    private static Reply Decode(RankingPage page)
    {
        var body = RankingBodyUtils.Encode(page);

        return new Reply(
            body,
            (int)BinaryUtility.ReadUInt32LittleEndian(body, 0),
            (int)BinaryUtility.ReadUInt32LittleEndian(body, 4));
    }
}
