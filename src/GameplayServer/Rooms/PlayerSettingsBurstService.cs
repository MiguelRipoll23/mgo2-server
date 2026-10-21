using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Builds the settings records the recorded host sends once, about two and a
/// half seconds after it has answered a joiner with the roster: the room's
/// rotation, and one loadout per player of the room.
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
/// <c>0x01</c> or more with a length in 50…62 is a player's loadout, carrying
/// its team byte, its appearance and its skills (<c>docs/analysis/replay-console/rpdt.js</c>,
/// laid out in <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c> and mapped in
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>). The recorded room's 161-byte body
/// reads as maps 5, 7, 10, 6, 14, 15, 3, 17, 2, 9, 12, 4, 1, 8, 13 in modes 1, 5,
/// 3, 7, 1, 4, 1, 2, 16, 1, 5, 4, 1, 6, 5 — a fifteen-round rotation — and the
/// joiner's 50-byte body as its appearance.
/// </para>
/// <para>
/// <b>The rotation and the loadouts are the room's.</b> The room row and the host
/// character's saved settings say which rounds the room plays, so the triples are
/// written from the room's own rotation (<see cref="RoomRotationBody"/>) and not
/// from the recorded host's fifteen: a dedicated host whose room names one map was
/// telling every joiner the maps the capture's room played. The loadouts are read
/// the same way (<see cref="LobbyLoadoutService"/>): each player's record is
/// written from its own character's appearance and equipped skills, so a joiner is
/// shown the room's own faces and gear rather than the recorded host's. What
/// nothing names in a loadout body — the four bytes at <c>0x12</c> and the two
/// before the appearance block, both measured as per-player values — stays the
/// recorded body's; <see cref="LoadoutFramesUtils.TryWrite"/> carries the detail.
/// </para>
/// <para>
/// The captured burst holds <i>three</i> loadout payloads under <c>0x1001</c> — a
/// fifty-byte one in slot <c>0x01</c> and two fifty-four-byte ones in slot
/// <c>0x15</c>, byte for byte identical. They are the two players the room holds
/// at t+6.687: slot <c>0x01</c> is the host's own body — key zero is the host's
/// own entry in its player table — and <c>0x15</c> is its first joiner's, whose
/// key the same capture's roster records carry as <c>0x14</c>. So the burst names
/// the room's players, one record each, which is what this builder writes.
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
/// <param name="logger">Logger of this builder.</param>
public sealed partial class PlayerSettingsBurstService(
    RoomRosterService roster,
    ILogger<PlayerSettingsBurstService> logger)
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

    /// <summary>Control byte the burst opens with.</summary>
    public const byte OpeningControlByte = 0x24;

    /// <summary>Control byte of the record that follows the burst.</summary>
    public const byte ClosingControlByte = 0x02;

    /// <summary>
    /// Offset of the rotation's first triple in the room's settings body, which
    /// is where the replay parser reads it (<c>0x1a</c>).
    /// </summary>
    public const int RotationOffset = 0x1a;

    /// <summary>Length of one rotation triple: the map, the rule and the flags byte.</summary>
    public const int RotationEntryLength = 3;

    /// <summary>
    /// Triples the body carries. It is fifteen in both captures, and the field is
    /// a fixed stride rather than a count the body states.
    /// </summary>
    public const int RotationEntryCount = 15;

    /// <summary>
    /// The room's settings block as the capture at t+4.9929 wrote it: the fields
    /// around the rotation, and that room's own fifteen triples at body
    /// <c>+26</c>.
    /// </summary>
    /// <remarks>
    /// It is the base the built body starts from, because the 26 bytes a triple
    /// sits behind and the 90 after them are **[U]**
    /// (<c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c>): the recorded bytes are kept
    /// and only the triples the parser names are written over. It is also the last
    /// resort when the room cannot be read — a joiner still needs a body, and the
    /// caller logs which of the two it sent.
    /// </remarks>
    private const string RoomRotationPayloadHex =
        "0b00ff27feeeffff3f03000000000000000000000000000000000501000705000a0300060700"
        + "0e01000f04000301001102000210000901000c05000404000101000806000d05000000000fb4"
        + "0002032a620700b4000c02010000280000002800020100020a2c01a401b4002c011c022c012c"
        + "01f00003d0a0ac00242200b400341700002c012c010202040202020400020402022863000300"
        + "01017f7ffff3e50e01";

    /// <summary>
    /// Builds the burst in the order the recorded host sends it: the opening
    /// control byte, the joining player's round-state transfer, the room's
    /// rotation and the host's own settings header, one loadout per joining
    /// player, the two second loadouts, and the state blob that closes it.
    /// </summary>
    /// <param name="rotation">
    /// The room's own rotation, or <c>null</c> when the room could not be read, in
    /// which case the recorded room's body is written instead. The choice is the
    /// caller's because it is the caller that can say which it was, and a joiner
    /// told the wrong rotation is worse than one told a rotation this host can
    /// name in its log.
    /// </param>
    /// <param name="loadouts">
    /// The room's own players, each with its own appearance and skills
    /// (<see cref="LobbyLoadoutService"/>). Empty or <c>null</c> leaves the
    /// recorded host's loadout bodies in the burst, which is what a room that
    /// could not be read gets.
    /// </param>
    /// <param name="answeringSlot">
    /// The slot byte (<c>key + 1</c>) of the player the burst is answering, carried
    /// as the closing state blob's fourth byte, or <c>0</c> for a peer the roster
    /// cannot name. Every capture writes the joiner's own there:
    /// `mgo2-started-briefing-then-solo-game` t+6.006 carries `4` and its request
    /// reads `0b 04`, `mgo2-game-started-on-briefing` t+5.996 carries `2` behind a
    /// `0b 02`, and `mgo2-game1` t+6.687 carries `0x15` for the joiner whose key is
    /// `0x14` — the blob is that player's statement about itself, on the same
    /// record the briefing's tick later writes one per player under
    /// (`docs/analysis/join-stall-after-mode2.md` §28).
    /// </param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildSettingsBurst(
        IReadOnlyList<MatchRotationEntry>? rotation,
        IReadOnlyList<LobbyLoadout>? loadouts,
        byte answeringSlot = 0)
    {
        // The fourth byte continues the roster run's numbering instead of
        // restarting it, and it comes from the one counter every record of the
        // roster identifier consumes: at the join the run reads 0 for its head
        // and the host's own entry, 1 for the player and 2 for the close, and the
        // burst that follows takes the values after them in the order it writes:
        // the rotation, the host's header, then a loadout per player and the two
        // second loadouts (<see cref="RoomRosterService.TakeRunOrdinal"/>).
        // Writing zero on all of them, as this did, tells the peer the same
        // sequence over and over.
        List<RosterRecord> burst =
        [
            new(UdpCommandConstants.InGameControl, [OpeningControlByte]),
            .. JoinPreamble(rotation, answeringSlot),
        ];

        if (loadouts is { Count: > 0 })
        {
            // The room's own players, the host first because its key is zero:
            // the captured burst names the host's own entry and the players', and
            // the slot a record carries is the key the roster handed out.
            foreach (var loadout in loadouts.OrderBy(loadout => loadout.Slot))
            {
                burst.Add(new(
                    UdpCommandConstants.PlayerProfile,
                    LoadoutBody(loadout),
                    roster.TakeRunOrdinal()));
            }
        }
        else
        {
            // The recorded host's own loadout, for a room whose players could not
            // be read: one body per joining player, and the two byte-identical
            // records behind them that the capture writes once.
            for (var joiner = 0; joiner < roster.JoinerCount; joiner++)
            {
                burst.Add(new(
                    LoadoutBodiesUtils.LoadoutRecordType,
                    LoadoutBodiesUtils.PlayerLoadout(),
                    roster.TakeRunOrdinal()));
            }

            burst.Add(new(
                LoadoutBodiesUtils.LoadoutRecordType,
                LoadoutBodiesUtils.SecondLoadout(),
                roster.TakeRunOrdinal()));
            burst.Add(new(
                LoadoutBodiesUtils.LoadoutRecordType,
                LoadoutBodiesUtils.SecondLoadout(),
                roster.TakeRunOrdinal()));
        }

        burst.Add(new(UdpCommandConstants.GameStateBlob, BuildClosingStateBlob(), answeringSlot));
        return burst;
    }

    /// <summary>
    /// One player's loadout body: its own character's fields written into the
    /// recorded body of the shape its skills have.
    /// </summary>
    /// <param name="loadout">The player as the room reads it.</param>
    /// <returns>The record body.</returns>
    private byte[] LoadoutBody(LobbyLoadout loadout)
    {
        var template = LoadoutBodiesUtils.RecordedLoadoutFor(loadout.Skills.Count);
        if (template is not null && LoadoutFramesUtils.TryWrite(loadout, template, out var body))
        {
            return body;
        }

        // A skill count no shape is written for. Every count from none to three
        // has one — the captured bodies for none, two and three, and the built
        // one for a single pair, which the captures never held — so this is a
        // player with four equipped skills, whose length the rule gives and no
        // capture confirms. A body written at the wrong length is one the peer
        // reads as nothing, and the decoder's own skill reader stops at three
        // pairs, so the player is still named, in its own slot, with the recorded
        // no-pairs body behind that one byte.
        logger.LogWarning(
            "Player in slot {Slot} has {SkillCount} equipped skill(s), which no loadout body is written for; "
            + "the recorded no-pairs body is written behind its own slot byte instead",
            loadout.Slot,
            loadout.Skills.Count);

        var fallback = LoadoutBodiesUtils.PlayerLoadout();
        fallback[LoadoutFramesUtils.SlotByteOffset] = (byte)(loadout.Slot + 1);
        return fallback;
    }

    /// <summary>
    /// Builds the single one-byte control record the recorded host sends on the
    /// counter after the burst, 300 µs behind it.
    /// </summary>
    /// <returns>The record, ready to be sent.</returns>
    public static RosterRecord BuildClosingControl() =>
        new(UdpCommandConstants.InGameControl, [ClosingControlByte]);

    /// <summary>The recorded room's settings body, as the capture wrote it.</summary>
    /// <returns>The recorded bytes.</returns>
    public static byte[] RecordedRotationBody() => Convert.FromHexString(RoomRotationPayloadHex);

    /// <summary>
    /// The room's own settings body: the recorded body with its fifteen rotation
    /// triples written from this room's rotation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each triple is the parser's layout — map, then rule, then one more byte
    /// (<see cref="RotationOffset"/>) — and the room stores the same three values
    /// the other way round, as <see cref="MatchRotationEntry"/>: a rule, a map and
    /// the flags byte the client is handed in the room's details over TCP.
    /// </para>
    /// <para>
    /// <b>The third byte is written from the room's own flags byte.</b> It is
    /// **[U]** in the payload's layout, and the measurement is: zero in fifteen of
    /// the sixteen triples the three captures hold, and <c>0x20</c> in the
    /// sixteenth — the value the rotation family's flags byte takes, and the only
    /// non-zero one this position was ever seen to hold. Writing the room's own
    /// byte therefore reproduces the triple the room was published with, where
    /// writing zero would put a value there that no row states.
    /// </para>
    /// <para>
    /// <b>A rotation shorter than the field is walked round.</b> Entry <c>i</c> is
    /// the room's entry <c>i</c> modulo its count, so all fifteen name a real map
    /// and rule rather than the map <c>0</c> the client reads as None; a room whose
    /// rotation is longer contributes its first fifteen, because that is all the
    /// field holds.
    /// </para>
    /// </remarks>
    /// <param name="rotation">The room's rotation, in play order.</param>
    /// <returns>The body.</returns>
    public static byte[] RoomRotationBody(IReadOnlyList<MatchRotationEntry> rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);
        if (rotation.Count == 0)
        {
            throw new ArgumentException(
                "A room's rotation states at least one round, so an empty one has no map or rule to write.",
                nameof(rotation));
        }

        var body = RecordedRotationBody();
        for (var slot = 0; slot < RotationEntryCount; slot++)
        {
            var entry = rotation[slot % rotation.Count];
            var offset = RotationOffset + (slot * RotationEntryLength);
            body[offset] = Component(nameof(entry.Map), entry.Map);
            body[offset + 1] = Component(nameof(entry.Rule), entry.Rule);
            body[offset + 2] = Component(nameof(entry.Flags), entry.Flags);
        }

        return body;
    }

    /// <summary>
    /// One byte of a rotation triple, refused when the room's row does not state
    /// one: a map, a rule and a flags byte are one byte each on the wire, so a
    /// value outside that is a row this server cannot express rather than a value
    /// to truncate onto the wrong map.
    /// </summary>
    /// <param name="field">Component being written, for the exception.</param>
    /// <param name="value">The row's value.</param>
    /// <returns>The value as a byte.</returns>
    private static byte Component(string field, int value) =>
        value is >= byte.MinValue and <= byte.MaxValue
            ? (byte)value
            : throw new ArgumentOutOfRangeException(field, value, "A rotation component is one byte.");

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
    /// Builds the state blob the burst closes with: a presence map with bit 150
    /// set, then that field's one byte.
    /// </summary>
    /// <remarks>
    /// <b>It is a one-field state transfer, not a body with a marker</b>: a presence
    /// map with field 150's bit set, then that field's one byte, which is the host's
    /// still-unmeasured figure for the peer — 50. The parse is
    /// `docs/analysis/join-stall-after-mode2.md` §28.7 and what the field measures is
    /// **[U]**.
    /// </remarks>
    /// <returns>The record body.</returns>
    public static byte[] BuildClosingStateBlob()
    {
        var body = new byte[StateBlobLength];
        body[ClosingStateFieldIndex / 8] = (byte)(1 << (ClosingStateFieldIndex % 8));
        body[ClosingStateMapLength] = JoinStateValue;
        return body;
    }

    /// <summary>Body length of the state blob: the map, then the field's byte.</summary>
    public const int StateBlobLength = 22;

    /// <summary>The slot state's field the blob carries, one byte at <c>state+287</c>.</summary>
    private const int ClosingStateFieldIndex = 150;

    /// <summary>Length of the presence map the blob opens with, <c>ceil(164 / 8)</c>.</summary>
    private const int ClosingStateMapLength = 21;

    /// <summary>The value the captured burst carries: the figure before any measurement.</summary>
    private const byte JoinStateValue = 50;
}
