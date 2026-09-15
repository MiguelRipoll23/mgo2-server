using System.Net;
using Mgo2Server.Stun;

namespace Mgo2Server.Tests;

/// <summary>Exercises the binding request parser and the binding response builder.</summary>
public sealed class StunMessageCodecTests
{
    /// <summary>
    /// Transaction identifier of a capture of the real server, which the
    /// obfuscated address of these tests is keyed on.
    /// </summary>
    private static readonly byte[] TransactionIdentifier =
    [
        0xeb, 0x55, 0xd7, 0x21, 0x4f, 0x1a, 0x8c, 0x3d,
        0x9e, 0x72, 0x05, 0xb6, 0xc1, 0xd8, 0x47, 0x90,
    ];

    /// <summary>The observed address of the client of that capture.</summary>
    private static readonly IPEndPoint MappedAddress = new(IPAddress.Parse("47.205.42.160"), 5730);

    private static readonly IPEndPoint SourceAddress = new(IPAddress.Parse("192.168.1.50"), 3478);

    private static readonly IPEndPoint ChangedAddress = new(IPAddress.Parse("192.168.1.201"), 3479);

    [Fact]
    public void Parses_a_binding_request()
    {
        var datagram = BuildRequest();

        var parsed = StunMessageCodec.TryParseBindingRequest(datagram, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(TransactionIdentifier, request.TransactionIdentifier);
        Assert.Equal(0, request.ChangeRequestFlags);
    }

    [Fact]
    public void Parses_a_header_only_keepalive()
    {
        var datagram = BuildRequest(body: []);

        var parsed = StunMessageCodec.TryParseBindingRequest(datagram, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(0, request.ChangeRequestFlags);
    }

    [Fact]
    public void Reads_the_change_request_flags()
    {
        var datagram = BuildRequest(
            Attributes(StunMessageCodec.ChangeRequestType, [0x00, 0x00, 0x00, 0x06]));

        var parsed = StunMessageCodec.TryParseBindingRequest(datagram, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);
        Assert.Equal(6, request.ChangeRequestFlags);
    }

    [Fact]
    public void Accepts_the_vendor_attribute_without_echoing_it()
    {
        // The observed probe of the console: its own 0xf000 attribute, sub-type 2.
        var datagram = BuildRequest(
            Attributes(
                0xf000,
                [0x05, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02]));

        var parsed = StunMessageCodec.TryParseBindingRequest(datagram, out var request);

        Assert.True(parsed);
        Assert.NotNull(request);

        var response = StunMessageCodec.BuildBindingResponse(
            request,
            MappedAddress,
            SourceAddress,
            ChangedAddress);

        var attributeTypes = ReadAttributes(response).Select(attribute => attribute.Type).ToArray();
        Assert.Equal(
            [
                StunMessageCodec.MappedAddressType,
                StunMessageCodec.SourceAddressType,
                StunMessageCodec.ChangedAddressType,
                StunMessageCodec.XorMappedAddressType,
            ],
            attributeTypes);
        Assert.DoesNotContain(0xf000, attributeTypes);
    }

    [Fact]
    public void Rejects_a_truncated_datagram()
    {
        Assert.False(StunMessageCodec.TryParseBindingRequest(new byte[12], out _));
    }

    [Fact]
    public void Rejects_a_length_that_disagrees_with_the_datagram()
    {
        // A header declaring four bytes of body and a datagram that carries none.
        var datagram = BuildRequest(body: []);
        datagram[2] = 0x00;
        datagram[3] = 0x04;

        Assert.False(StunMessageCodec.TryParseBindingRequest(datagram, out _));
    }

    [Fact]
    public void Rejects_a_binding_response()
    {
        var datagram = BuildRequest();
        datagram[1] = 0x01;
        datagram[0] = 0x01;

        Assert.False(StunMessageCodec.TryParseBindingRequest(datagram, out _));
    }

    [Fact]
    public void Rejects_an_attribute_that_runs_past_the_datagram()
    {
        // One attribute header declaring four bytes of value, with none behind it.
        var datagram = BuildRequest(body: [0x00, 0x03, 0x00, 0x04]);

        Assert.False(StunMessageCodec.TryParseBindingRequest(datagram, out _));
    }

    [Fact]
    public void Answers_with_a_binding_response_that_echoes_the_transaction_identifier()
    {
        var request = new StunBindingRequest(TransactionIdentifier, 0);

        var response = StunMessageCodec.BuildBindingResponse(
            request,
            MappedAddress,
            SourceAddress,
            ChangedAddress);

        Assert.Equal(0x01, response[0]);
        Assert.Equal(0x01, response[1]);
        Assert.Equal(response.Length - StunMessageCodec.HeaderLength, (response[2] << 8) | response[3]);
        Assert.Equal(TransactionIdentifier, response[4..StunMessageCodec.HeaderLength]);
    }

    [Fact]
    public void Preserves_the_mapped_port_and_reports_the_two_server_addresses()
    {
        var request = new StunBindingRequest(TransactionIdentifier, 0);

        var response = StunMessageCodec.BuildBindingResponse(
            request,
            MappedAddress,
            SourceAddress,
            ChangedAddress);

        var attributes = ReadAttributes(response);

        Assert.Equal(MappedAddress, ReadAddress(attributes[0].Value));
        Assert.Equal(SourceAddress, ReadAddress(attributes[1].Value));
        Assert.Equal(ChangedAddress, ReadAddress(attributes[2].Value));
    }

    [Fact]
    public void Obfuscates_the_mapped_address_with_the_transaction_identifier()
    {
        var request = new StunBindingRequest(TransactionIdentifier, 0);

        var response = StunMessageCodec.BuildBindingResponse(
            request,
            MappedAddress,
            SourceAddress,
            ChangedAddress);

        // Tags 0x8020 rather than the 0x0020 of RFC 5389, and the key is the
        // transaction identifier rather than a magic cookie.
        var xorMapped = ReadAttributes(response)[3];
        Assert.Equal(StunMessageCodec.XorMappedAddressType, xorMapped.Type);

        // Verified against the capture: port 5730 and address 47.205.42.160 come
        // out as fd37 and c498fd81 under this transaction identifier.
        Assert.Equal([0xfd, 0x37], xorMapped.Value[2..4]);
        Assert.Equal([0xc4, 0x98, 0xfd, 0x81], xorMapped.Value[4..8]);

        Assert.Equal(MappedAddress, ReadXorAddress(xorMapped.Value, TransactionIdentifier));
    }

    [Fact]
    public void Writes_the_four_attributes_in_the_order_the_console_expects()
    {
        var request = new StunBindingRequest(TransactionIdentifier, 0);

        var response = StunMessageCodec.BuildBindingResponse(
            request,
            MappedAddress,
            SourceAddress,
            ChangedAddress);

        // Four attributes of eight bytes each, in this order, and no trace of the
        // standard 0x0020 tag.
        Assert.Equal(68, response.Length);

        var attributeTypes = ReadAttributes(response).Select(attribute => attribute.Type).ToArray();
        Assert.Equal([0x0001, 0x0004, 0x0005, 0x8020], attributeTypes);
        Assert.DoesNotContain(0x0020, attributeTypes);
    }

    /// <summary>Builds a binding request the way the console does.</summary>
    /// <param name="body">Attribute section of the request.</param>
    private static byte[] BuildRequest(byte[]? body = null)
    {
        body ??= Attributes(StunMessageCodec.ChangeRequestType, [0x00, 0x00, 0x00, 0x00]);

        var datagram = new byte[StunMessageCodec.HeaderLength + body.Length];

        datagram[1] = 0x01; // A binding request.
        datagram[2] = (byte)(body.Length >> 8);
        datagram[3] = (byte)body.Length;
        TransactionIdentifier.CopyTo(datagram, 4);
        body.CopyTo(datagram, StunMessageCodec.HeaderLength);

        return datagram;
    }

    /// <summary>Writes one attribute and the padding that aligns the next one.</summary>
    /// <param name="attributeType">Type of the attribute.</param>
    /// <param name="value">Value of the attribute.</param>
    private static byte[] Attributes(int attributeType, byte[] value)
    {
        var attribute = new byte[4 + value.Length + (4 - value.Length % 4) % 4];

        attribute[0] = (byte)(attributeType >> 8);
        attribute[1] = (byte)attributeType;
        attribute[2] = (byte)(value.Length >> 8);
        attribute[3] = (byte)value.Length;
        value.CopyTo(attribute, 4);

        return attribute;
    }

    /// <summary>Reads the attribute section of a message.</summary>
    /// <param name="message">Message to read.</param>
    private static IReadOnlyList<(int Type, byte[] Value)> ReadAttributes(byte[] message)
    {
        var bodyLength = (message[2] << 8) | message[3];
        var attributes = new List<(int Type, byte[] Value)>();

        var offset = StunMessageCodec.HeaderLength;
        while (offset + 4 <= StunMessageCodec.HeaderLength + bodyLength)
        {
            var attributeType = (message[offset] << 8) | message[offset + 1];
            var attributeLength = (message[offset + 2] << 8) | message[offset + 3];

            attributes.Add((attributeType, message[(offset + 4)..(offset + 4 + attributeLength)]));
            offset += 4 + attributeLength + (4 - attributeLength % 4) % 4;
        }

        return attributes;
    }

    /// <summary>Reads the address an address attribute value holds.</summary>
    /// <param name="value">Value to read.</param>
    private static IPEndPoint ReadAddress(byte[] value) =>
        new(new IPAddress(value[4..8]), (value[2] << 8) | value[3]);

    /// <summary>Reads the address an obfuscated address attribute value holds.</summary>
    /// <param name="value">Value to read.</param>
    /// <param name="transactionIdentifier">Key the value was obfuscated with.</param>
    private static IPEndPoint ReadXorAddress(byte[] value, byte[] transactionIdentifier)
    {
        var port = ((value[2] << 8) | value[3]) ^ ((transactionIdentifier[0] << 8) | transactionIdentifier[1]);

        var address = new byte[4];
        for (var index = 0; index < address.Length; index++)
        {
            address[index] = (byte)(value[index + 4] ^ transactionIdentifier[index]);
        }

        return new IPEndPoint(new IPAddress(address), port);
    }
}
