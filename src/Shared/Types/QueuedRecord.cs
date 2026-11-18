namespace Mgo2Server.Shared.Types;

/// <summary>
/// One record read out of a channel's inbound queue: what the peer wrote, with the place
/// it declared for itself.
/// </summary>
/// <remarks>
/// <b>The ordinal is the record's place in its stream, and an unsequenced record carries
/// none.</b> The fourth wire byte is that ordinal for a sequenced record and the record's
/// attribute class for an unsequenced one. This host's codec separates that byte from the
/// body, so the queue keeps it for both kinds; the dispatcher reads it as the class of a
/// tick record and compares it with its stream only when the record is sequenced.
/// </remarks>
/// <param name="Type">Identifier the record travelled under.</param>
/// <param name="Flags">Fourth wire byte, which is the record's ordinal when it is sequenced.</param>
/// <param name="Body">Body of the record.</param>
public sealed record QueuedRecord(ushort Type, byte Flags, byte[] Body);
