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
    /// <summary>
    /// Whether the fourth byte is still to be stamped by the write path, from the
    /// stream the identifier is counted on, rather than one a builder wrote.
    /// </summary>
    /// <remarks>
    /// A record built with this set is written under the value the send path takes
    /// for it, which is what makes a value's spending and its record the same act:
    /// a builder cannot take a value for a record that is then dropped, held or
    /// written twice.
    /// </remarks>
    public bool AutoNumbered { get; init; }

    /// <summary>Creates a message whose declared length matches its body.</summary>
    /// <param name="type">Message type.</param>
    /// <param name="body">Body bytes.</param>
    /// <param name="flags">Per-message flags byte.</param>
    public static UdpMessage Create(ushort type, ReadOnlySpan<byte> body, byte flags = 0) =>
        new(type, (byte)body.Length, flags, body.ToArray());

    /// <summary>
    /// Creates a message whose fourth byte the write path stamps when it is
    /// written, from the stream its identifier is counted on.
    /// </summary>
    /// <param name="type">Message type.</param>
    /// <param name="body">Body bytes.</param>
    /// <returns>The message, marked for numbering at write.</returns>
    public static UdpMessage CreateAutoNumbered(ushort type, ReadOnlySpan<byte> body) =>
        new(type, (byte)body.Length, 0, body.ToArray()) { AutoNumbered = true };
}
