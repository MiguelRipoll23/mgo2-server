namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Wire-format constants of the Metal Gear Online 2 TCP packet.
/// </summary>
public static class PacketConstants
{
    /// <summary>Size of the fixed packet header: command, length, sequence, checksum.</summary>
    public const int HeaderSize = 24;

    /// <summary>
    /// Maximum accepted payload length. 0x400 (1024) is accepted on decode
    /// because payloads are zero-padded to block alignment for Blowfish and
    /// padded inbound packets carry the padded length.
    /// </summary>
    public const int MaximumPayloadLength = 0x400;

    /// <summary>Maximum length of a complete packet.</summary>
    public const int MaximumPacketLength = MaximumPayloadLength + HeaderSize + 1;

    /// <summary>Blowfish block size in bytes.</summary>
    public const int BlowfishBlockSize = 8;

    /// <summary>Byte offset of the payload length field inside the header.</summary>
    public const int PayloadLengthOffset = 2;

    /// <summary>Byte offset of the checksum field inside the header.</summary>
    public const int ChecksumOffset = 8;

    /// <summary>Length of the HMAC-MD5 checksum in bytes.</summary>
    public const int ChecksumLength = 16;
}
