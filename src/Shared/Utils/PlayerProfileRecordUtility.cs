using System.Text;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Builds the <c>0x1001</c> player-profile record. Reading one back is
/// <see cref="PlayerProfileRecordParseUtils.Parse"/>, and what each offset of it
/// means is <see cref="PlayerProfileRecordLayout"/>.
/// </summary>
public static class PlayerProfileRecordUtility
{
    /// <summary>
    /// Value the room's own roster record carries at offset <c>0x10</c>.
    /// </summary>
    /// <remarks>
    /// Every record of the captured roster reads <c>0x01</c> or <c>0x02</c>
    /// there. It does not follow the record type, since a player entry appears
    /// under both <c>0x9001</c> and <c>0x1001</c> and reads the same in each,
    /// so it is written per record rather than per type. What it counts is
    /// **[U]**: nothing in the capture names it, and the two values are carried
    /// here as the two values they are measured to be.
    /// </remarks>
    public const byte RoomRecordUnresolvedByte = 0x01;

    /// <summary>
    /// Value a joining player's roster record carries at offset <c>0x10</c>.
    /// </summary>
    /// <remarks>Unresolved for the same reason as <see cref="RoomRecordUnresolvedByte"/>.</remarks>
    public const byte PlayerRecordUnresolvedByte = 0x02;
    /// <summary>Record version byte every recorded roster entry carries.</summary>
    public const byte RecordVersion = 0x07;

    /// <summary>
    /// Sub-type byte on a player's roster entry. The captured entries carry
    /// this byte and, for one character, <c>0x47</c> — one bit apart — so the
    /// two are the same sub-type and what the bit selects is **[U]**.
    /// </summary>
    public const byte PlayerEntrySubType = 0x48;

    /// <summary>
    /// Sub-type byte on the room's own record, which the host sends as the head
    /// of the roster run. It is a different record from a player's entry: it
    /// carries no roster index, no clan, and the room's own name.
    /// </summary>
    public const byte RoomRecordSubType = 0x4c;

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
    internal const byte RosterIndexBase = 0xe2;

    /// <summary>
    /// What the host writes at offset 4, which is not
    /// <c><see cref="RosterIndexBase"/> - 1</c>: the captured host record
    /// carries a plain zero and the players are on their own scale.
    /// </summary>
    internal const byte HostRosterField = 0x00;

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
    /// The byte at <see cref="BlockConstantOffset"/>. It is the same in all six
    /// captured records and is the only non-zero constant in the part of the
    /// block before the name marker that every record agrees on.
    /// </summary>
    private const byte BlockConstant = 0x02;

    /// <summary>Offset of <see cref="BlockConstant"/> within the record body.</summary>
    private const int BlockConstantOffset = 0x12;

    /// <summary>
    /// Offset of the pair of bytes the room's own record carries and a player's
    /// does not.
    /// </summary>
    private const int RoomBlockFlagOffset = 0x0c;

    /// <summary>First byte of that pair on the room record.</summary>
    private const byte RoomBlockFlag = 0x01;

    /// <summary>Second byte of that pair on the room record.</summary>
    private const byte RoomBlockFlagSecond = 0x02;

    /// <summary>
    /// Value the pair carries on a player's record: zero in both bytes. Every
    /// captured player entry reads zero at both, whatever type it travels as.
    /// </summary>
    private const byte PlayerBlockFlag = 0x00;

    /// <summary>
    /// Offset of the unresolved per-record value the captured roster reads
    /// <c>0x01</c> and <c>0x02</c> at.
    /// </summary>
    public const int UnresolvedByteOffset = 0x10;

    /// <summary>
    /// Marker opening the second and third halves of the appearance block. It
    /// is 0x62 on every player entry and 0x63 on the room record, so it says
    /// which kind of record this is and nothing more.
    /// </summary>
    private const byte PlayerBlockMarker = 0x62;

    /// <summary>Value <see cref="PlayerBlockMarker"/> takes on the room record.</summary>
    private const byte RoomBlockMarker = 0x63;

    /// <summary>
    /// Byte closing each half of the appearance block. It is 0x16 in all six
    /// captured records whatever the marker before it is.
    /// </summary>
    private const byte BlockTrailer = 0x16;

    /// <summary>
    /// Offsets of the three constants inside the appearance block: the marker
    /// and the trailer of each of its two halves.
    /// </summary>
    private static ReadOnlySpan<int> BlockMarkerOffsets => [0x17, 0x1d];

    /// <summary>Offsets of the two block trailers.</summary>
    private static ReadOnlySpan<int> BlockTrailerOffsets => [0x18, 0x1e];

    /// <summary>First offset of the character's appearance block.</summary>
    public const int AppearanceOffset = 0x13;

    /// <summary>Length of the character's appearance block.</summary>
    public const int AppearanceLength = 0x1e - AppearanceOffset + 1;

    /// <summary>Offset of the character id.</summary>
    public const int CharacterIdOffset = 0x08;

    /// <summary>Offset of the constant name marker.</summary>
    public const int NameMarkerOffset = 0x43;

    /// <summary>Offset of the NUL-terminated character name.</summary>
    public const int NameOffset = 0x44;

