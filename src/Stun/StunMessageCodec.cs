using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Mgo2Server.Stun;

/// <summary>
/// A Binding Request of the console's port check, with the change it asked the
/// responder to make.
/// </summary>
/// <param name="TransactionIdentifier">
/// The sixteen bytes of the request, echoed in the answer so the console can pair
/// the two.
/// </param>
/// <param name="ChangeRequestFlags">
/// The flags of its CHANGE-REQUEST attribute, zero when it sent none.
/// </param>
public sealed record StunBindingRequest(byte[] TransactionIdentifier, int ChangeRequestFlags);

/// <summary>
/// Reads the Binding Requests the port check sends and writes the Binding Response
/// it expects.
/// </summary>
/// <remarks>
/// The console speaks the dialect of draft-ietf-behave-rfc3489bis-02, the 2005
/// working draft it shipped against, not RFC 5389: the transaction identifier is
/// sixteen bytes wide, there is no magic cookie, and the obfuscated address is
/// tagged 0x8020 XORed against the transaction identifier. That is a consistent
/// snapshot of one revision, not a set of separate quirks.
/// </remarks>
public static class StunMessageCodec
{
    /// <summary>Length of the header every message starts with.</summary>
    public const int HeaderLength = 20;

    /// <summary>Message type of a Binding Request.</summary>
    public const int BindingRequestType = 0x0001;

    /// <summary>Message type of a Binding Response.</summary>
    public const int BindingResponseType = 0x0101;

    /// <summary>Attribute carrying the address of the console as the responder sees it.</summary>
    public const int MappedAddressType = 0x0001;

    /// <summary>Attribute asking the answer to come from another address or port.</summary>
    public const int ChangeRequestType = 0x0003;

    /// <summary>Attribute carrying the address and port this answer comes from.</summary>
    public const int SourceAddressType = 0x0004;

    /// <summary>Attribute carrying the responder's other address and port.</summary>
    public const int ChangedAddressType = 0x0005;

    /// <summary>
    /// Attribute carrying the mapped address with its port and address XORed
    /// against the transaction identifier.
    /// </summary>
    /// <remarks>
    /// 0x8020 rather than the 0x0020 of RFC 5389, which is what the server the
    /// console was written against sent. The console accepts both, so this is the
    /// faithful choice rather than the required one.
    /// </remarks>
    public const int XorMappedAddressType = 0x8020;

    /// <summary>Flag of a CHANGE-REQUEST asking for a different address.</summary>
    public const int ChangeIpFlag = 0x04;

    /// <summary>Flag of a CHANGE-REQUEST asking for a different port.</summary>
    public const int ChangePortFlag = 0x02;

    /// <summary>Value that marks the address inside an address attribute as IPv4.</summary>
    private const byte AddressFamilyIpv4 = 0x01;

    /// <summary>Length of the address value an address attribute carries.</summary>
    private const int AddressValueLength = 8;

    /// <summary>
    /// Reads a datagram as a Binding Request.
    /// </summary>
    /// <param name="datagram">Datagram to read.</param>
    /// <param name="request">
    /// The parsed request, or <c>null</c> when the datagram is not a well formed
    /// Binding Request.
    /// </param>
    /// <remarks>
    /// The header declares the exact length of the attribute section and the
    /// attributes are four-byte aligned, so a datagram that disagrees with its own
    /// header is rejected before the attributes are walked. The console sends
    /// header-only keepalives, which this accepts.
    /// </remarks>
    public static bool TryParseBindingRequest(
        byte[] datagram,
        [NotNullWhen(true)] out StunBindingRequest? request)
    {
        request = null;

        if (datagram.Length < HeaderLength)
        {
            return false;
        }

        var messageType = (datagram[0] << 8) | datagram[1];
        if (messageType != BindingRequestType)
        {
            return false;
        }

        var bodyLength = (datagram[2] << 8) | datagram[3];
        if (bodyLength % 4 != 0 || datagram.Length != HeaderLength + bodyLength)
        {
            return false;
        }

        var transactionIdentifier = datagram[4..HeaderLength];

        // Everything but CHANGE-REQUEST is ignored. A comprehension-optional
        // attribute of the vendor range (0x8000-0xFFFF) must never be echoed: the
        // console dispatches on the sub-type of its own 0xf000 attribute, and a
        // sub-type it has no handler for leaves it spinning forever behind the
        // "Adjusting port settings" screen.
        var changeRequestFlags = 0;

        var offset = HeaderLength;
        while (offset + 4 <= HeaderLength + bodyLength)
        {
            var attributeType = (datagram[offset] << 8) | datagram[offset + 1];
            var attributeLength = (datagram[offset + 2] << 8) | datagram[offset + 3];

            var valueOffset = offset + 4;
            if (valueOffset + attributeLength > datagram.Length)
            {
                return false;
            }

            if (attributeType == ChangeRequestType && attributeLength >= 4)
            {
                changeRequestFlags =
                    (datagram[valueOffset] << 24) |
                    (datagram[valueOffset + 1] << 16) |
                    (datagram[valueOffset + 2] << 8) |
                    datagram[valueOffset + 3];
            }

            var padding = (4 - attributeLength % 4) % 4;
            offset = valueOffset + attributeLength + padding;
        }

        request = new StunBindingRequest(transactionIdentifier, changeRequestFlags);
        return true;
    }

