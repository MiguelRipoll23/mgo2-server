using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Builds the settings records the recorded host sends once, about two and a
/// half seconds after it has answered a joiner with the roster: the room's
/// rotation, the joining player's loadout and a second copy of it.
/// </summary>
/// <remarks>
/// <para>
/// The live capture holds exactly one burst in the whole round
/// (<c>docs/mgo2-game.pcapng</c>, t+4.9929, outbound counter 5, 366 bytes, six
/// records). It is not resent: no later datagram repeats that shape, and the
/// three peers that join at t+154, t+385 and t+493 do not each get one. So it
/// is sent when the first joiner is answered and not again.
/// </para>
/// <para>
/// It opens with a one-byte <see cref="UdpCommandConstants.InGameControl"/>
/// record, carries the room's own settings under
/// <see cref="RoomRosterService.HostEntryType"/>, then the payloads under
/// <see cref="UdpCommandConstants.PlayerProfile"/> and closes with a
/// <see cref="UdpCommandConstants.GameStateBlob"/>. A seventh record, a second
/// one-byte control record, follows 300 µs later on its own counter.
/// </para>
/// <para>
/// <b>What the payload bodies mean is settled by the replay parser.</b> Every
/// one of them opens on <c>0x0b</c>, which is the replay's
/// <c>TYPE_SETTINGS</c> discriminator, and its byte <c>+1</c> is the slot the
/// record belongs to: slot <c>0x00</c> with a body of 71 bytes or more is the
/// room's settings block, from which the parser reads fifteen rotation triples
/// at body <c>+26</c> — map id, mode, and an unresolved byte; a slot of
/// <c>0x01</c> or more with a length in 50…58 is a player's loadout, carrying
/// its team byte, its appearance and its skills (<c>docs/analysis/replay-console/rpdt.js</c>,
/// laid out in <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c> and mapped in
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>). The room's 161-byte body reads
/// as maps 5, 7, 10, 6, 14, 15, 3, 17, 2, 9, 12, 4, 1, 8, 13 in modes 1, 5, 3,
/// 7, 1, 4, 1, 2, 16, 1, 5, 4, 1, 6, 5 — a fifteen-round rotation — and the
/// joiner's 50-byte body as its appearance. The bodies are kept as the bytes the
/// recorded host wrote, because the fields behind them are the player's own and
/// this server does not have them.
/// </para>
/// <para>
/// The captured burst holds <i>three</i> payloads under <c>0x1001</c> — a
/// fifty-byte one in slot <c>0x01</c> and two fifty-four-byte ones in slot
/// <c>0x15</c>, byte for byte identical, for its single joiner: loadouts for the
/// player slots their bytes name (0 and 20), written once rather than per joiner
/// because the capture cannot say whether they belong to the joiner, to the
/// host, or to the round.
/// </para>
/// <para>
/// One thing is worth recording because it is easy to assume otherwise: these
/// payloads are not a two-player event. <c>0b</c> bodies appear 580 times over
/// the round, in 470 datagrams, running from t+4.99 to the last second at
/// t+825.6, and 504 of them go out once four peer handles have been announced.
/// What is a one-shot is the burst, not the payload family.
/// </para>
/// </remarks>
/// <param name="roster">Roster of the room this host is playing.</param>
public sealed class PlayerSettingsBurstService(RoomRosterService roster)
{
    /// <summary>
    /// Leading byte of every <c>0b</c> payload body, whatever record type
    /// carries it: the replay parser's <c>TYPE_SETTINGS</c> discriminator, and
    /// the byte that tells a payload apart from a <c>07 48</c> roster entry on
    /// the same <c>0x9001</c>/<c>0x1001</c> record.
    /// </summary>
    public const byte SettingsPayloadType = 0x0b;

    /// <summary>
    /// Slot byte on the room's own settings — the rotation, which the parser
    /// reads only from slot <c>0x00</c>.
    /// </summary>
    public const byte RoomRotationSlot = 0x00;

    /// <summary>Body length of the room's rotation record, as measured.</summary>
    public const int RoomRotationPayloadLength = 161;

    /// <summary>Slot byte on a joining player's loadout record.</summary>
    public const byte PlayerLoadoutSlot = 0x01;

    /// <summary>Body length of a joining player's loadout record, as measured.</summary>
    public const int PlayerLoadoutPayloadLength = 50;

    /// <summary>Slot byte on the two byte-identical loadout records.</summary>
    public const byte SecondLoadoutSlot = 0x15;

    /// <summary>Body length of each of the two second loadout records, as measured.</summary>
    public const int SecondLoadoutPayloadLength = 54;

    /// <summary>Control byte the burst opens with.</summary>
    public const byte OpeningControlByte = 0x24;

    /// <summary>Control byte of the record that follows the burst.</summary>
    public const byte ClosingControlByte = 0x02;

    /// <summary>
    /// The room's own settings block, copied from the capture at t+4.9929: the
    /// fifteen rotation triples the replay parser reads at body <c>+26</c> and
    /// the fields around them.
    /// </summary>
    private const string RoomRotationPayloadHex =
        "0b00ff27feeeffff3f03000000000000000000000000000000000501000705000a0300060700"
        + "0e01000f04000301001102000210000901000c05000404000101000806000d05000000000fb4"
        + "0002032a620700b4000c02010000280000002800020100020a2c01a401b4002c011c022c012c"
        + "01f00003d0a0ac00242200b400341700002c012c010202040202020400020402022863000300"
        + "01017f7ffff3e50e01";

