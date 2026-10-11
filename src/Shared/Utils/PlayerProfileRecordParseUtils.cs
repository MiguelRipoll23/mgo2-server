using System.Text;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Reads the <c>0x1001</c> player-profile record back into a
/// <see cref="PlayerProfileRecord"/>. Writing one is
/// <see cref="PlayerProfileRecordUtility"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two layouts reach this reader and both end the same way — the character
/// name, a NUL, and a clan name running to the end of the record — so they are
/// told apart by what precedes them rather than by a length the record does not
/// carry. The roster entry puts its names at fixed offsets; a join request has
/// a longer block in front of them and no marker byte to say so, and is read
/// back from the end.
/// </para>
/// <para>
/// Nothing here is lenient. A body that is too short for the fixed fields is
/// <c>null</c> rather than a record of zeroes, and the roster close — seven
/// bytes — is one of those, so it reads as no player at all.
///
/// <para>
/// The two layouts are told apart by the record version and the length byte that
/// follows it, and the fields only the roster layout has are read only when that
/// layout is the one present. A join request is a different record that merely
/// ends the same way, and its offsets are its own.
/// </para>
/// </remarks>
public static class PlayerProfileRecordParseUtils
{
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
        if (body.Length < PlayerProfileRecordUtility.MinimumBodySize)
        {
            return null;
        }

        var name = string.Empty;
        var clan = string.Empty;
        var roster = RosterLayoutOf(body);
        var isRosterEntry = roster is not null;

        // The short layout is the long one without its twelve-byte endpoint
        // field, so the bytes at 0x13 are the tail of a different record there
        // and are read only from the long layout. Both layouts put the character
        // id and the slot before the difference, at 0x08 and 0x05.
        var hasAddressData = isRosterEntry
            && NameOffsetOf(body) == PlayerProfileRecordUtility.NameOffset;
        if (isRosterEntry)
        {
            (name, clan) = roster!.Value;
        }
        else
        {
            (name, clan) = TrailingLayoutOf(body);
        }

