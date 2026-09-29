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
/// [0x08] u16  per-player value that varies across the roster
/// [0x0a] u8   team flag
/// [0x0b..0x43] zero
/// [0x43] u8   constant 0x03
/// [0x44] char[]  NUL-terminated account name
///          char[]  clan name, running to the end of the record
/// </code>
/// <para>
/// The name is NUL-terminated; the clan name is not, because the record ends
/// there. Both are bounded, so a name longer than the buffer is truncated
/// rather than allowed to overrun the frame.
/// </para>
/// </remarks>
public static class PlayerProfileRecordUtility
{
    /// <summary>Record version byte every recorded profile carries.</summary>
    public const byte RecordVersion = 0x07;

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
    /// Byte preceding the account name. It reads 0x03 in every recorded record
    /// that carries a clan name and 0x00 in the one that does not, so it marks
    /// whether a clan name follows rather than being a constant.
    /// </summary>
    private const byte ClanNameMarker = 0x03;

    /// <summary>Offset of the constant name marker.</summary>
    public const int NameMarkerOffset = 0x43;

    /// <summary>Offset of the NUL-terminated account name.</summary>
    public const int NameOffset = 0x44;

    /// <summary>
    /// Smallest body a profile record can have. A body must reach past the
    /// fixed fields to carry a name, so this is the fixed-field size plus the
    /// marker and at least one character.
    /// </summary>
    private const int MinimumBodySize = NameOffset + 2;

    /// <summary>Longest account name a record will carry.</summary>
    public const int MaximumNameLength = 20;

    /// <summary>Longest clan name a record will carry.</summary>
    public const int MaximumClanLength = 24;

    /// <summary>
    /// Builds a profile record body.
    /// </summary>
    /// <param name="characterIdentifier">Character identifier of the player.</param>
    /// <param name="rosterIndex">Position of the player in the room roster.</param>
    /// <param name="perPlayerValue">
    /// The varying value written at offset 8. It differs per player in every
    /// recorded match and its meaning is unresolved, so it is passed through.
    /// </param>
    /// <param name="teamFlag">Team flag written at offset 10.</param>
    /// <param name="name">Account name.</param>
    /// <param name="clanName">Clan name, which may be empty.</param>
    public static byte[] Build(
        byte characterIdentifier,
        byte rosterIndex,
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

        var nameEnd = body[NameOffset..].IndexOf((byte)0x00);
        var name = nameEnd < 0
            ? Encoding.ASCII.GetString(body[NameOffset..])
            : Encoding.ASCII.GetString(body.Slice(NameOffset, nameEnd));
        var clan = body.Length > NameOffset + name.Length + 1
            ? Encoding.ASCII.GetString(body[(NameOffset + name.Length + 1)..])
            : string.Empty;

        return new PlayerProfileRecord(
            body[0],
            body[1],
            body[4],
            (byte)(body[4] - RosterIndexBase),
            (byte)(body[5] - PlayerNumberBase),
            body[NameMarkerOffset] != 0x00,
            BinaryUtility.ReadUInt16LittleEndian(body, 8),
            body[10],
            name,
            clan);
    }

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
/// <param name="RosterIndex">Position of the player in the room roster, recovered from that field.</param>
/// <param name="PlayerNumber">
/// The value at offset 5, which is 6 plus the roster index. The two fields
/// share an index but not a base, so neither can be read off the other.
/// </param>
/// <param name="HasClanName">Whether a clan name follows the account name.</param>
/// <param name="PerPlayerValue">The varying value at offset 8; its meaning is unresolved.</param>
/// <param name="TeamFlag">Team flag.</param>
/// <param name="Name">Account name.</param>
/// <param name="ClanName">Clan name.</param>
public sealed record PlayerProfileRecord(
    byte Version,
    byte CharacterIdentifier,
    byte RosterBaseField,
    byte RosterIndex,
    byte PlayerNumber,
    bool HasClanName,
    ushort PerPlayerValue,
    byte TeamFlag,
    string Name,
    string ClanName);
