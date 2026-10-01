using System.Text;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Builds and parses the <c>0x1001</c> player-profile record that both ends of
/// the peer-to-peer channel exchange once a session reaches the data phase.
/// </summary>
/// <remarks>
/// <para>
/// The layout was read off the <c>0x1001</c> records the host wrote into a
/// recorded survival match (<c>tools/replays/replay_360827_5.dat</c>). All ten
/// full-length records in that file parse to exactly their declared length with
/// this reader, which is what pins the field offsets: a wrong offset shifts the
/// name and the record no longer adds up.
/// </para>
/// <para>
/// Body, little-endian throughout:
/// </para>
/// <code>
/// [0x00] u8   record version, 0x07 in every recorded record
/// [0x01] u8   character identifier
/// [0x02] u16  zero
/// [0x04] u8   0x32 + roster index
/// [0x05] u8   roster index
/// [0x06] u8   zero
/// [0x07] u8   roster index, repeated
/// [0x08] u16  per-player value that varies across the roster/// [0x0a] u8        team flag
/// [0x0b..0x42]     per-player block; 0x02 at 0x12, zero elsewhere but for
///                  eight columns that differ per player
/// [0x43] u8        0x03 when a clan name follows, 0x00 when none does
/// [0x44] char[]  NUL-terminated character name
///          char[]  clan name, running to the end of the record
/// </code>
/// <para>
/// The name is NUL-terminated; the clan name is not, because the record ends
/// there. Both are bounded, so a name longer than the buffer is truncated
/// rather than allowed to overrun the frame.
/// </para>
/// <para>
/// The host's own record is the one at roster index <see cref="HostRosterIndex"/>,
/// which is why the index is signed: the recorded host record carries
/// <c>0x31</c> and <c>0x05</c> where a joining player's first record carries
/// <c>0x32</c> and <c>0x06</c>. Every other player follows at 0, 1, 2 and so on.
/// </para>
/// <para>
/// A joining client opens the exchange with a record of the same shape but a
/// different leading byte — <c>0x02</c> where a roster record carries
/// <see cref="RecordVersion"/> — so <see cref="Parse"/> reports the byte rather
/// than rejecting the body.
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

    /// <summary>Base added to the roster index to form the field at offset 4.</summary>
    private const byte RosterIndexBase = 0x32;

    /// <summary>
    /// Base added to the roster index to form the fields at offsets 5 and 7.
    /// It is not the same base as <see cref="RosterIndexBase"/>, so the two
    /// fields are written from the index separately rather than derived from
    /// one another.
    /// </summary>
    private const byte PlayerNumberBase = 6;

    /// <summary>
    /// Byte preceding the character name. It reads 0x03 in every recorded record
    /// that carries a clan name and 0x00 in the one that does not, so it marks
    /// whether a clan name follows rather than being a constant.
    /// </summary>
    private const byte ClanNameMarker = 0x03;

    /// <summary>
    /// The one byte of the per-player block that is not zero in every recorded
    /// record. Across the twelve records of the recorded match this is the only
    /// constant in the block that is not zero; the other seven columns that
    /// carry anything vary per player, and the remaining forty-eight are zero
    /// throughout.
    /// </summary>
    private const byte BlockConstant = 0x02;

    /// <summary>Offset of <see cref="BlockConstant"/> within the record body.</summary>
    private const int BlockConstantOffset = 0x12;

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
    /// <param name="teamFlag">Team flag written at offset 10.</param>
    /// <param name="name">Character name.</param>
    /// <param name="clanName">Clan name, which may be empty.</param>
    public static byte[] Build(
        byte characterIdentifier,
        sbyte rosterIndex,
        ushort perPlayerValue,
        byte teamFlag,
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
        body[4] = (byte)(RosterIndexBase + rosterIndex);
        body[5] = (byte)(PlayerNumberBase + rosterIndex);
        body[7] = (byte)(PlayerNumberBase + rosterIndex);
        BinaryUtility.WriteUInt16LittleEndian(body, 8, perPlayerValue);
        body[10] = teamFlag;
        body[BlockConstantOffset] = BlockConstant;
        body[NameMarkerOffset] = clanBytes.Length > 0 ? ClanNameMarker : (byte)0x00;
        nameBytes.CopyTo(body, NameOffset);
        body[NameOffset + nameBytes.Length] = 0x00;
        clanBytes.CopyTo(body, NameOffset + nameBytes.Length + 1);
        return body;
    }

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
            (sbyte)(body[4] - RosterIndexBase),
            (sbyte)(body[5] - PlayerNumberBase),
            body[NameMarkerOffset] != 0x00,
            BinaryUtility.ReadUInt16LittleEndian(body, 8),
            body[10],
            name,
            clan);
    }

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
    /// printable run precedes that NUL — trimmed back to the first byte an
    /// character name can hold, because the per-player block that precedes it is
    /// full of printable bytes of its own (0x60 in the live capture) that would
    /// otherwise be read as part of the name.
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
    /// of the block in front of it, which are printable but are not text.
    /// Only the leading edge is trimmed with it, so a name may still contain
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
/// <param name="RosterBaseField">The raw field at offset 4, which is 0x32 plus the roster index.</param>
/// <param name="RosterIndex">
/// Position of the player in the room roster, recovered from that field. It is
/// signed because the host's own slot is <c>-1</c>.
/// </param>
/// <param name="PlayerNumber">
/// The roster index recovered from the field at offset 5, which carries the same
/// index under a different base. The two fields share an index but not a base,
/// so neither can be read off the other.
/// </param>
/// <param name="HasClanName">Whether a clan name follows the character name.</param>
/// <param name="PerPlayerValue">The varying value at offset 8; its meaning is unresolved.</param>
/// <param name="TeamFlag">Team flag.</param>
/// <param name="Name">Character name.</param>
/// <param name="ClanName">Clan name.</param>
public sealed record PlayerProfileRecord(
    byte Version,
    byte CharacterIdentifier,
    byte RosterBaseField,
    sbyte RosterIndex,
    sbyte PlayerNumber,
    bool HasClanName,
    ushort PerPlayerValue,
    byte TeamFlag,
    string Name,
    string ClanName);
