using System.Text;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Builds and parses the <c>0x1001</c> player-profile record that both ends of
/// the peer-to-peer channel exchange once a session reaches the data phase.
/// </summary>
/// <remarks>
/// <para>
/// The layout is read off the <c>0x1001</c> and <c>0x9001</c> records a real
/// dedicated server wrote during a live game (<c>docs/mgo2-game.pcapng</c>).
/// That capture holds six roster records — the host and five joining players,
/// at roster indices <c>-1</c> and <c>0</c> to <c>4</c> — and all six parse to
/// exactly their declared length with this reader, which is what pins the field
/// offsets: a wrong offset shifts the name and the record no longer adds up.
/// An earlier reading came from a recorded replay and put the index at
/// <c>0x32 + index</c> with offsets <c>0x05</c>/<c>0x07</c> as the same index
/// under a second base; the live capture supersedes it on both counts, and
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4 sets out the differences.
/// </para>
/// <para>Body, little-endian throughout:</para>
/// <code>
/// [0x00] u8   record version, 0x07 in every captured record
/// [0x01] u8   character identifier
/// [0x02] u16  zero
/// [0x04] u8   0xe2 + roster index, or zero for the host
/// [0x05] u8   per-player value, repeated at 0x07
/// [0x06] u8   zero
/// [0x07] u8   the same per-player value as 0x05
/// [0x08] u16  per-player value that varies across the roster
/// [0x0a] u8   unresolved: 0 on the host, 1 on every joining player
/// [0x0b..0x42]     per-player block; 0x02 at 0x12 and 0x16 at 0x18 and 0x1e,
///                  zero elsewhere but for the columns that differ per player
/// [0x43] u8        0x03 when a clan name follows, 0x00 when none does
/// [0x44] char[]  NUL-terminated character name
///          char[]  clan name, running to the end of the record
/// </code>
/// <para>
/// The name is NUL-terminated; the clan name is not, because the record ends
/// there. Both are bounded, so a longer name is truncated rather than allowed
/// to overrun the frame. A joining client opens the exchange with a record of
/// the same shape but a different leading byte — <c>0x02</c> where a roster
/// record carries <see cref="RecordVersion"/> — so <see cref="Parse"/> reports
/// that byte rather than rejecting the body.
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
public static class PlayerProfileRecordUtility
{
    /// <summary>Record version byte every recorded roster entry carries.</summary>
    public const byte RecordVersion = 0x07;

    /// <summary>
    /// Leading byte a joining client puts on the record it opens the exchange
    /// with. It is not a roster entry, so it never appears in what the host
    /// sends back.
    /// </summary>
    public const byte JoinRequestVersion = 0x02;

    /// <summary>
    /// Roster slot the host itself takes. The host is not one of the joining
    /// players, so its slot sits below the first one rather than at it.
    /// </summary>
    public const sbyte HostRosterIndex = -1;

    /// <summary>
    /// Bytes of the roster-close record, in the order the game writes them: the
    /// record version, five zeros, and the marker byte. Seven bytes in total.
    /// </summary>
    private static ReadOnlySpan<byte> ClosingRecordBytes => [0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03];

    /// <summary>
    /// Base added to the roster index to form the field at offset 4. Measured
    /// from the live capture: the joining players' records carry 0xe2, 0xe3,
    /// 0xe4, 0xe5 and 0xe6 at roster indices 0 to 4.
    /// </summary>
    private const byte RosterIndexBase = 0xe2;

    /// <summary>
    /// What the host writes at offset 4, which is not
    /// <c><see cref="RosterIndexBase"/> - 1</c>: the captured host record
    /// carries a plain zero and the players are on their own scale.
    /// </summary>
    private const byte HostRosterField = 0x00;

    /// <summary>
    /// Value written at offsets 5 and 7 — a per-player value of unresolved
    /// meaning, not the roster index as an earlier replay reading had it — so
    /// it is sent as zero.
    /// </summary>
    private const byte PlayerValue = 0x00;

    /// <summary>
    /// Byte preceding the character name: 0x03 when a clan name follows, 0x00
    /// when none does, so it marks the presence of one rather than being a
    /// constant.
    /// </summary>
    private const byte ClanNameMarker = 0x03;

    /// <summary>
    /// The byte at <see cref="BlockConstantOffset"/>. It and the two
    /// <see cref="BlockMarker"/> columns are the only constants in the block
    /// that are not zero across the six captured records; the thirteen columns
    /// carrying anything else vary per player, and the rest are zero throughout.
    /// </summary>
    private const byte BlockConstant = 0x02;

    /// <summary>Offset of <see cref="BlockConstant"/> within the record body.</summary>
    private const int BlockConstantOffset = 0x12;

    /// <summary>
    /// The other two columns of the block that are the same byte in all six
    /// captured records without being zero.
    /// </summary>
    private const byte BlockMarker = 0x16;

    /// <summary>First offset of <see cref="BlockMarker"/>.</summary>
    private const int FirstBlockMarkerOffset = 0x18;