    /// <summary>
    /// Writes the Binding Response of a request.
    /// </summary>
    /// <param name="request">Request being answered.</param>
    /// <param name="mappedAddress">Address of the console as the responder observed it.</param>
    /// <param name="sourceAddress">Address and port this answer is sent from.</param>
    /// <param name="changedAddress">The responder's other address and port.</param>
    /// <remarks>
    /// The four attributes go out in this order, which is the order the real server
    /// used and which the console is hardcoded to: the port of MAPPED-ADDRESS is
    /// what the console compares against the port it sent from, and it is preserved
    /// exactly.
    /// </remarks>
    public static byte[] BuildBindingResponse(
        StunBindingRequest request,
        IPEndPoint mappedAddress,
        IPEndPoint sourceAddress,
        IPEndPoint changedAddress)
    {
        var body = new List<byte>();

        AppendAttribute(body, MappedAddressType, AddressValue(mappedAddress));
        AppendAttribute(body, SourceAddressType, AddressValue(sourceAddress));
        AppendAttribute(body, ChangedAddressType, AddressValue(changedAddress));
        AppendAttribute(
            body,
            XorMappedAddressType,
            XorAddressValue(mappedAddress, request.TransactionIdentifier));

        var response = new byte[HeaderLength + body.Count];

        response[0] = (byte)(BindingResponseType >> 8);
        response[1] = (byte)(BindingResponseType & 0xff);
        response[2] = (byte)(body.Count >> 8);
        response[3] = (byte)body.Count;

        request.TransactionIdentifier.CopyTo(response, 4);
        body.CopyTo(response, HeaderLength);

        return response;
    }

    /// <summary>Appends one attribute and the padding that aligns the next one.</summary>
    /// <param name="body">Attribute section being built.</param>
    /// <param name="attributeType">Type of the attribute.</param>
    /// <param name="value">Value of the attribute.</param>
    private static void AppendAttribute(List<byte> body, int attributeType, byte[] value)
    {
        body.Add((byte)(attributeType >> 8));
        body.Add((byte)attributeType);
        body.Add((byte)(value.Length >> 8));
        body.Add((byte)value.Length);
        body.AddRange(value);

        for (var padding = (4 - value.Length % 4) % 4; padding > 0; padding--)
        {
            body.Add(0);
        }
    }

    /// <summary>Writes an address as one address attribute value holds it.</summary>
    /// <param name="endPoint">Address and port to write.</param>
    private static byte[] AddressValue(IPEndPoint endPoint)
    {
        var value = new byte[AddressValueLength];

        value[1] = AddressFamilyIpv4;
        value[2] = (byte)(endPoint.Port >> 8);
        value[3] = (byte)endPoint.Port;
        endPoint.Address.GetAddressBytes().CopyTo(value, 4);

        return value;
    }

    /// <summary>
    /// Writes an address as the obfuscated address attribute holds it: the port
    /// and the address XORed against the request's transaction identifier, both
    /// big-endian.
    /// </summary>
    /// <param name="endPoint">Address and port to write.</param>
    /// <param name="transactionIdentifier">Transaction identifier of the request.</param>
    private static byte[] XorAddressValue(IPEndPoint endPoint, byte[] transactionIdentifier)
    {
        var value = AddressValue(endPoint);

        // The port against the first two bytes of the transaction identifier, the
        // address against the first four; both start at the beginning of it.
        value[2] ^= transactionIdentifier[0];
        value[3] ^= transactionIdentifier[1];

        for (var index = 0; index < 4; index++)
        {
            value[4 + index] ^= transactionIdentifier[index];
        }

        return value;
    }
}
