namespace Mgo2Server.Shared.Types;

/// <summary>
/// The state code a queued record's entry carries at offset nine, which is the byte the reader
/// tests before it compares a fourth byte at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reader's condition is <c>(state &amp; 5) == 5</c>, so the code is two bits: one set at
/// bit 0, one at bit 2.</b> <c>read_queued_p2p_message</c> applies that test and, only when it
/// passes, compares the entry's ordinal at offset 10 with the byte its stream holds, advancing
/// the stream on a match and dropping the record without advancing on a mismatch. An entry
/// whose code does not satisfy the test is handed to its handler with no comparison at all.
/// </para>
/// <para>
/// <b>The image writes one of two values there, and a handle flag decides which.</b>
/// <c>P2P_QueueInboundRecord</c> (<c>0x269b00</c>) stores <c>1</c> when the record handle's byte
/// at <c>+0xa</c> has bit 1 clear and <c>5</c> when it is set, so the gate applies to a record
/// only when its handle asks for it. This host has no such handle flag to read, and every record
/// the captures contain declares the sequenced class on its identifier (2 789 host writes over
/// 24 types in <c>pcapng1</c>, all with bit <c>0x10</c>), so it stores
/// <see cref="QueuedSequenced"/> for a record whose identifier says sequenced and
/// <see cref="None"/> for one that does not.
/// </para>
/// <para>
/// The reader sets bit 7 on an entry it has taken, which is why a taken entry is not read twice.
/// </para>
/// </remarks>
[Flags]
public enum RecordEntryState : byte
{
    /// <summary>The entry carries no place in a stream; the reader compares nothing.</summary>
    None = 0,

    /// <summary>Bit 0 of the reader's condition: the entry is queued for reading.</summary>
    Queued = 1 << 0,

    /// <summary>Bit 2 of the reader's condition: the entry carries a place in its stream.</summary>
    Sequenced = 1 << 2,

    /// <summary>
    /// Both bits of the reader's condition, <c>5</c>: the entry's fourth byte is compared with
    /// the byte its stream is waiting for.
    /// </summary>
    QueuedSequenced = Queued | Sequenced,

    /// <summary>Set by the reader on an entry it has handed out.</summary>
    Taken = 1 << 7
}
