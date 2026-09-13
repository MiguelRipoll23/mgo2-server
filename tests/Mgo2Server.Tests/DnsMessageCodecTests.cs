using System.Text;
using Mgo2Server.Dns;

namespace Mgo2Server.Tests;

/// <summary>Exercises the query parser and the address response builder.</summary>
public sealed class DnsMessageCodecTests
{
    [Fact]
    public void Parses_the_first_question_of_a_query()
    {
        var query = BuildQuery(0x1234, "mgo2pc.com", DnsMessageCodec.AddressRecordType);

        var parsed = DnsMessageCodec.ParseQuery(query);

        Assert.NotNull(parsed);
        Assert.Equal("mgo2pc.com", parsed.Domain);
        Assert.Equal(DnsMessageCodec.AddressRecordType, parsed.QueryType);
    }

    [Fact]
    public void Lowercases_the_parsed_domain()
    {
        var query = BuildQuery(0x1234, "MGO2PC.COM", DnsMessageCodec.AddressRecordType);

        var parsed = DnsMessageCodec.ParseQuery(query);

        Assert.NotNull(parsed);
        Assert.Equal("mgo2pc.com", parsed.Domain);
    }

    [Fact]
    public void Rejects_a_truncated_datagram()
    {
        Assert.Null(DnsMessageCodec.ParseQuery(new byte[4]));
    }

    [Fact]
    public void Rejects_a_compression_pointer_cycle()
    {
        // The name at offset 12 is a pointer back to offset 12.
        var query = new byte[14];
        query[12] = 0xc0;
        query[13] = 0x0c;

        Assert.Null(DnsMessageCodec.ParseQuery(query));
    }

    [Fact]
    public void Rejects_a_label_that_runs_past_the_datagram()
    {
        // A five-byte label with only two bytes left in the datagram.
        var query = new byte[15];
        query[12] = 0x05;
        query[13] = (byte)'a';
        query[14] = (byte)'b';

        Assert.Null(DnsMessageCodec.ParseQuery(query));
    }

    [Fact]
    public void Rejects_a_question_without_a_class()
    {
        // Name "a", then only the two QTYPE bytes, no QCLASS.
        var query = new byte[17];
        query[12] = 0x01;
        query[13] = (byte)'a';
        query[14] = 0x00;
        query[15] = 0x00;
        query[16] = 0x01;

        Assert.Null(DnsMessageCodec.ParseQuery(query));
    }

    [Fact]
    public void Answers_a_query_with_one_address_record()
    {
        var query = BuildQuery(0xbeef, "mgo2pc.com", DnsMessageCodec.AddressRecordType);

        var response = DnsMessageCodec.BuildAddressResponse(query, "192.168.1.10");

        // Header: echoed transaction identifier, response flags, one question
        // and one answer.
        Assert.Equal(0xbe, response[0]);
        Assert.Equal(0xef, response[1]);
        Assert.Equal(0x85, response[2]);
        Assert.Equal(0x80, response[3]);
        Assert.Equal(0, response[4]);
        Assert.Equal(1, response[5]);
        Assert.Equal(0, response[6]);
        Assert.Equal(1, response[7]);

        // The question section is copied verbatim, and the answer is a single
        // address record with a five-minute time to live.
        Assert.Equal(query.Length - 12, response.Length - 12 - 16);
        Assert.True(response.AsSpan(12, query.Length - 12).SequenceEqual(query.AsSpan(12)));

        var answer = response.AsSpan(response.Length - 16);
        Assert.Equal([0xc0, 0x0c], answer[..2].ToArray());
        Assert.Equal([0x00, 0x01], answer[2..4].ToArray());
        Assert.Equal([0x00, 0x01], answer[4..6].ToArray());
        Assert.Equal([0x00, 0x00, 0x01, 0x2c], answer[6..10].ToArray());
        Assert.Equal([0x00, 0x04], answer[10..12].ToArray());
        Assert.Equal([192, 168, 1, 10], answer[12..16].ToArray());
    }

    /// <summary>Builds a single-question query the way the client does.</summary>
    private static byte[] BuildQuery(ushort transactionIdentifier, string domain, ushort queryType)
    {
        var labels = domain.Split('.');
        var length = 12 + labels.Sum(label => label.Length + 1) + 1 + 4;
        var query = new byte[length];

        query[0] = (byte)(transactionIdentifier >> 8);
        query[1] = (byte)transactionIdentifier;
        query[2] = 0x01; // Recursion desired.
        query[5] = 1; // One question.

        var offset = 12;
        foreach (var label in labels)
        {
            query[offset++] = (byte)label.Length;
            var encoded = Encoding.ASCII.GetBytes(label);
            Array.Copy(encoded, 0, query, offset, encoded.Length);
            offset += encoded.Length;
        }

        offset++; // Terminating zero.
        query[offset++] = (byte)(queryType >> 8);
        query[offset++] = (byte)queryType;
        query[offset++] = 0x00;
        query[offset] = 0x01; // Class: internet.

        return query;
    }
}
