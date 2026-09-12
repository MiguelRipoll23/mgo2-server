using System.Text;

namespace Mgo2Server.Dns;

/// <summary>First question of a DNS query.</summary>
/// <param name="Domain">Queried domain, lowercased.</param>
/// <param name="QueryType">Queried record type.</param>
public sealed record DnsQuery(string Domain, int QueryType);

/// <summary>
/// Reads the queries the client sends and writes the answers. Only the single
/// question shape the console uses is supported.
/// </summary>
public static class DnsMessageCodec
{
    /// <summary>Record type of an address record.</summary>
    public const int AddressRecordType = 1;

    /// <summary>Record type that matches every record.</summary>
    public const int AnyRecordType = 255;

    /// <summary>Offset the first question starts at, after the twelve-byte header.</summary>
    private const int QuestionOffset = 12;

    /// <summary>Time to live of the answers, in seconds.</summary>
    private const int TimeToLiveSeconds = 300;

    /// <summary>Parses the first question of a query.</summary>
    /// <param name="data">Datagram to parse.</param>
    /// <returns>The parsed question, or <c>null</c> when the datagram is malformed.</returns>
    public static DnsQuery? ParseQuery(byte[] data)
    {
        if (data.Length < QuestionOffset)
        {
            return null;
        }

        var (domain, nextOffset) = ParseDomainName(data, QuestionOffset);
        if (nextOffset + 1 >= data.Length)
        {
            return null;
        }

        var queryType = (data[nextOffset] << 8) | data[nextOffset + 1];
        return new DnsQuery(domain.ToLowerInvariant(), queryType);
    }

    /// <summary>
    /// Builds an address response for a query, echoing its question and
    /// answering with the resolved address.
    /// </summary>
    /// <param name="query">Query datagram to answer.</param>
    /// <param name="ipAddress">Address to answer with, in dotted-quad form.</param>
    public static byte[] BuildAddressResponse(byte[] query, string ipAddress)
    {
        var parts = ipAddress.Split('.').Select(byte.Parse).ToArray();

        // Walk past the question name, then past its type and class.
        var offset = QuestionOffset;
        while (offset < query.Length && query[offset] != 0)
        {
            offset += query[offset] + 1;
        }

        offset++; // Terminating zero.
        offset += 4; // Query type and class.

        var questionLength = offset - QuestionOffset;
        var response = new byte[QuestionOffset + questionLength + 16];

        response[0] = query[0];
        response[1] = query[1]; // Transaction identifier, echoed.
        response[2] = 0x85; // Response, authoritative, recursion desired.
        response[3] = 0x80; // Recursion available.
        response[5] = 1; // One question.
        response[7] = 1; // One answer.

        Array.Copy(query, QuestionOffset, response, QuestionOffset, questionLength);

        var cursor = QuestionOffset + questionLength;
        response[cursor++] = 0xc0;
        response[cursor++] = 0x0c; // Name pointer back to the question.
        response[cursor++] = 0x00;
        response[cursor++] = 0x01; // Type: address.
        response[cursor++] = 0x00;
        response[cursor++] = 0x01; // Class: internet.
        response[cursor++] = (byte)(TimeToLiveSeconds >> 24);
        response[cursor++] = (byte)(TimeToLiveSeconds >> 16);
        response[cursor++] = (byte)(TimeToLiveSeconds >> 8);
        response[cursor++] = unchecked((byte)TimeToLiveSeconds);
        response[cursor++] = 0x00;
        response[cursor++] = 0x04; // Length of the address.

        foreach (var part in parts)
        {
            response[cursor++] = part;
        }

        return response;
    }

    /// <summary>Reads a domain name, following compression pointers.</summary>
    /// <param name="data">Datagram to read from.</param>
    /// <param name="startOffset">Offset the name starts at.</param>
    private static (string Domain, int NextOffset) ParseDomainName(byte[] data, int startOffset)
    {
        var labels = new List<string>();
        var offset = startOffset;

        while (offset < data.Length)
        {
            var length = data[offset];

            if (length == 0)
            {
                offset++;
                break;
            }

            // A compression pointer sets the two top bits.
            if ((length & 0xc0) == 0xc0)
            {
                var pointer = ((length & 0x3f) << 8) | data[offset + 1];
                var (domain, _) = ParseDomainName(data, pointer);
                if (domain.Length > 0)
                {
                    labels.Add(domain);
                }

                offset += 2;
                break;
            }

            offset++;
            labels.Add(Encoding.UTF8.GetString(data, offset, length));
            offset += length;
        }

        return (string.Join('.', labels), offset);
    }
}
