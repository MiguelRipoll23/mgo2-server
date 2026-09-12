using Microsoft.Extensions.Logging;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Shared.Tcp;

/// <summary>
/// Encodes and decodes the Metal Gear Online 2 TCP packet. The wire format is
/// XOR-obfuscated in full and carries an HMAC-MD5 checksum over the first eight
/// header bytes and the payload.
/// </summary>
public sealed class PacketCodecService(ILogger<PacketCodecService> logger)
{
    /// <summary>
    /// Decodes a raw packet buffer.
    /// </summary>
    /// <param name="buffer">Wire bytes of exactly one packet.</param>
    /// <returns>The decoded packet, or <c>null</c> when the buffer is malformed or the checksum is invalid.</returns>
    public Packet? DecodePacket(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < PacketConstants.HeaderSize)
        {
            return null;
        }

        var decrypted = buffer.ToArray();
        CryptoUtility.ApplyExclusiveOr(decrypted);

        var command = BinaryUtility.ReadUInt16BigEndian(decrypted, 0);
        var payloadLength = BinaryUtility.ReadUInt16BigEndian(decrypted, PacketConstants.PayloadLengthOffset);
        var sequence = BinaryUtility.ReadUInt32BigEndian(decrypted, 4);
        var checksum = decrypted[PacketConstants.ChecksumOffset..(PacketConstants.ChecksumOffset + PacketConstants.ChecksumLength)];

        if (payloadLength > PacketConstants.MaximumPayloadLength)
        {
            return null;
        }

        if (decrypted.Length < PacketConstants.HeaderSize + payloadLength)
        {
            return null;
        }

        var payload = decrypted[PacketConstants.HeaderSize..(PacketConstants.HeaderSize + payloadLength)];

        var expectedChecksum = CryptoUtility.ComputeHmacMd5(decrypted.AsSpan(0, 8), payload);
        if (!checksum.AsSpan().SequenceEqual(expectedChecksum))
        {
            return null;
        }

        if (BlowfishEncryptedCommandConstants.Inbound.Contains(command))
        {
            var padded = CryptoUtility.PadToBlowfishBlock(payload);
            CryptoUtility.DecryptPacketPayload(padded);
            payload = padded[..payloadLength];
        }

        return new Packet(
            new PacketHeader(command, payloadLength, sequence, checksum),
            payload);
    }

    /// <summary>
    /// Encodes a packet into wire bytes, encrypting the payload when the
    /// command requires it.
    /// </summary>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="payload">Payload bytes.</param>
    /// <param name="sequenceOut">Outbound sequence number.</param>
    /// <param name="logPrefix">Prefix used for the outbound packet log line.</param>
    public byte[] EncodePacket(
        ushort commandId,
        ReadOnlySpan<byte> payload,
        uint sequenceOut,
        string logPrefix = "packet-codec")
    {
        byte[] encodedPayload;
        if (BlowfishEncryptedCommandConstants.IsEncrypted(commandId))
        {
            encodedPayload = CryptoUtility.PadToBlowfishBlock(payload);
            CryptoUtility.EncryptPacketPayload(encodedPayload);
        }
        else
        {
            encodedPayload = payload.ToArray();
        }

        var packetBuffer = new byte[PacketConstants.HeaderSize + encodedPayload.Length];
        BinaryUtility.WriteUInt16BigEndian(packetBuffer, 0, commandId);
        BinaryUtility.WriteUInt16BigEndian(packetBuffer, PacketConstants.PayloadLengthOffset, (ushort)encodedPayload.Length);
        BinaryUtility.WriteUInt32BigEndian(packetBuffer, 4, sequenceOut);

        var checksum = CryptoUtility.ComputeHmacMd5(packetBuffer.AsSpan(0, 8), encodedPayload);
        checksum.CopyTo(packetBuffer, PacketConstants.ChecksumOffset);
        encodedPayload.CopyTo(packetBuffer.AsSpan(PacketConstants.HeaderSize));

        logger.LogDebug(
            "[{LogPrefix}] OUT 0x{Command} ({Length} bytes): {Bytes}",
            logPrefix,
            commandId.ToString("x4"),
            packetBuffer.Length,
            Convert.ToHexStringLower(packetBuffer));

        CryptoUtility.ApplyExclusiveOr(packetBuffer);
        return packetBuffer;
    }

    /// <summary>Encodes a packet carrying a masked error code.</summary>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="errorCode">Error code; zero is sent unmasked.</param>
    /// <param name="sequenceOut">Outbound sequence number.</param>
    /// <param name="logPrefix">Prefix used for the outbound packet log line.</param>
    public byte[] EncodeErrorPacket(
        ushort commandId,
        uint errorCode,
        uint sequenceOut,
        string logPrefix = "packet-codec")
    {
        var payload = new byte[4];
        var maskedErrorCode = errorCode != 0 ? errorCode | ErrorCodeConstants.ErrorMask : 0;
        BinaryUtility.WriteUInt32BigEndian(payload, 0, maskedErrorCode);
        return EncodePacket(commandId, payload, sequenceOut, logPrefix);
    }

    /// <summary>Encodes a packet with an empty payload.</summary>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="sequenceOut">Outbound sequence number.</param>
    /// <param name="logPrefix">Prefix used for the outbound packet log line.</param>
    public byte[] EncodeAcknowledgementPacket(
        ushort commandId,
        uint sequenceOut,
        string logPrefix = "packet-codec") =>
        EncodePacket(commandId, ReadOnlySpan<byte>.Empty, sequenceOut, logPrefix);
}
