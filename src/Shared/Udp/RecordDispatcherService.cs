using Mgo2Server.Shared.Interfaces;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// The rule by which a received record names its handler: the container it belongs to, and
/// whether it is sequenced at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The image resolves a record to a handler by its container, not by the whole
/// identifier.</b> A record's identifier is a twelve-bit container plus the class bits a
/// handler is registered under, which is the split this server's registry already uses —
/// <c>0x5001</c> answers <c>0x1001</c> because the class is the answer bit above the same
/// container. Resolving on the container is what lets one handler serve a class and its
/// answer without the answer being a type of its own.
/// </para>
/// <para>
/// <b>Sequencing is a property of the record's header, not of its identifier.</b> The header's
/// bit <c>0x10</c> is what makes the fourth byte a place in a stream; with it clear the byte
/// carries no place and the record is read whatever the stream is waiting for
/// (<c>P2P_QueueInboundRecord</c>, <c>0x269b00</c>, zeroes the ordinal for exactly that case).
/// A dispatcher that treated every fourth byte as an ordinal would refuse the unsequenced
/// records the game writes beside the sequenced ones.
/// </para>
/// <para>
/// The channel's record table is twenty-four slots — <c>0x18</c> is the loop bound the image
/// walks, and <c>0x17</c> the last index of that table — which is the same bound the send
/// window keeps.
/// </para>
/// </remarks>
public static class RecordDispatcherService
{
    /// <summary>Identifier bits that name the record's container.</summary>
    public const ushort ContainerMask = 0x0fff;

    /// <summary>Slots in a channel's record table, which is also its window's bound.</summary>
    public const int SlotCount = 24;

    /// <summary>
    /// The container a record belongs to, which is what its handler is resolved on.
    /// </summary>
    /// <param name="identifier">Record identifier as it travels.</param>
    /// <returns>The identifier's low twelve bits.</returns>
    public static ushort Container(ushort identifier) => (ushort)(identifier & ContainerMask);

    /// <summary>
    /// Whether a record's fourth byte is a place in a stream.
    /// </summary>
    /// <param name="headerFlags">The record's header byte 1.</param>
    /// <returns><c>true</c> for a sequenced record, <c>false</c> for one that carries no ordinal.</returns>
    public static bool IsSequenced(byte headerFlags) =>
        RecordEntryFramingUtils.IsSequenced(headerFlags);

    /// <summary>
    /// The handler that serves a record, or <c>null</c> when the channel has none.
    /// </summary>
    /// <param name="registry">Registry of this server's handlers.</param>
    /// <param name="identifier">Record identifier as it travels.</param>
    /// <returns>The handler type, or <c>null</c>.</returns>
    public static Type? Resolve(PeerCommandRegistry registry, ushort identifier) =>
        registry.ResolveHandlerType(identifier);
}
