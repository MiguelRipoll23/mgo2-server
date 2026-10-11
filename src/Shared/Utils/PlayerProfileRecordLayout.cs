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
/// That capture holds six roster records — the host's own entry and five player
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
/// [0x01] u8   the record's own length less seven -- not a sub-type
/// [0x02] u16  zero
/// [0x04] u8   0xe2 + roster index, or zero for the host
/// [0x05] u8   per-player value, repeated at 0x07
/// [0x06] u8   zero
/// [0x07] u8   the same per-player value as 0x05
/// [0x08] u32  character id; its high byte is what offset 0x0a used to be
///              read as, which is why that column is not a field of its own
/// [0x0c] u8   unresolved: 0x01 0x02 on the host's own entry, zero on a player's
/// [0x0d] u8   unresolved, and it moves with 0x0c
/// [0x10] u8   two values across the captured roster and nothing else varies
///              with it; **[U]**, named by nothing in the capture
/// [0x11] u8   zero
/// [0x12] u8   BlockConstant, 0x02 in every captured record
/// [0x13..0x18]    first endpoint pair from the character's handshake: four
///                   address octets, then its port little-endian -- so 0x17 is
///                   that port's low byte and 0x18 its high byte
/// [0x19..0x1e]    the second pair, the same shape: octets at 0x19..0x1c, and
///                   so its port's low byte is 0x1d and its high byte 0x1e
/// [0x1f..0x42]    zero in every captured record
/// [0x43] u8        0x03 when a clan name follows, 0x00 when none does
/// [0x44] char[]  NUL-terminated character name
///          char[]  clan name, running to the end of the record
///                   (the short layout puts the marker at 0x37 and the name at
///                    0x38 -- see below)
/// </code>
/// <para>
/// <b>Offset <c>0x01</c> is the record's own length, not a sub-type.</b> It reads
/// <c>0x4c</c> on the host's own 83-byte entry, <c>0x48</c> on a 79-byte player entry
/// and <c>0x47</c> on the 78-byte one, and in every case it is the body's length
/// less seven. That is why it moves with the name: <c>fargraven2</c> is ten
/// characters and <c>Libertine</c> nine, and the entry is a byte shorter for it.
/// All four captures agree, over fifty records and fourteen distinct (byte,
/// length) pairs, with no counterexample. An earlier reading of the
/// <c>0x47</c>/<c>0x48</c> pair as “the same sub-type, one bit apart, what the bit
/// selects being **[U]**” is superseded. **[V]**
/// </para>
/// <para>
/// <b>A record has two layouts, and which one is present is decided per record.</b>
/// The short layout is this one without the twelve-byte endpoint field: its clan
/// id sits at <c>0x13</c> instead of <c>0x1f</c>, and the name at <c>0x38</c>
/// instead of <c>0x44</c> — twelve bytes earlier throughout, which is exactly the
/// length difference. The header's format version is the same in both.
/// <c>rpdt.js</c> tells them apart by walking back from the end of the record to
/// the nick's NUL, and <see cref="PlayerProfileRecordParseUtils"/> ports that
/// rule. Of the three captures, game1's roster is the long layout and game2's and
/// game3's the short. <see cref="PlayerProfileRecordUtility.Build"/> writes the
/// long layout only. **[V]** on the offsets and the rule; what a short record
/// means at <c>0x04</c> is **[U]** — game2's host writes <c>0x01</c>/<c>0x02</c>
/// there, where game1's writes <c>0xe2</c> plus the index, so the base is a
/// property of the host and not of the layout.
/// </para>
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
/// and the host's own entry's id, <c>0x0000a001</c>, has <c>0x00</c> there —
/// because that id is the host's own, the <c>0x0000a001</c> the same host
/// announces as its handshake <c>peer_id</c> (<c>UDP_GAME_CAPTURE.md</c> §8.1).
/// </para>
/// <para>
/// <b>The head of a run is the host's own entry, not a description of the
/// room.</b> It carries the host's character id and, at <c>0x44</c>, the host's
/// character name — the same field in the same place a joining player's entry
/// carries its own. It is what the server's <c>HostIdentityService</c> supplies,
/// and it is told apart from a player's entry only by the bytes below, the pair
/// at <c>0x0c</c>/<c>0x0d</c>. Whether
/// the <c>0x00</c> it writes at <c>0x04</c> means a dedicated host or the host's
/// own player slot is **[U]**;
/// <see cref="PlayerProfileRecordUtility.HostRosterIndex"/> carries the reading
/// this server works from.
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
/// <b>What looked like a marker and a trailer in this field is the pairs'
/// ports.</b> The bytes at <c>0x17</c>/<c>0x18</c> and <c>0x1d</c>/<c>0x1e</c>
/// were read as a per-peer marker — <c>0x63</c> on the host's own entry,
/// <c>0x62</c> on a player's — closing into a constant trailer <c>0x16</c>.
/// They are two ports. The round's host answers on 5731, which is <c>0x1663</c>,
/// and its joiner on 5730, which is <c>0x1662</c>, and each peer repeats its own
/// port in both of its pairs: so the host's entry reads <c>0x63 0x16</c> twice
/// and the joiner's <c>0x62 0x16</c> twice, and that pair of values is the whole
/// of the "marker" and the "trailer". Our own live host entry carries
/// <c>05 0d</c> there for its port 3333, and its joiner entry <c>62 16</c> for
/// 5730 — the same four bytes the reference joiner's entry carries, because it
/// is the same peer port. **[V]**
/// </para>
/// <para>
/// The short layout settles it rather than the long one: it has no endpoint
/// field at all, its name sits at <c>0x38</c> and every short entry is shorter
/// than <c>0x44</c>, and the bytes here are then neither a port nor a constant —
/// two of game3's thirty-two short entries hold <c>0x02</c> at <c>0x17</c>. A
/// layout constant would have to read the same in a record that carries no
/// endpoint. **[V]**
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
/// The client binary names it: it is the player's slot in the host's 24-entry
/// network player table — the byte the replay console's <c>rpdt.js</c> calls the
/// record's <c>slot</c> (the lookup <c>0x26d5a8</c> walks 24 records of
/// <c>0x74</c> bytes at <c>net + 0xd4</c>, slot in the first byte), so it is a
/// per-session player number and its observed range is <c>0</c>…<c>0x17</c>.
/// </para>
/// <para>
/// Three of the offsets above and one variant are named independently by the
/// replay console's own parser, <c>rpdt.js</c> (vendored at
/// <c>docs/analysis/replay-console/</c>): <c>joinCounter</c> at <c>0x04</c>,
/// <c>slot</c> at <c>0x05</c>, <c>clanId</c> as a u16 at <c>0x1f</c>, and a
/// roster record twelve bytes shorter with its name at <c>+56</c>. §8.11 of
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> sets the cross-check out; every
/// offset that parser reads is one of the fields here.
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
