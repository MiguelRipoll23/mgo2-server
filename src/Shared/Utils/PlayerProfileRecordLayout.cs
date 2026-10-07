namespace Mgo2Server.Shared.Utils;

/// <summary>
/// The layout of a <c>0x1001</c> / <c>0x9001</c> roster record, and what the
/// capture establishes about each of its offsets.
/// </summary>
/// <remarks>
/// The account of the layout lives here rather than on
/// <see cref="PlayerProfileRecordUtility"/>, which writes it, or on
/// <see cref="PlayerProfileRecordParseUtils"/>, which reads it back. A roster
/// record is what both ends of the peer-to-peer channel exchange once a session
/// reaches the data phase.
/// <para>
/// <para>
/// The layout is read off the <c>0x1001</c> and <c>0x9001</c> records a real
/// dedicated server wrote during a live game (<c>docs/mgo2-game.pcapng</c>).
/// That capture holds six roster records — the room record and five player
/// entries, at roster indices <c>-1</c> and <c>0</c> to <c>4</c> — and all six
/// parse to exactly their declared length with the reader, which is what pins
/// the field offsets: a wrong offset shifts the name and the record no longer
/// adds up. An earlier reading came from a recorded replay and put the index at
/// <c>0x32 + index</c> with offsets <c>0x05</c>/<c>0x07</c> as the same index
/// under a second base; the live capture supersedes it on both counts, and
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4 sets out the differences.
/// </para>
/// <para>Body, little-endian throughout:</para>
/// <code>
/// [0x00] u8   record version, 0x07 in every captured record
/// [0x01] u8   record sub-type: PlayerEntrySubType on a player's entry,
///              RoomRecordSubType on the room's own record
/// [0x02] u16  zero
/// [0x04] u8   0xe2 + roster index, or zero for the host
/// [0x05] u8   per-player value, repeated at 0x07
/// [0x06] u8   zero
/// [0x07] u8   the same per-player value as 0x05
/// [0x08] u32  character id; its high byte is what offset 0x0a used to be
///              read as, which is why that column is not a field of its own
/// [0x0c] u8   unresolved: 0x01 0x02 on the room record, zero on a player's
/// [0x0d] u8   unresolved, and it moves with 0x0c
/// [0x10] u8   two values across the captured roster and nothing else varies
///              with it; **[U]**, named by nothing in the capture
/// [0x11] u8   zero
/// [0x12] u8   BlockConstant, 0x02 in every captured record
/// [0x13..0x1e]    two IPv4 endpoint pairs from the character's handshake, 12 bytes
/// [0x1f..0x42]    zero in every captured record
/// [0x43] u8        0x03 when a clan name follows, 0x00 when none does
/// [0x44] char[]  NUL-terminated character name
///          char[]  clan name, running to the end of the record
/// </code>
/// <para>
/// <b>Offset <c>0x08</c> is the character id.</b> An earlier reading called it
/// a per-player value of unresolved meaning and read it as a u16. It is a
/// u32, and every one of the fifteen player entries in the capture carries
/// the same value at it for the same character across every rejoin — the
/// capture holds fifteen player entries and one value per character, all of them
/// in the <c>0x0001xxxx</c> range. The local player's id is the same number in
/// this roster and in the TCP character record of the same session, which is the
/// cross-check that names the field rather than merely numbering it. The values
/// are redacted here as elsewhere; the test vectors carry stand-ins.
///
/// <para>
/// The byte at <c>0x0a</c> is <b>not</b> a field of its own. It was read as one
/// — "zero on the host, one on every joining player", which is not enough to
/// name — and it is the third byte of the id: every captured character id is in
/// the <c>0x0001xxxx</c> range, so that byte reads <c>0x01</c> on all of them,
/// and the room record's id, <c>0x0000a001</c>, has <c>0x00</c> there. What the
/// room record's own value at the id is, given it is not this host's character
/// id, is **[U]**.</para>
/// </para>
/// <para>
/// <b>The twelve bytes at <c>0x13</c>–<c>0x1e</c> are two IPv4 endpoint
/// pairs.</b> Each six-byte pair is four address octets followed by a
/// little-endian port. In the live capture both pairs match the corresponding
/// character's handshake, including public and private endpoints. They are not
/// character appearance bytes. Join-request profiles do not carry the pairs at
/// these offsets; the server must retain them from the handshake and write them
/// into the roster response.
/// </para>
/// <para>
/// Two offsets are easy to misread. The host sits at roster index
/// <see cref="HostRosterIndex"/>, signed because the index is, but it carries
/// a plain <c>0x00</c> at offset <c>0x04</c> where the joining players carry
/// <c>0xe2</c>, <c>0xe3</c>, <c>0xe4</c>… — <c>0xe2 - 1</c> is <c>0xe1</c>, so
/// the host is not on the players' scale. And offsets <c>0x05</c> and
/// <c>0x07</c> hold the same byte in all six records,
/// <c>0x00</c> <c>0x14</c> <c>0x0f</c> <c>0x04</c> <c>0x0b</c> <c>0x06</c>,
/// which follows no order: a per-player value, not a second copy of the index.
/// </para>
/// <para>
/// The run of entries is closed by a further <c>0x1001</c> record of seven bytes
/// — the record version, five zeros and the marker byte — which carries no name
/// and no roster index, and which the capture shows closing a two-entry roster.
/// </para>

/// </remarks>
public static class PlayerProfileRecordLayout
{
    /// <summary>Offset of the character id, a u32 little-endian.</summary>
    public const int CharacterIdOffset = PlayerProfileRecordUtility.CharacterIdOffset;

    /// <summary>Offset of the unresolved per-record value that reads 0x01 or 0x02.</summary>
    public const int UnresolvedByteOffset = PlayerProfileRecordUtility.UnresolvedByteOffset;
}