    /// <summary>
    /// The fifty-byte loadout record a joining player is announced under.
    /// </summary>
    private const string PlayerLoadoutPayloadHex =
        "0b01020000000000000000000000000000000000005002feffc2051e452f573967680e0e000e0e00000b16000e0e070f0000";

    /// <summary>
    /// The fifty-four-byte loadout record written twice, in slot <c>0x15</c>.
    /// The parser reads the same appearance out of it as out of the joiner's
    /// record; why the recorded host sends it again is not established, so it is
    /// named for the slot it travels in.
    /// </summary>
    private const string SecondLoadoutPayloadHex =
        "0b15020000000000000000000000000000007800005002ff01030201ff78051e452f573967680e0e000e0e00000b16000e0e070f0000";

    /// <summary>
    /// Builds the burst in the order the recorded host sends it: the opening
    /// control byte, the room's rotation, one loadout per joining player, the
    /// two second loadouts, and the state blob that closes it.
    /// </summary>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildSettingsBurst()
    {
        // The fourth byte continues the roster run's numbering instead of
        // restarting it. At the join the run reads 0 for its head and the host's
        // own entry, 1 for the player and 2 for the close, and the burst that
        // follows reads 3, 4, 5 and 6 — the rotation, the player's loadout, and
        // the two second loadouts, in that order. Writing zero on all four, as
        // this did, tells the peer the same sequence four times over.
        var ordinal = roster.NextRunOrdinal;
        var burst = new List<RosterRecord>
        {
            new(UdpCommandConstants.InGameControl, [OpeningControlByte]),
            new(RoomRosterService.HostEntryType, RoomRotationPayload(), ordinal++),
        };

        for (var joiner = 0; joiner < roster.JoinerCount; joiner++)
        {
            burst.Add(new(
                UdpCommandConstants.PlayerProfile,
                PlayerLoadoutPayload(),
                ordinal++));
        }

        // The capture writes these two once, byte for byte identical, and does
        // not repeat them for the peers that join later in the round.
        burst.Add(new(UdpCommandConstants.PlayerProfile, SecondLoadoutPayload(), ordinal++));
        burst.Add(new(UdpCommandConstants.PlayerProfile, SecondLoadoutPayload(), ordinal));

        burst.Add(new(UdpCommandConstants.GameStateBlob, BuildClosingStateBlob()));
        return burst;
    }

    /// <summary>
    /// Builds the single one-byte control record the recorded host sends on the
    /// counter after the burst, 300 µs behind it.
    /// </summary>
    /// <returns>The record, ready to be sent.</returns>
    public static RosterRecord BuildClosingControl() =>
        new(UdpCommandConstants.InGameControl, [ClosingControlByte]);

    /// <summary>The room's rotation body, as the capture wrote it.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] RoomRotationPayload() => Convert.FromHexString(RoomRotationPayloadHex);

    /// <summary>A joining player's loadout body, as the capture wrote it.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] PlayerLoadoutPayload() => Convert.FromHexString(PlayerLoadoutPayloadHex);

    /// <summary>
    /// The fifty-four-byte loadout body the capture writes twice, as recorded.
    /// </summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] SecondLoadoutPayload() => Convert.FromHexString(SecondLoadoutPayloadHex);

    /// <summary>
    /// Builds a settings-record body: the type byte, the slot, and zeros for the
    /// rest of the measured length.
    /// </summary>
    /// <remarks>
    /// Kept for the shape assertions in the tests. Nothing in the send path
    /// calls it: a zero-filled body is not one the peer has been seen to read,
    /// and it was the reason the burst carried nothing the client could use.
    /// </remarks>
    /// <param name="slot">Slot byte, which is what tells two payloads apart.</param>
    /// <param name="length">Body length to build, from the measured lengths.</param>
    /// <returns>The record body.</returns>
    public static byte[] BuildPlaceholderPayload(byte slot, int length)
    {
        if (length < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                "A settings payload carries its type byte and its slot, so it cannot be shorter than two bytes.");
        }

        var body = new byte[length];
        body[0] = SettingsPayloadType;
        body[1] = slot;
        return body;
    }

    /// <summary>
    /// Builds the state blob the burst closes with: eighteen zero bytes, a
    /// marker, a zero, and a sixteen-bit big-endian value.
    /// </summary>
    /// <remarks>
    /// The shape is fixed across all 141 blobs the round contains, and only the
    /// trailing value moves — 100 for 56 of them, then 0, 94, 98, 99 and the 50
    /// this burst carries. What it counts is unknown, and the replay parser's
    /// record table does not dispatch its identifier either.
    /// </remarks>
    /// <returns>The record body.</returns>
    public static byte[] BuildClosingStateBlob()
    {
        var body = new byte[StateBlobLength];
        body[StateBlobMarkerOffset] = StateBlobMarker;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(
            body.AsSpan(StateBlobValueOffset, sizeof(ushort)),
            JoinStateValue);
        return body;
    }

    /// <summary>Body length of the state blob.</summary>
    public const int StateBlobLength = 22;

    /// <summary>Offset of the fixed marker inside the state blob.</summary>
    private const int StateBlobMarkerOffset = 18;

    /// <summary>Value the state blob's fixed marker carries in every capture.</summary>
    private const byte StateBlobMarker = 0x40;

    /// <summary>Offset of the blob's only varying field, a sixteen-bit value.</summary>
    private const int StateBlobValueOffset = 20;

    /// <summary>
    /// The value the blob carries in the captured burst. Later blobs in the same
    /// round carry 100 for the most part, so this one is the join-time value
    /// rather than a constant.
    /// </summary>
    private const ushort JoinStateValue = 50;
}
