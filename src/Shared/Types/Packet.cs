namespace Mgo2Server.Shared.Types;

/// <summary>Decoded header of a TCP packet.</summary>
/// <param name="Command">Command identifier of the packet.</param>
/// <param name="PayloadLength">Declared payload length.</param>
/// <param name="Sequence">Sequence number of the packet.</param>
/// <param name="Checksum">Sixteen-byte HMAC-MD5 checksum carried by the header.</param>
public readonly record struct PacketHeader(
    ushort Command,
    ushort PayloadLength,
    uint Sequence,
    byte[] Checksum);

/// <summary>A decoded TCP packet: its header and its payload.</summary>
/// <param name="Header">Decoded packet header.</param>
/// <param name="Payload">Packet payload, decrypted when the command requires it.</param>
public sealed record Packet(PacketHeader Header, byte[] Payload);