    /// <summary>Second offset of <see cref="BlockMarker"/>.</summary>
    private const int SecondBlockMarkerOffset = 0x1e;

    /// <summary>Offset of the constant name marker.</summary>
    public const int NameMarkerOffset = 0x43;

    /// <summary>Offset of the NUL-terminated character name.</summary>
    public const int NameOffset = 0x44;

    /// <summary>
    /// Smallest body a profile record can have. A body must reach past the
    /// fixed fields to carry a name, so this is the fixed-field size plus the
    /// marker and at least one character.
    /// </summary>
    private const int MinimumBodySize = NameOffset + 2;

    /// <summary>Longest character name a record will carry.</summary>
    /// <remarks>
    /// Sixteen characters, which is the width the TCP character list uses for
    /// the same field (`0x3049`'s `selected_name`, 16 bytes, ISO-8859-1).
    /// </remarks>
    public const int MaximumNameLength = 16;

    /// <summary>Longest clan name a record will carry.</summary>
    public const int MaximumClanLength = 24;

    /// <summary>
    /// Builds a profile record body.
    /// </summary>
    /// <param name="characterIdentifier">Character identifier of the player.</param>
    /// <param name="rosterIndex">
    /// Position of the player in the room roster, counted from
    /// <see cref="HostRosterIndex"/>. It is signed because the host's own slot
    /// is the one below zero.
    /// </param>
    /// <param name="perPlayerValue">
    /// The varying value written at offset 8. It differs per player in every
    /// recorded match and its meaning is unresolved, so it is passed through.
    /// </param>
    /// <param name="flagValue">
    /// Value written at offset 10, unresolved: the six captured records carry
    /// <c>0</c> on the host and <c>1</c> on every joining player, which is not
    /// enough to name it. An earlier replay reading called it a team flag
    /// without evidence for that.
    /// </param>
    /// <param name="name">Character name.</param>
    /// <param name="clanName">Clan name, which may be empty.</param>
    public static byte[] Build(
        byte characterIdentifier,
        sbyte rosterIndex,
        ushort perPlayerValue,
        byte flagValue,
        string name,
        string clanName)
    {
        var nameBytes = Truncate(Encoding.ASCII.GetBytes(name ?? string.Empty), MaximumNameLength);
        var clanBytes = Truncate(Encoding.ASCII.GetBytes(clanName ?? string.Empty), MaximumClanLength);

        // The name's NUL terminator is written explicitly; the clan name runs to
        // the end of the record and carries none, which is what the recorded
        // records do.
        var body = new byte[NameOffset + 1 + nameBytes.Length + clanBytes.Length];
        body[0] = RecordVersion;
        body[1] = characterIdentifier;
        body[4] = rosterIndex == HostRosterIndex
            ? HostRosterField
            : (byte)(RosterIndexBase + rosterIndex);
        body[5] = PlayerValue;
        body[7] = PlayerValue;
        BinaryUtility.WriteUInt16LittleEndian(body, 8, perPlayerValue);
        body[10] = flagValue;
        body[BlockConstantOffset] = BlockConstant;
        body[FirstBlockMarkerOffset] = BlockMarker;
        body[SecondBlockMarkerOffset] = BlockMarker;
        body[NameMarkerOffset] = clanBytes.Length > 0 ? ClanNameMarker : (byte)0x00;
        nameBytes.CopyTo(body, NameOffset);
        body[NameOffset + nameBytes.Length] = 0x00;
        clanBytes.CopyTo(body, NameOffset + nameBytes.Length + 1);
        return body;
    }

    /// <summary>
    /// Builds the record that closes a roster run: a seven-byte <c>0x1001</c> body
    /// of the record version, five zeros and the marker byte.
    /// </summary>
    /// <remarks>
    /// The recorded host writes it last, immediately after the last roster entry,
    /// and it is a <c>0x1001</c> record like the entries rather than a type of its
    /// own. Its body is too short to carry a name, so <see cref="Parse"/> returns
    /// <c>null</c> for it and it cannot be mistaken for a player.
    /// </remarks>
    public static byte[] BuildRosterClose() => [.. ClosingRecordBytes];

