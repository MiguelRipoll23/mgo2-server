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
/// <see cref="RoomRosterService.HostEntryType"/> and one payload per joining
/// player under <see cref="UdpCommandConstants.PlayerProfile"/>, and closes with
/// a <see cref="UdpCommandConstants.GameStateBlob"/>. A seventh record, a second
/// one-byte control record, follows 300 µs later on its own counter.
/// </para>
/// <para>
/// <b>The payload bodies are the weak part of this.</b> They open <c>0b
/// &lt;slot&gt;</c> and run 50 to 161 bytes, and they are built here as the tag,
/// the slot and zeros. The capture's own bodies are one specific character's
/// equipment, skills and settings, and they are not copied here: they belong to
/// a character this server knows nothing about, and sending them to any other
/// player would be wrong as well as a redaction problem. What that means on the
/// wire is **[U]** — the shape is measured and the content is not.
/// </para>
/// <para>
/// The burst also does not map one record onto one player the way the roster
/// run does. The captured burst holds <i>three</i> payloads under
/// <c>0x1001</c> — slots <c>0x01</c>, <c>0x15</c> and <c>0x15</c> again — for its
/// single joiner, so one payload per joining player is a reading of the shape,
/// not a measurement of it. The two identical <c>0x15</c> payloads in
/// particular are unexplained.
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

    /// <summary>Control byte the burst opens with.</summary>
    public const byte OpeningControlByte = 0x24;

    /// <summary>Control byte of the record that follows the burst.</summary>
    public const byte ClosingControlByte = 0x02;

    /// <summary>
    /// Builds the burst in the order the recorded host sends it: the opening
    /// control byte, the host's payload, one payload per joining player, and the
    /// state blob that closes it.
    /// </summary>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildBurst()
    {
        var burst = new List<RosterRecord>
        {
            new(UdpCommandConstants.InGameControl, [OpeningControlByte]),
            new(RoomRosterService.HostEntryType, BuildPayload(HostPayloadSlot, HostPayloadLength)),
        };

        for (var joiner = 0; joiner < roster.JoinerCount; joiner++)
        {
            burst.Add(new(
                UdpCommandConstants.PlayerProfile,
                BuildPayload(PlayerPayloadSlot, PlayerPayloadLength)));
        }

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

    /// <summary>
    /// Builds a <c>0b</c> payload body: the tag, the slot, and zeros for the
    /// rest of the measured length.
    /// </summary>
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
    /// this burst carries. What it counts is **[U]**.
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