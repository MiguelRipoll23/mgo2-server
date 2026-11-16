namespace Mgo2Server.Shared.Types;

/// <summary>
/// The class bits a record identifier carries in its high byte, which is the byte the receive
/// path reads as the record header's flags.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no flag byte beside the identifier: the flags are its high byte.</b>
/// <c>P2P_QueueInboundRecord</c> (<c>0x269b00</c>) reads <c>*(byte *)(record + 1)</c>, and for a
/// <c>0x1001</c> record that byte is <c>0x10</c> — <see cref="Sequenced"/> set. The same bit
/// raised into the identifier's low half is the <c>0x1000</c> the wire shows, so the class is
/// what makes <c>0x1001</c>, <c>0x5001</c> and <c>0x9001</c> three names on one container.
/// </para>
/// <para>
/// Every record the captures contain from the host declares <see cref="Sequenced"/>: 2 789 writes
/// over 24 identifiers in <c>pcapng1</c> and 6 071 over 20 in this host's own capture, none
/// without it.
/// </para>
/// </remarks>
[Flags]
public enum RecordClassBits : byte
{
    /// <summary>No class bit set: the identifier names an unsequenced record.</summary>
    None = 0,

    /// <summary>
    /// The record is sequenced: its fourth byte is a place in its stream, compared by the
    /// reader before the record is handed out (<see cref="RecordEntryState.QueuedSequenced"/>).
    /// </summary>
    Sequenced = 0x10,

    /// <summary>
    /// The record declares a sixteen-bit length, which also moves its fourth byte one position
    /// later in the header.
    /// </summary>
    ExtendedLength = 0x20,

    /// <summary>The record is an answer to one of the peer's, not a record of its own.</summary>
    Answer = 0x40,

    /// <summary>
    /// The record is a timed one: its answer carries the peer's turnaround as one body byte,
    /// and the address side measures its own clock against it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is measured, not guessed: the branch is in <c>decode_p2p_datagram</c>
    /// (<c>0x2666c8</c>).</b> A sequenced record carrying the bit stamps the session's timed
    /// exchange — <c>P2P_ReadTimeBaseInto(session + 0x18)</c> for a record this host sends,
    /// <c>session + 0x1a</c> for one it receives — together with the record's fourth byte at
    /// <c>session + 0x55</c> and its handle at <c>session + 0x20</c>, but only while
    /// <c>P2P_SessionTickNumber()</c> is under <c>200</c>.
    /// </para>
    /// <para>
    /// The answer (<see cref="Answer"/> and this bit, one body byte that is not <c>0xff</c>,
    /// the same fourth byte) closes it: <c>P2P_ElapsedMillisecondsSince</c> gives the elapsed
    /// milliseconds, the answer's byte is subtracted from them, and the remainder is kept at
    /// <c>session + 0xc2</c>. A remainder over <c>600</c> either sets the session's resend flag
    /// (<c>session[5] |= 0x20</c>, when the remainder is under <c>0x3e9</c>) or shrinks the
    /// interval at <c>session[0x24]</c>, which is then clamped to at least <c>16 000</c>. The
    /// pending handle is cleared either way.
    /// </para>
    /// <para>
    /// A record carrying the bit <em>without</em> <see cref="Sequenced"/> takes the other
    /// branch: the session's word at <c>+0x26</c> gains bit <c>4</c> and the counter at
    /// <c>+0xb2</c> advances by one, and the record goes on to the ordinary queue path.
    /// </para>
    /// <para>
    /// What the report carries is not established; what it asks for is. Every
    /// <c>0x8000</c> record in the working captures is answered with exactly one body byte
    /// (<c>0x9001</c> → <c>0xd001</c>, <c>0x92de</c> → <c>0xd2de</c>), which is the byte the
    /// measurement above subtracts.
    /// </para>
    /// </remarks>
    Timed = 0x80
}
