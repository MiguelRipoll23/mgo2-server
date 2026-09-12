namespace Mgo2Server.Shared.Types;

/// <summary>
/// A decoded message carried in the content region of a peer-to-peer frame.
/// </summary>
/// <param name="Type">Message type, little-endian on the wire. Class bits 0x4000 and 0x8000 live in this word.</param>
/// <param name="Length">Body length declared on the wire.</param>
/// <param name="Flags">Per-message flags byte following the length.</param>
/// <param name="Body">Body bytes.</param>
public sealed record UdpMessage(ushort Type, byte Length, byte Flags, byte[] Body)
{
    /// <summary>Creates a message whose declared length matches its body.</summary>
    /// <param name="type">Message type.</param>
    /// <param name="body">Body bytes.</param>
    /// <param name="flags">Per-message flags byte.</param>
    public static UdpMessage Create(ushort type, ReadOnlySpan<byte> body, byte flags = 0) =>
        new(type, (byte)body.Length, flags, body.ToArray());
}
