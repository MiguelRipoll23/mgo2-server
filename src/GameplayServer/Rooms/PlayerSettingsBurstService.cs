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
/// record, carries the room's own settings and the payloads under
/// <see cref="UdpCommandConstants.PlayerProfile"/> and closes with a
/// <see cref="UdpCommandConstants.GameStateBlob"/>. A seventh record, a second
/// one-byte control record, follows 300 µs later on its own counter.
///
/// The room's population is not in it: that state transfer closes the roster run
/// (<see cref="RoomRosterFramesUtils.BuildRun"/>), which is where the reference
/// host writes it, and a burst that repeated it gave a joiner two counts before
/// it had reported anything.
/// </para>
/// <para>
/// <b>The payload bodies share the replay parser's settings discriminator.</b> Every
/// one of them opens on <c>0x0b</c>, which is the replay's
/// <c>TYPE_SETTINGS</c> discriminator, and its byte <c>+1</c> is the slot the
/// record belongs to: slot <c>0x00</c> with at least 74 bytes carries the
/// room's settings block, whose native layout has sixteen rotation triples at
/// body <c>+26</c> — map id, mode, and an unresolved byte; a slot of
/// <c>0x01</c> or more with a length in 50…62 is a player's loadout, carrying
/// its team byte, its appearance and its skills (<c>docs/analysis/replay-console/rpdt.js</c>,
/// laid out in <c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c> and mapped in
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>). The recorded room's 161-byte body
/// reads as maps 5, 7, 10, 6, 14, 15, 3, 17, 2, 9, 12, 4, 1, 8, 13 in modes 1, 5,
/// 3, 7, 1, 4, 1, 2, 16, 1, 5, 4, 1, 6, 5 — fifteen configured rounds — and the
/// joiner's 50-byte body as its appearance.
/// </para>
/// <para>
/// <b>The rotation and the loadouts are the room's.</b> The room row and the host
/// character's saved settings say which rounds the room plays, so the triples are
/// written from the room's own rotation (<see cref="RoomRotationBody"/>) and not
/// from the recorded host's rotation: a dedicated host whose room names one map was
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
    public const int RoomRotationPayloadLength = RoomRotationPayloadUtils.PayloadLength;

    /// <summary>
    /// Control byte the burst opens with: <c>0x06</c>, the value the capture whose
    /// briefing appears writes on the control record ahead of its settings burst
    /// (`briefing-then-solo-game` t+4.71), and the value the joining pass answers
    /// with. It was <c>0x28</c>'s neighbour <c>0x24</c> — the byte two other
    /// captures use for a joining pass — and the capture that came back with no
    /// briefing at all (`myserver.pcapng`, 2026-11-03) carried <c>24</c>, <c>02</c>
    /// and <c>03</c> where this one carries <c>06</c> and <c>08</c>.
    /// </summary>
    public const byte OpeningControlByte = 0x06;

    /// <summary>
    /// Control byte of the record that follows the burst: the second value
    /// `briefing-then-solo-game` writes in its briefing phase, <c>0x08</c>.
    /// </summary>
    public const byte ClosingControlByte = 0x08;

    /// <summary>
    /// Offset of the rotation's first triple in the room's settings body
    /// (<c>0x1a</c>).
    /// </summary>
    public const int RotationOffset = 0x1a;

    /// <summary>Length of one rotation triple: the map, the rule and the flags byte.</summary>
    public const int RotationEntryLength = 3;

    /// <summary>
    /// Triples the body carries. The native field and room-details response both
    /// have sixteen slots, and this is a fixed stride rather than a count in the body.
    /// </summary>
    public const int RotationEntryCount = 16;

    /// <summary>
    /// Builds the burst in the order the recorded host sends it: the opening
    /// control byte, the round-state transfer, the room's rotation and the host's
    /// own settings header, one loadout per joining player, the two second
    /// loadouts, and the state blob that closes it.
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
    /// The slot byte (<c>key + 1</c>) of the player the burst is answering, which
    /// the closing state blob takes as its ordinal, or <c>0</c> for a peer the
    /// roster cannot name. Every capture numbers the blob with the joiner's own:
    /// `mgo2-started-briefing-then-solo-game` t+6.006 carries `4` and its request
    /// reads `0b 04`, `mgo2-game-started-on-briefing` t+5.996 carries `2` behind a
    /// `0b 02`, and `mgo2-game1` t+6.687 carries `0x15` for the joiner whose key is
    /// `0x14` — the blob is that player's statement about itself, on the same
    /// record the briefing's tick later writes one per player under
    /// (`docs/analysis/join-stall-after-mode2.md` §28).
    /// </param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    /// <param name="roomState">The room state.</param>
    /// <param name="currentRotationIndex">Slot of the round the room is playing.</param>
    /// <param name="playerCount">The room's population, host included.</param>
    public List<RosterRecord> BuildSettingsBurst(
        IReadOnlyList<MatchRotationEntry>? rotation,
        IReadOnlyList<LobbyLoadout>? loadouts,
        RoomState roomState,
        int currentRotationIndex,
        byte playerCount,
        byte answeringSlot = 0)
    {
        var rotationBody = rotation is null
            ? RecordedRotationBody()
            : RoomRotationBody(rotation, roomState, currentRotationIndex, playerCount);
        return BuildSettingsBurst(rotationBody, loadouts, answeringSlot);
    }

    /// <summary>
    /// Builds a settings burst with the current room state and configured rule timers.
    /// </summary>
    /// <param name="rotation">The room's rotation.</param>
    /// <param name="loadouts">The room's player loadouts.</param>
    /// <param name="roomState">The room state.</param>
    /// <param name="currentRotationIndex">The slot the native state selects.</param>
    /// <param name="playerCount">The room population, including the host.</param>
    /// <param name="ruleTimers">The configured seventeen-word timer bank.</param>
    /// <param name="answeringSlot">The answering peer's slot byte.</param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildSettingsBurst(
        IReadOnlyList<MatchRotationEntry> rotation,
        IReadOnlyList<LobbyLoadout>? loadouts,
        RoomState roomState,
        int currentRotationIndex,
        byte playerCount,
        IReadOnlyList<int> ruleTimers,
        byte answeringSlot = 0)
    {
        var rotationBody = RoomRotationPayloadUtils.Build(
            rotation,
            roomState,
            currentRotationIndex,
            playerCount,
            ruleTimers);
        return BuildSettingsBurst(rotationBody, loadouts, answeringSlot);
    }

    private List<RosterRecord> BuildSettingsBurst(
        byte[] rotationBody,
        IReadOnlyList<LobbyLoadout>? loadouts,
        byte answeringSlot)
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
            .. JoinPreamble(rotationBody),
        ];

        // One loadout per player of the room, the host's included. The recorded
        // burst of a two-player room carries three bodies — `mgo2-game1`
        // t+6.687 writes the fifty-byte one in the host's own slot `01` and the
        // fifty-four-byte one twice in its joiner's `15` — so the family names
        // the room's players and not only the peer being answered. Writing the
        // answering peer's alone leaves every other player of the room with no
        // appearance record at all, and the client's own voice plugin reports
        // `PL_PLG_VOICE_NET :: invalid voice id 0` for a player it has no
        // appearance for (docs/analysis/join-stall-after-mode2.md §85). A solo
        // capture still writes one, because its room holds one joiner besides
        // the host, and the host's own body is the one at slot `01` in both.
        if (loadouts is { Count: > 0 })
        {
            foreach (var loadout in loadouts.OrderBy(player => player.Slot))
            {
                burst.Add(new(
                    UdpCommandConstants.PlayerProfile,
                    BuildLoadoutBody(loadout),
                    roster.TakeRunOrdinal()));

                // **One record per seat, and no gear body beside it.** The burst here
                // wrote a loadout *and* a seventy-byte gear record for every player,
                // which `actions.pcapng` never writes at all: its `0x1001` lengths are
                // 57, 63, 72, 49, 50, 54, 25 and 161, and a seventy-byte body appears
                // nowhere in the round. Its burst is one body per seat — the fifteen
                // bots' at 49 or 50 bytes and the human's at 54 — so this writes the
                // loadout alone. A late-seated bot still carries its gear in its own
                // run (`BotRosterPublishService`), which is a different frame.
            }
        }
        else
        {
            burst.Add(new(
                UdpCommandConstants.PlayerProfile,
                AnsweringLoadoutBody(loadouts, answeringSlot),
                roster.TakeRunOrdinal()));
        }

        burst.Add(new(UdpCommandConstants.GameStateBlob, BuildClosingStateBlob(), answeringSlot));
        return burst;
    }

    /// <summary>
    /// Builds the single one-byte control record the recorded host sends on the
    /// counter after the burst, 300 µs behind it.
    /// </summary>
    /// <returns>The record, ready to be sent.</returns>
    public static RosterRecord BuildClosingControl() =>
        new(UdpCommandConstants.InGameControl, [ClosingControlByte]);

    /// <summary>The working capture's settings body, built field by field.</summary>
    /// <returns>The captured reference values.</returns>
    public static byte[] RecordedRotationBody() => RoomRotationPayloadUtils.BuildReferenceBody();

    /// <summary>
    /// A settings body with this room's rotation and the state of the room it is
    /// being written for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each triple is the parser's layout — map, then rule, then one more byte
    /// (<see cref="RotationOffset"/>) — and the room stores the same three values
    /// the other way round, as <see cref="MatchRotationEntry"/>: a rule, a map and
    /// the flags byte the client is handed in the room's details over TCP.
    /// </para>
    /// <para>
    /// <b>The third byte is the round's own options, and it is the byte that tells
    /// a joiner the round is a Drebin Points round.</b> <c>0x02</c> is Drebin
    /// Points, <c>0x04</c> Headshots Only and <c>0x00</c> neither
    /// (<see cref="Mgo2Server.Shared.Types.RoundOption"/>): a room with Drebin
    /// points switched on writes <c>0x02</c> on its configured triples and the same
    /// room's disabled capture writes <c>0x00</c> on all of its own. Writing the
    /// room's own byte therefore reproduces the triple the room was published with,
    /// where writing zero would tell a joiner an ordinary round.
    /// </para>
    /// <para>
    /// <b>Unused entries are zeroed, not repeated.</b> The field has sixteen
    /// slots, but a shorter room rotation only names its configured rounds. The
    /// remaining triples are zero, as they are in the lobby's rotation field and
    /// the working capture. A longer rotation contributes its first sixteen,
    /// because that is all this field holds.
    /// </para>
    /// </remarks>
    /// <param name="rotation">The room's rotation, in play order.</param>
    /// <param name="state">The room state.</param>
    /// <param name="currentRotationIndex">Slot of the round the room is playing.</param>
    /// <param name="playerCount">The room's population, host included.</param>
    /// <returns>The body.</returns>
    public static byte[] RoomRotationBody(
        IReadOnlyList<MatchRotationEntry> rotation,
        RoomState state,
        int currentRotationIndex,
        byte playerCount)
    {
        return RoomRotationPayloadUtils.BuildReferenceBody(
            rotation,
            state,
            currentRotationIndex,
            playerCount);
    }

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
