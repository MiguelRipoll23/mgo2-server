using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Builds the burst the recorded host sends once, about two and a half seconds
/// after it has answered a joiner with the roster.
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
/// record, carries the host's own payload under
/// <see cref="RoomRosterService.HostEntryType"/>, then the payloads under
/// <see cref="UdpCommandConstants.PlayerProfile"/> and closes with a
/// <see cref="UdpCommandConstants.GameStateBlob"/>. A seventh record, a second
/// one-byte control record, follows 300 µs later on its own counter.
/// </para>
/// <para>
/// <b>What the payload bodies mean is unknown.</b> An earlier reading of them
/// was never confirmed, so they are named for their position and nothing more
/// and are called <c>unknown</c> throughout. The bodies below are the bytes the
/// recorded host actually wrote, copied verbatim, because the alternative —
/// the leading tag and the slot followed by zeros — is a body the peer has
/// never been shown to accept and a run it can parse to nothing. They are one
/// capture's values: every peer is sent the same ones, which is what matching
/// the recorded host means here and is not per-player data.
/// </para>
/// <para>
/// The captured burst holds <i>three</i> payloads under <c>0x1001</c> — a
/// fifty-byte one in slot <c>0x01</c> and two fifty-four-byte ones in slot
/// <c>0x15</c>, byte for byte identical, for its single joiner. The two
/// fifty-four-byte records are written once rather than per joiner because the
/// capture cannot say whether they belong to the joiner, to the host, or to the
/// round; sending them at all is what the recorded host does.
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
public sealed class PostJoinBurstService(RoomRosterService roster)
{
    /// <summary>
    /// Leading byte of every <c>0b</c> payload body, whatever record type carries
    /// it, and the byte that tells a payload apart from a <c>07 48</c> roster
    /// entry on the same <c>0x9001</c>/<c>0x1001</c> record.
    /// </summary>
    public const byte ThingPayloadTag = 0x0b;

    /// <summary>Slot byte on the host's own payload.</summary>
    public const byte HostPayloadSlot = 0x00;

    /// <summary>Body length of the host's own payload, as measured.</summary>
    public const int HostPayloadLength = 161;

    /// <summary>Slot byte on a joining player's payload.</summary>
    public const byte PlayerPayloadSlot = 0x01;

    /// <summary>Body length of a joining player's payload, as measured.</summary>
    public const int PlayerPayloadLength = 50;

    /// <summary>Slot byte on the two payloads of unknown purpose.</summary>
    public const byte UnknownPayloadSlot = 0x15;

    /// <summary>Body length of each of the two unknown payloads, as measured.</summary>
    public const int UnknownPayloadLength = 54;

    /// <summary>Control byte the burst opens with.</summary>
    public const byte OpeningControlByte = 0x24;

    /// <summary>Control byte of the record that follows the burst.</summary>
    public const byte ClosingControlByte = 0x02;

    /// <summary>
    /// The host's own payload, copied from the capture at t+4.9929.
    /// </summary>
    private const string HostPayloadHex =
        "0b00ff27feeeffff3f03000000000000000000000000000000000501000705000a0300060700"
        + "0e01000f04000301001102000210000901000c05000404000101000806000d05000000000fb4"
        + "0002032a620700b4000c02010000280000002800020100020a2c01a401b4002c011c022c012c"
        + "01f00003d0a0ac00242200b400341700002c012c010202040202020400020402022863000300"
        + "01017f7ffff3e50e01";

    /// <summary>
    /// The fifty-byte payload a joining player is announced under.
    /// </summary>
    private const string PlayerPayloadHex =
        "0b01020000000000000000000000000000000000005002feffc2051e452f573967680e0e000e0e00000b16000e0e070f0000";

    /// <summary>
    /// The fifty-four-byte payload written twice, in slot <c>0x15</c>. What it
    /// carries is not established, so it is named for its slot and not for
    /// anything the capture does not show.
    /// </summary>
    private const string UnknownPayloadHex =
        "0b15020000000000000000000000000000007800005002ff01030201ff78051e452f573967680e0e000e0e00000b16000e0e070f0000";

    /// <summary>
    /// Builds the burst in the order the recorded host sends it: the opening
    /// control byte, the host's own payload, one payload per joining player,
    /// the two unknown payloads, and the state blob that closes it.
    /// </summary>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildBurst()
    {
        // The fourth byte continues the roster run's numbering instead of
        // restarting it. At the join the run reads 0 for its head and the host's
        // own entry, 1 for the player and 2 for the close, and the burst that
        // follows reads 3, 4, 5 and 6 — the host's own payload, the player's, and
        // the two unknown payloads, in that order. Writing zero on all four, as
        // this did, tells the peer the same sequence four times over.
        var ordinal = roster.NextRunOrdinal;
        var burst = new List<RosterRecord>
        {
            new(UdpCommandConstants.InGameControl, [OpeningControlByte]),
            new(RoomRosterService.HostEntryType, HostPayload(), ordinal++),
        };

        for (var joiner = 0; joiner < roster.JoinerCount; joiner++)
        {
            burst.Add(new(
                UdpCommandConstants.PlayerProfile,
                PlayerPayload(),
                ordinal++));
        }

        // The capture writes these two once, byte for byte identical, and does
        // not repeat them for the peers that join later in the round.
        burst.Add(new(UdpCommandConstants.PlayerProfile, UnknownPayload(), ordinal++));
        burst.Add(new(UdpCommandConstants.PlayerProfile, UnknownPayload(), ordinal));

        burst.Add(new(UdpCommandConstants.GameStateBlob, BuildStateBlob()));
        return burst;
    }

    /// <summary>
    /// Builds the single one-byte control record the recorded host sends on the
    /// counter after the burst, 300 µs behind it.
    /// </summary>
    /// <returns>The record, ready to be sent.</returns>
    public static RosterRecord BuildFollowUp() =>
        new(UdpCommandConstants.InGameControl, [ClosingControlByte]);

    /// <summary>The host's own payload body, as the capture wrote it.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] HostPayload() => Convert.FromHexString(HostPayloadHex);

    /// <summary>A joining player's payload body, as the capture wrote it.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] PlayerPayload() => Convert.FromHexString(PlayerPayloadHex);

    /// <summary>
    /// The fifty-four-byte payload of unknown purpose, as the capture wrote it.
    /// </summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] UnknownPayload() => Convert.FromHexString(UnknownPayloadHex);

    /// <summary>
    /// Builds a <c>0b</c> payload body: the tag, the slot, and zeros for the
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
    public static byte[] BuildPayload(byte slot, int length)
    {
        if (length < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                "A 0b payload carries its tag and its slot, so it cannot be shorter than two bytes.");
        }

        var body = new byte[length];
        body[0] = ThingPayloadTag;
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
    /// this burst carries. What it counts is unknown.
    /// </remarks>
    /// <returns>The record body.</returns>
    public static byte[] BuildStateBlob()
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