    /// <summary>
    /// Smallest body a profile record can have. A body must reach past the
    /// fixed fields to carry a name, so this is the fixed-field size plus the
    /// marker and at least one character.
    /// </summary>
    internal const int MinimumBodySize = NameOffset + 2;

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
    /// <param name="recordSubType">
    /// Byte written at offset 1: <see cref="PlayerEntrySubType"/> for a
    /// player's entry, <see cref="RoomRecordSubType"/> for the room's own.
    /// </param>
    /// <param name="rosterIndex">
    /// Position of the player in the room roster, counted from
    /// <see cref="HostRosterIndex"/>. It is signed because the host's own slot
    /// is the one below zero.
    /// </param>
    /// <param name="characterId">Character id written at offset 8.</param>
        /// <param name="name">Character name.</param>
    /// <param name="clanName">Clan name, which may be empty.</param>
    /// <param name="appearance">
    /// The character's appearance block as it was read off a profile. Null or
    /// empty writes the block's measured constants and zeros, which is what a
    /// record built without a character to copy them from looks like.
    /// </param>
    /// <param name="unresolvedByte">
    /// Value written at offset <c>0x10</c>. The captured roster reads
    /// <c>0x01</c> on its room record and <c>0x02</c> on a player's, and the
    /// value does not follow the record type, so it is passed in rather than
    /// derived here. What it means is **[U]**.
    /// </param>
    public static byte[] Build(
        byte recordSubType,
        sbyte rosterIndex,
        int characterId,
        string name,
        string clanName,
        byte[]? appearance = null,
        byte unresolvedByte = PlayerRecordUnresolvedByte)
    {
        var nameBytes = Truncate(Encoding.ASCII.GetBytes(name ?? string.Empty), MaximumNameLength);
        var clanBytes = Truncate(Encoding.ASCII.GetBytes(clanName ?? string.Empty), MaximumClanLength);

        // The name's NUL terminator is written explicitly; the clan name runs to
        // the end of the record and carries none, which is what the recorded
        // records do.
        var body = new byte[NameOffset + 1 + nameBytes.Length + clanBytes.Length];
        body[0] = RecordVersion;
        body[1] = recordSubType;
        body[4] = rosterIndex == HostRosterIndex
            ? HostRosterField
            : (byte)(RosterIndexBase + rosterIndex);
        body[5] = PlayerValue;
        body[7] = PlayerValue;
        BinaryUtility.WriteUInt32LittleEndian(body, CharacterIdOffset, (uint)characterId);
        body[BlockConstantOffset] = BlockConstant;
        body[RoomBlockFlagOffset] = recordSubType == RoomRecordSubType ? RoomBlockFlag : PlayerBlockFlag;
        body[RoomBlockFlagOffset + 1] = recordSubType == RoomRecordSubType ? RoomBlockFlagSecond : PlayerBlockFlag;
        body[UnresolvedByteOffset] = unresolvedByte;

        var marker = recordSubType == RoomRecordSubType ? RoomBlockMarker : PlayerBlockMarker;
        foreach (var offset in BlockMarkerOffsets)
        {
            body[offset] = marker;
        }

        foreach (var offset in BlockTrailerOffsets)
        {
            body[offset] = BlockTrailer;
        }

        // The caller's block is written last so that it wins over the constants:
        // a block read off a record is the record's own, and the two only differ
        // where the record it came from was not a player entry.
        WriteAppearance(body, appearance);

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
    /// own. Its body is too short to carry a name, so
    /// <see cref="PlayerProfileRecordParseUtils.Parse"/> returns <c>null</c> for
    /// it and it cannot be mistaken for a player.
    /// </remarks>
    public static byte[] BuildRosterClose() => [.. ClosingRecordBytes];

    /// <summary>
    /// Copies an appearance block into a record body, leaving the bytes it does
    /// not cover at zero. A block longer than the field is truncated and a
    /// shorter one is padded, so the record's layout never moves.
    /// </summary>
    private static void WriteAppearance(Span<byte> body, byte[]? appearance)
    {
        if (appearance is null || appearance.Length == 0)
        {
            return;
        }

        appearance.AsSpan(0, Math.Min(appearance.Length, AppearanceLength)).CopyTo(body[AppearanceOffset..]);
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
/// <param name="RecordSubType">
/// Byte at offset 1, which says what kind of record this is rather than who it
/// is about: <see cref="PlayerProfileRecordUtility.PlayerEntrySubType"/> on a
/// player's entry, <see cref="PlayerProfileRecordUtility.RoomRecordSubType"/> on
/// the room's own.
/// </param>
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
/// <param name="CharacterId">The character id at offset 8, little-endian.</param>
/// <param name="Appearance">
/// The character's appearance block, twelve bytes. It is the same for a
/// character on every entry it appears in, which is what separates it from the
/// per-occurrence values around it.
/// </param>
/// <param name="Name">Character name.</param>
/// <param name="ClanName">Clan name.</param>
public sealed record PlayerProfileRecord(
    byte Version,
    byte RecordSubType,
    byte RosterBaseField,
    sbyte RosterIndex,
    byte PlayerValue,
    bool HasClanName,
    int CharacterId,
    byte[] Appearance,
    string Name,
    string ClanName);