        // The character id, endpoint data and the name marker are read
        // only off the roster layout. A join request carries its names in the
        // same place but puts a longer block in front of them, so the same
        // offsets land on structural bytes rather than on those fields: a
        // join request's offset 0x08 is part of its own header, and reading it
        // as the id yields a number that belongs to nothing. Reading the
        // endpoint data out of a join request is worse than reading nothing,
        // because the block is twelve bytes long and so overwrites the markers
        // at 0x17/0x18/0x1d/0x1e with whatever the record happened to hold
        // there - on one live join, the first four bytes of the player's own
        // name.
        //
        // The peer identifier a roster entry is written with does not come from
        // here at all: it is the session's, from the handshake. See
        // RoomRosterService.Register.
        return new PlayerProfileRecord(
            body[0],
            body[PlayerProfileRecordUtility.RecordLengthOffset],
            isRosterEntry ? body[4] : (byte)0x00,
            isRosterEntry ? RosterIndexOf(body[4]) : PlayerProfileRecordUtility.HostRosterIndex,
            isRosterEntry ? body[5] : (byte)0x00,
            clan.Length > 0,
            isRosterEntry
                ? (int)BinaryUtility.ReadUInt32LittleEndian(body, PlayerProfileRecordUtility.CharacterIdOffset)
                : 0,
            hasAddressData ? AddressDataOf(body) : [],
            name,
            clan);
    }

    /// <summary>
    /// Recovers the roster index from the field at offset 4. The host is
    /// written as a distinct zero rather than as one below the first player, so
    /// zero means the host and everything else is the index under
    /// <see cref="PlayerProfileRecordUtility.RosterIndexBase"/>.
    /// </summary>
    /// <param name="field">Raw field at offset 4.</param>
    /// <returns>The roster index the record reports.</returns>
    public static sbyte RosterIndexOf(byte field) =>
        field == PlayerProfileRecordUtility.HostRosterField
            ? PlayerProfileRecordUtility.HostRosterIndex
            : (sbyte)(field - PlayerProfileRecordUtility.RosterIndexBase);

    /// <summary>
    /// The twelve endpoint bytes at offsets 0x13..0x1e, in wire order. The
    /// capture shows them matching the public/private pairs in this character's
    /// handshake; join requests do not carry them at these offsets.
    /// </summary>
    private static byte[] AddressDataOf(ReadOnlySpan<byte> body) =>
        body.Slice(
            PlayerProfileRecordUtility.AddressDataOffset,
            PlayerProfileRecordUtility.AddressDataLength).ToArray();

    /// <summary>
    /// Reads the names off the documented roster layout, or returns <c>null</c>
    /// when the body is not that shape.
    /// </summary>
    /// <remarks>
    /// The record's version and its own length must both hold, and the name must
    /// be non-empty; together those are the discrimination between the two
    /// layouts. The record is then read at whichever of the two name offsets it
    /// uses, which is what tells a long record from a short one.
    /// </remarks>
    private static (string Name, string Clan)? RosterLayoutOf(ReadOnlySpan<byte> body)
    {
        // A record opens with its version and a length byte that describes it. A
        // body that merely lines up with the layout is something else: the `0b`
        // settings payload does, and would otherwise read back as a record named
        // with two control bytes and be registered as a player. See
        // docs/protocol/UDP_SETTINGS_PAYLOAD.md.
        if (!PlayerProfileRecordUtility.IsRosterRecord(body))
        {
            return null;
        }

        var nameOffset = NameOffsetOf(body);
        if (body.Length < nameOffset + 2)
        {
            return null;
        }

        var nameEnd = body[nameOffset..].IndexOf((byte)0x00);
        if (nameEnd <= 0)
        {
            return null;
        }

        // Any printable run is a name here: the recorded characters carry
        // spaces and punctuation, so nothing narrower may be required.
        var name = DecodeName(body.Slice(nameOffset, nameEnd));
        var clanStart = nameOffset + nameEnd + 1;
        if (clanStart >= body.Length)
        {
            return (name, string.Empty);
        }

        var clanEnd = body[clanStart..].IndexOf((byte)0x00);
        return (
            name,
            DecodeName(clanEnd < 0 ? body[clanStart..] : body.Slice(clanStart, clanEnd)));
    }

    /// <summary>
    /// Which of the two name offsets a record uses.
    /// </summary>
    /// <remarks>
    /// A record ends with the nick, a NUL and the clan, so the nick is the
    /// printable run before the last NUL: if that run begins exactly
    /// <see cref="PlayerProfileRecordUtility.ShortNameOffset"/> bytes in, the short
    /// layout is the one present and otherwise the long. This is the replay
    /// console's <c>rosterNameOff</c>, and the captures need both — game1's roster
    /// is the long layout, game2's and game3's the short — which the header's
    /// format version, the same in each, does not distinguish.
    /// </remarks>
    /// <param name="body">Record body to measure.</param>
    /// <returns>The offset the character name begins at.</returns>
    private static int NameOffsetOf(ReadOnlySpan<byte> body)
    {
        var end = body.Length - 1;
        while (end > 0 && body[end] != 0x00)
        {
            end--;
        }

        var start = end;
        while (start > 0 && body[start - 1] >= 0x20)
        {
            start--;
        }

        return start == PlayerProfileRecordUtility.ShortNameOffset && end > start
            ? PlayerProfileRecordUtility.ShortNameOffset
            : PlayerProfileRecordUtility.NameOffset;
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
    /// Decodes bytes the way the game writes them: raw bytes in ISO-8859-1, the
    /// encoding the TCP character list uses for the same field. Latin-1 maps
    /// every byte to the codepoint of the same value, so what came off the wire
    /// survives exactly and a decode can never throw.
    /// </summary>
    private static string DecodeName(ReadOnlySpan<byte> bytes) =>
        bytes.IsEmpty ? string.Empty : Encoding.Latin1.GetString(bytes);
}