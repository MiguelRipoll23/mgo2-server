namespace Mgo2Server.Shared.Types;

/// <summary>
/// One record read out of a channel's inbound queue: what the peer wrote, with the place
/// it declared for itself.
/// </summary>
/// <remarks>
/// <b>The ordinal is the record's place in its stream, and an unsequenced record carries
/// none.</b> It is the fourth wire byte for a sequenced record and zero for an unsequenced
/// one, which is what the image's queue stores when the header's sequenced bit is clear
/// (<c>P2P_QueueInboundRecord</c>, <c>0x269b00</c>). The dispatcher reads it to decide
/// whether the record is the one its stream is waiting for; the body and the identifier are
/// all a handler sees.
/// </remarks>
/// <param name="Type">Identifier the record travelled under.</param>
/// <param name="Flags">Fourth wire byte, which is the record's ordinal when it is sequenced.</param>
/// <param name="Body">Body of the record.</param>
public sealed record QueuedRecord(ushort Type, byte Flags, byte[] Body);
