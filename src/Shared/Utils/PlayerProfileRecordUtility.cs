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
    /// there: <c>0x01</c> on the host's own entry and <c>0x02</c> on a joining
    /// player's. It does not follow the wire type, since a player's entry appears
    /// under both <c>0x9001</c> and <c>0x1001</c> and reads the same in each, so
    /// it is written per record rather than per type. What it counts is **[U]**:
    /// nothing in the capture names it, and the two values are carried here as
    /// the two values they are measured to be.
    /// </remarks>
    public const byte HostEntryUnresolvedByte = 0x01;

    /// <summary>
    /// Value a joining player's roster record carries at offset <c>0x10</c>.
    /// </summary>
    /// <remarks>Unresolved for the same reason as <see cref="HostEntryUnresolvedByte"/>.</remarks>
    public const byte PlayerRecordUnresolvedByte = 0x02;
    /// <summary>Record version byte every recorded roster record carries.</summary>
    public const byte RecordVersion = 0x07;

    /// <summary>
    /// Offset of the record's own length byte — which is not a sub-type. The byte
    /// counts the record's bytes past <see cref="FixedPrefixLength"/>, so it moves
    /// with the character name; <see cref="PlayerProfileRecordLayout"/> carries the
    /// measurement.
    /// </summary>
    public const int RecordLengthOffset = 0x01;

    /// <summary>
    /// The seven bytes the value at <see cref="RecordLengthOffset"/> counts past:
    /// a record's length is that byte plus this.
    /// </summary>
    public const int FixedPrefixLength = 0x07;

    /// <summary>
    /// Whether a body opens the way a roster record does: the record version, and
    /// a length byte that agrees with the body it is in.
    /// </summary>
    /// <remarks>
    /// The version and the length tell a record from a body that merely lines up
    /// with the layout: the <c>0b</c> settings payload fails both, and without the
    /// test it reads back as a record named with two control bytes. The kind is
    /// the third — an entry is <c>0x00</c>, and the records that share this
    /// version and identifier without being one carry <c>0x01</c>–<c>0x04</c>
    /// there; <see cref="PlayerProfileRecordLayout"/> has them.
    /// </remarks>
    /// <param name="body">Body to test.</param>
    /// <returns><c>true</c> when the body opens a roster record.</returns>
    public static bool IsRosterRecord(ReadOnlySpan<byte> body) =>
        body.Length > RecordLengthOffset &&
        body[0] == RecordVersion &&
        body[PlayerProfileRecordLayout.KindOffset] ==
            PlayerProfileRecordLayout.RosterKind &&
        body[RecordLengthOffset] == body.Length - FixedPrefixLength;

    /// <summary>
    /// Leading byte a joining client puts on the record it opens the exchange
    /// with. It is not a roster entry, so it never appears in what the host
    /// sends back.
    /// </summary>
    public const byte JoinRequestVersion = 0x02;

    /// <summary>
    /// Roster slot the host itself takes, as this server reads it.
    /// </summary>
    /// <remarks>
    /// Nothing on the wire is <c>-1</c>: the host's own entry writes <c>0x00</c>
    /// at <c>0x04</c> where joining players write a base plus their index, and
    /// reading that zero as "one below the first player" is this server's
    /// inference. Whether it marks a dedicated host or the host's own slot is
    /// **[U]** — game2's host writes <c>0x01</c>/<c>0x02</c> there on a scale of
    /// its own, so even the base is per host.
    /// </remarks>
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
    /// Value written at offsets 5 and 7: the player's slot in the host's
    /// 24-entry network player table, which is the byte the replay console's
    /// <c>rpdt.js</c> calls the record's <c>slot</c>. The values match the member
    /// bytes in the <c>0x0261</c>/<c>0x0a61</c> rules-roster-shaped records, and
    /// the client binary's lookup <c>0x26d5a8</c> walks exactly that table
    /// (24 records of <c>0x74</c> bytes at <c>net + 0xd4</c>, slot in the first
    /// byte, <c>0xff</c> empty). Every observed value lies in <c>0</c>…<c>0x17</c>:
    /// the seven roster records in <c>tools/replays/</c> carry
    /// <c>2, 5, 7, 9, 13, 17, 21</c> and the live capture
    /// <c>4, 6, 11, 15, 20</c> (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §8.11).
    /// <para>
    /// Zero is the slot the client's table is built from, and it is the host's
    /// own: the recorded host's own entry carries it and no joined player's does,
    /// in any captured round. A joining player is therefore given a slot of its
    /// own and never this one, which is why <see cref="Build"/> takes it as a
    /// required argument rather than defaulting a record to this value.
    /// </para>
    /// </summary>
    public const byte HostSessionSlot = 0x00;

    /// <summary>
    /// Byte preceding the character name: 0x03 when a clan name follows, 0x00
    /// when none does, so it marks the presence of one rather than being a
    /// constant.
    /// </summary>
    private const byte ClanNameMarker = 0x03;

    /// <summary>
    /// The byte at <see cref="BlockConstantOffset"/>: the number of endpoint
    /// pairs behind it, <c>0x02</c> in all sixty-one long entries on the bench and
    /// <c>0x00</c> in all ninety-one short ones.
    /// </summary>
    private const byte BlockConstant = 0x02;

    /// <summary>Offset of <see cref="BlockConstant"/> within the record body.</summary>
    private const int BlockConstantOffset = 0x12;

    /// <summary>
    /// Offset of the pair of bytes the host's own entry carries and a player's
    /// does not.
    /// </summary>
    private const int HostEntryBlockFlagOffset = 0x0c;

    /// <summary>First byte of that pair on the host's own entry.</summary>
    private const byte HostEntryBlockFlag = 0x01;

    /// <summary>Second byte of that pair on the host's own entry.</summary>
    private const byte HostEntryBlockFlagSecond = 0x02;

    /// <summary>
    /// Value the pair carries on a player's record: zero in both bytes. Every
    /// captured player entry reads zero at both, whatever type it travels as.
    /// </summary>
    private const byte PlayerBlockFlag = 0x00;

    /// <summary>
    /// Offset of the unresolved per-record value the captured roster reads
    /// <c>0x00</c>, <c>0x01</c> and <c>0x02</c> at.
    /// </summary>
    public const int UnresolvedByteOffset = 0x10;

    /// <summary>
    /// Length of one endpoint pair: four address octets and then a little-endian
    /// port. The field's two pairs sit back to back, so the second opens where
    /// the first ends.
    /// </summary>
    /// <remarks>
    /// <b>The four bytes at <c>0x17</c>/<c>0x18</c> and <c>0x1d</c>/<c>0x1e</c>
    /// are these pairs' ports, not a constant of the layout.</b> They were read
    /// as a per-peer marker (<c>0x63</c> on the host's own entry, <c>0x62</c> on
    /// a player's) followed by a constant trailer (<c>0x16</c>), and they are
    /// two ports: the capture round's host answers on 5731, which is
    /// <c>0x1663</c>, and its joiner on 5730, which is <c>0x1662</c>, each
    /// repeated in that peer's own two pairs. So the pair is written from the
    /// endpoint the peer announced and never from a constant here.
    /// <see cref="PlayerProfileRecordLayout"/> carries the measurement.
    /// </remarks>
    public const int EndpointPairLength = 0x06;

    /// <summary>First offset of the two endpoint pairs announced in the handshake.</summary>
    public const int AddressDataOffset = 0x13;

    /// <summary>
    /// Length of the endpoint-data field: the two pairs, each an address and the
    /// port it is reached on, written back to back.
    /// </summary>
    public const int AddressDataLength = 2 * EndpointPairLength;

    /// <summary>Offset of the character id.</summary>
    public const int CharacterIdOffset = 0x08;

    /// <summary>Offset of the constant name marker.</summary>
    public const int NameMarkerOffset = 0x43;

    /// <summary>Offset of the NUL-terminated character name.</summary>
    public const int NameOffset = 0x44;

    /// <summary>
    /// Offset of the character name in the short record layout, twelve bytes below
    /// <see cref="NameOffset"/>. Which of the two a record uses is decided per
    /// record; <see cref="PlayerProfileRecordLayout"/> says how.
    /// </summary>
    public const int ShortNameOffset = 0x38;

    /// <summary>
    /// Smallest body a profile record can have: the <b>short</b> layout's name
    /// offset plus the NUL and one character — a record without the endpoint
    /// field carries its name at <see cref="ShortNameOffset"/> and the shortest on
    /// the bench is 61 bytes, where a floor at <see cref="NameOffset"/> refused them.
    /// </summary>
    internal const int MinimumBodySize = ShortNameOffset + 2;

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
    /// <param name="isHostEntry">
    /// Whether this is the host's own entry rather than a joining player's: the
    /// head of a roster run, carrying the host's own character id and name. It
    /// selects the pair at <c>0x0c</c>/<c>0x0d</c> — not what <c>0x01</c>
    /// carries, which is the length, and not the endpoint field, which is the
    /// endpoints the peer announced rather than anything this flag chooses.
    /// </param>
    /// <param name="rosterIndex">
    /// Position of the player in the room roster, counted from
    /// <see cref="HostRosterIndex"/>. It is signed because the host's own slot
    /// is the one below zero.
    /// </param>
    /// <param name="characterId">Character id written at offset 8.</param>
        /// <param name="name">Character name.</param>
    /// <param name="clanName">Clan name, which may be empty.</param>
    /// <param name="addressData">
    /// The two IPv4 endpoint pairs, copied from the peer handshake. Null or empty
    /// leaves the measured structural bytes in place.
    /// </param>
    /// <param name="unresolvedByte">
    /// Value written at offset <c>0x10</c>. The captured roster reads
    /// <c>0x01</c> on the host's own entry and <c>0x02</c> on a player's, and the
    /// value does not follow the wire type, so it is passed in rather than
    /// derived here. What it means is **[U]**.
    /// </param>
    /// <param name="sessionSlot">
    /// The player's slot in the host's 24-entry network player table, written at
    /// offsets <c>0x05</c> and <c>0x07</c>. It is required, because the
    /// captures leave no room for a default: zero is the host's own slot
    /// (<see cref="HostSessionSlot"/>) and no joined player's entry carries it,
    /// so a record built without one would put the player on the host's slot of
    /// the client's table.
    /// </param>
    public static byte[] Build(
        bool isHostEntry,
        sbyte rosterIndex,
        int characterId,
        string name,
        string clanName,
        byte sessionSlot,
        byte[]? addressData = null,
        byte unresolvedByte = PlayerRecordUnresolvedByte)
    {
        var nameBytes = Truncate(Encoding.ASCII.GetBytes(name ?? string.Empty), MaximumNameLength);
        var clanBytes = Truncate(Encoding.ASCII.GetBytes(clanName ?? string.Empty), MaximumClanLength);

        // The name's NUL terminator is written explicitly; the clan name runs to
        // the end of the record and carries none, which is what the recorded
        // records do.
        var body = new byte[NameOffset + 1 + nameBytes.Length + clanBytes.Length];
        body[0] = RecordVersion;
        body[RecordLengthOffset] = (byte)(body.Length - FixedPrefixLength);
        body[4] = rosterIndex == HostRosterIndex
            ? HostRosterField
            : (byte)(RosterIndexBase + rosterIndex);
        body[5] = sessionSlot;
        body[7] = sessionSlot;
        BinaryUtility.WriteUInt32LittleEndian(body, CharacterIdOffset, (uint)characterId);
        body[BlockConstantOffset] = BlockConstant;
        body[HostEntryBlockFlagOffset] = isHostEntry ? HostEntryBlockFlag : PlayerBlockFlag;
        body[HostEntryBlockFlagOffset + 1] = isHostEntry ? HostEntryBlockFlagSecond : PlayerBlockFlag;
        body[UnresolvedByteOffset] = unresolvedByte;

        // The endpoint field is the peer's two announced endpoints and nothing
        // else; a body built with none leaves it zero rather than inventing an
        // address and port for the peer it belongs to.
        WriteAddressData(body, addressData);

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
    /// Copies endpoint data into a record body: the two pairs at
    /// <see cref="AddressDataOffset"/>, each an address and its little-endian
    /// port. The field is fixed at <see cref="AddressDataLength"/> bytes, so a
    /// longer input is truncated and a shorter one is padded.
    /// </summary>
    /// <remarks>
    /// The four bytes at <c>0x17</c>/<c>0x18</c> and <c>0x1d</c>/<c>0x1e</c> are
    /// the two ports and nothing else — see <see cref="EndpointPairLength"/>. They
    /// travel with the pairs, which is why a record whose peer announced an
    /// endpoint carries that peer's own port at them; nothing here writes a
    /// constant over them, and a record built without endpoint data leaves them
    /// at zero rather than at a port that belongs to no one.
    /// </remarks>
    private static void WriteAddressData(Span<byte> body, byte[]? addressData)
    {
        if (addressData is null || addressData.Length == 0)
        {
            return;
        }

        addressData.AsSpan(0, Math.Min(addressData.Length, AddressDataLength)).CopyTo(body[AddressDataOffset..]);
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
/// <param name="RecordLength">
/// Byte at offset 1 read back raw. It is the record's own length less seven, not
/// a record kind; <see cref="PlayerProfileRecordUtility.IsRosterRecord"/> is the
/// test over it.
/// </param>
/// <param name="RosterBaseField">Raw field at offset 4: 0xe2 plus the index, or zero for the host.</param>
/// <param name="RosterIndex">
/// Position of the player in the room roster, recovered from that field. It is
/// signed because the host's own slot is <c>-1</c>, written as a plain zero
/// rather than as one below the first player's.
    /// </param>
    /// <param name="sessionSlot">
    /// The player's slot in the host's 24-entry network player table (offsets 0x05
    /// and 0x07 of the roster entry), the byte the replay console's <c>rpdt.js</c>
    /// calls <c>slot</c>. It varies per player without following roster order and
    /// matches the member bytes in the <c>0x0261</c>/<c>0x0a61</c>
    /// rules-roster-shaped records, so it is not an index. The client binary names
    /// the field via the table at <c>0x26d5a8</c>; how the host selects it is
    /// unresolved. The live capture calls these "member bytes" and confirms they
    /// are slots in a 24-entry table.
    /// </param>
/// <param name="HasClanName">Whether a clan name was read alongside the name.</param>
/// <param name="CharacterId">The character id at offset 8, little-endian.</param>
/// <param name="AddressData">
/// The public and private IPv4 endpoint pairs, copied from the character's
/// handshake and carried as twelve bytes in the roster record.
/// </param>
/// <param name="Name">Character name.</param>
/// <param name="ClanName">Clan name.</param>
    public sealed record PlayerProfileRecord(
        byte Version,
        byte RecordLength,
        byte RosterBaseField,
        sbyte RosterIndex,
        byte SessionSlot,
        bool HasClanName,
        int CharacterId,
        byte[] AddressData,
        string Name,
        string ClanName);