    /// <summary>
    /// Parses a profile record body.
    /// </summary>
    /// <param name="body">Message body to parse.</param>
    /// <returns>
    /// The parsed record, or <c>null</c> when the body is too short to hold the
    /// fixed fields.
    /// </returns>
    public static PlayerProfileRecord? Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < MinimumBodySize)
        {
            return null;
        }

        var name = string.Empty;
        var clan = string.Empty;
        var roster = RosterLayoutOf(body);
        if (roster is not null)
        {
            (name, clan) = roster.Value;
        }
        else
        {
            (name, clan) = TrailingLayoutOf(body);
        }

        return new PlayerProfileRecord(
            body[0],
            body[1],
            body[4],
            RosterIndexOf(body[4]),
            body[5],
            body[NameMarkerOffset] != 0x00,
            BinaryUtility.ReadUInt16LittleEndian(body, 8),
            body[10],
            name,
            clan);
    }

    /// <summary>
    /// Recovers the roster index from the field at offset 4. The host is
    /// written as a distinct zero rather than as one below the first player, so
    /// zero means the host and everything else is the index under
    /// <see cref="RosterIndexBase"/>.
    /// </summary>
    /// <param name="field">Raw field at offset 4.</param>
    /// <returns>The roster index the record reports.</returns>
    private static sbyte RosterIndexOf(byte field) =>
        field == HostRosterField ? HostRosterIndex : (sbyte)(field - RosterIndexBase);

    /// <summary>
    /// Reads the names off the documented roster layout, or returns <c>null</c>
    /// when the body is not that shape.
    /// </summary>
    private static (string Name, string Clan)? RosterLayoutOf(ReadOnlySpan<byte> body)
    {
        if (body[NameMarkerOffset] is not (0x00 or 0x03))
        {
            return null;
        }

        var nameEnd = body[NameOffset..].IndexOf((byte)0x00);
        if (nameEnd < 0)
        {
            return null;
        }

        // Any printable run is a name here: the recorded characters carry
        // spaces and punctuation, so nothing narrower may be required.
        return (
            DecodeName(body.Slice(NameOffset, nameEnd)),
            DecodeName(body[(NameOffset + nameEnd + 1)..]));
    }

    /// <summary>
    /// Reads the names off a join request, whose per-player block is longer than
    /// the roster record's and so carries them elsewhere.
    /// </summary>
    /// <remarks>
    /// The record ends the way the roster record does: the character name, a NUL,
    /// and the clan name running to the end. The name is therefore whatever
    /// printable run precedes that NUL, trimmed back to the first byte a
    /// character name can hold, because the block before it is full of
    /// printable bytes of its own that would otherwise be read as part of it.
    /// </remarks>
    private static (string Name, string Clan) TrailingLayoutOf(ReadOnlySpan<byte> body)
    {
        var clanStart = body.LastIndexOf((byte)0x00) + 1;
        var nameEnd = clanStart - 1;

        var nameStart = nameEnd;
        while (nameStart > 0 && IsPrintable(body[nameStart - 1]))
        {
            nameStart--;
        }

        while (nameStart < nameEnd && !IsNameByte(body[nameStart]))
        {
            nameStart++;
        }

        return (
            nameEnd > nameStart ? DecodeName(body[nameStart..nameEnd]) : string.Empty,
            clanStart < body.Length ? DecodeName(body[clanStart..]) : string.Empty);
    }

    private static bool IsPrintable(byte value) => value is >= 0x20 and < 0x7f;

    /// <summary>
    /// Whether a byte can begin a character name. Letters, digits and the
    /// underscore: this is what tells the name apart from the structural bytes
    /// of the block in front of it, which are printable but are not text. Only
    /// the leading edge is trimmed with it, so a name may still contain
    /// anything once it has started.
    /// </summary>
    private static bool IsNameByte(byte value) =>
        value is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or
               (>= (byte)'0' and <= (byte)'9') or (byte)'_';

    /// <summary>
    /// Decodes a character name the way the game writes one: raw bytes in
    /// ISO-8859-1, the encoding the TCP character list uses for the same field.
    /// Latin-1 maps every byte to the codepoint of the same value, so a name
    /// survives exactly as it was on the wire and a decode can never throw.
    /// </summary>
    private static string DecodeName(ReadOnlySpan<byte> bytes) =>
        bytes.IsEmpty ? string.Empty : Encoding.Latin1.GetString(bytes);

    private static byte[] Truncate(byte[] value, int maximum)
    {
        if (value.Length <= maximum)
        {
            return value;
        }

        var truncated = new byte[maximum];
        value.AsSpan(0, maximum).CopyTo(truncated);
        return truncated;
    }
}

/// <summary>A parsed <c>0x1001</c> player-profile record.</summary>
/// <param name="Version">Record version byte.</param>
/// <param name="CharacterIdentifier">Character identifier of the player.</param>
/// <param name="RosterBaseField">Raw field at offset 4: 0xe2 plus the index, or zero for the host.</param>
/// <param name="RosterIndex">
/// Position of the player in the room roster, recovered from that field. It is
/// signed because the host's own slot is <c>-1</c>, written as a plain zero
/// rather than as one below the first player's.
/// </param>
/// <param name="PlayerValue">
/// The value at offset 5, repeated at offset 7. It varies per player without
/// following roster order, so it is a per-player value of unresolved meaning
/// rather than an index.
/// </param>
/// <param name="HasClanName">Whether a clan name follows the character name.</param>
/// <param name="PerPlayerValue">The varying value at offset 8; its meaning is unresolved.</param>
/// <param name="FlagValue">The value at offset 10, whose meaning is unresolved.</param>
/// <param name="Name">Character name.</param>
/// <param name="ClanName">Clan name.</param>
public sealed record PlayerProfileRecord(
    byte Version,
    byte CharacterIdentifier,
    byte RosterBaseField,
    sbyte RosterIndex,
    byte PlayerValue,
    bool HasClanName,
    ushort PerPlayerValue,
    byte FlagValue,
    string Name,
    string ClanName);
