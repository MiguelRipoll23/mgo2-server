namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The state-<c>0x00</c> transfers a reference host writes: the room's phase, the
/// room's population, and the two records a round's open puts in front of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A body is its descriptor read out, not a pinned byte array.</b> It is
/// <c>[0x0b][state 0x00]</c>, a presence map of one bit per field
/// (<c>ceil(55 / 8) = 7</c> bytes, bit <c>i</c> = field <c>i</c>, least
/// significant bit first) and then every present field's bytes in field order.
/// The client's field table at <c>0x1229b80</c> gives each field its offset and
/// width, and <c>P2P_ReadOperationStateField</c> moves exactly that many bytes, so
/// <see cref="RoundStateField"/> carries the entries this host writes and
/// <see cref="TransferBody"/> builds the record from them.
/// </para>
/// <para>
/// <b>The values are big-endian.</b> Unlike the operations stream beside it, whose
/// clock is little-endian (<c>60 09</c> = 2 400), this block is written as the
/// client's object holds it: <c>0x0128</c> travels as <c>01 28</c>
/// (<c>docs/analysis/rotation-payload-corpus.md</c> §5a).
/// </para>
/// </remarks>
public static class RoundStateBodiesUtils
{
    /// <summary>Body length of a transfer that carries one one-byte field.</summary>
    public const int PayloadLength = 10;

    /// <summary>Type byte the body opens with: the settings payload type.</summary>
    public const byte PayloadType = PlayerSettingsBurstService.SettingsPayloadType;

    /// <summary>Offset of the state index the transfer names: state <c>0x00</c>.</summary>
    public const int StateIndexOffset = 1;

    /// <summary>Offset of the state's presence map.</summary>
    public const int MapOffset = 2;

    /// <summary>Length of that map: one bit per field, <c>ceil(55 / 8)</c> bytes.</summary>
    public const int MapLength = 7;

    /// <summary>Offset of the first carried field's bytes, behind the map.</summary>
    public const int ValueOffset = MapOffset + MapLength;

    /// <summary>
    /// The state field the join's transfer carries: one byte at state offset
    /// <c>83</c>, whose map bit is the <c>0x04</c> of the map's second byte.
    /// </summary>
    public const int PlayerCountFieldIndex = 10;

    /// <summary>
    /// The state field the room's own transitions carry: one byte at state offset
    /// <c>0</c>, whose map bit is the <c>0x01</c> of the map's first byte.
    /// </summary>
    public const int RoomStateFieldIndex = 0;

    /// <summary>
    /// The value a round's open holds field 0 at: <c>7</c>, the client's other
    /// retry value beside <c>0</c> (<c>0x288160</c>:
    /// <c>if (field0 == 0 || field0 == 7) retry</c>). It is not a
    /// <see cref="RoomState"/>: no room body carries it, and it exists only on the
    /// transfer a round's open writes ahead of the phase.
    /// </summary>
    public const int PlacementHoldValue = 7;

    /// <summary>
    /// Field 19 at a round's open: <c>0x0200</c>, in the two-byte field's high
    /// byte. The other two values the capture writes are <c>0x0100</c>, on the
    /// frame that carries a score award (t+41.240 and t+148.899, each in the same
    /// frame as the <c>0x26 &lt;slot&gt; &lt;points&gt;</c> record and the peers' slot
    /// states), and <c>0x0000</c>, on the frame that closes the round (t+74.067,
    /// t+163.096).
    /// </summary>
    public const int RoundOpenStateValue = 0x0200;

    /// <summary>
    /// Field 0 at a round's result: <c>6</c>. Every result transfer of
    /// <c>rounded-ended-by-reaching-tickets-limit</c> states it — t+74.571 behind
    /// round 1 and t+163.597 behind round 2 — and no room body carries the value,
    /// so it is the result screen's phase and not a <see cref="RoomState"/>.
    /// </summary>
    public const int RoundEndStateValue = 6;

    /// <summary>
    /// The state-<c>0x00</c> fields this host writes, by the index the presence map
    /// counts in.
    /// </summary>
    /// <remarks>
    /// Each member names its entry in the client's field table: the object offset,
    /// the width the client moves, and the publication flags at <c>+6</c>
    /// (<c>0x0025</c> = publish on change; <c>0x0064</c> = publish always). The
    /// table's other fifty entries are the room's settings, which this host writes
    /// as the 161-byte room body rather than as transfers.
    /// </remarks>
    public enum RoundStateField : byte
    {
        /// <summary>Object <c>+0x00</c>, <c>u8</c>, flags <c>0x0025</c>: the room's phase.</summary>
        RoomState = 0,

        /// <summary>Object <c>+0x53</c>, <c>u8</c>, flags <c>0x0025</c>: the room's population.</summary>
        Population = 10,

        /// <summary>
        /// Object <c>+0x58</c>, <c>u8</c>, flags <c>0x0064</c>: written <c>1</c> by
        /// a round that follows another of its own stage and left out of the map by
        /// a stage's first round.
        /// </summary>
        FollowsAnotherRound = 13,

        /// <summary>
        /// Object <c>+0x5c</c>, <c>u16</c>, flags <c>0x0025</c>: a two-byte state
        /// whose high byte the capture writes <c>2</c> at an open, <c>1</c> on a
        /// score-award frame and <c>0</c> at the close. The schema names no field and
        /// no call site of the setter passes its offset, so the name is the shape and
        /// the values are what the capture shows.
        /// </summary>
        RoundState = 19,

        /// <summary>
        /// Object <c>+0x5e</c>, <c>u8</c>, flags <c>0x0025</c>: the round's own
        /// number, written beside field 0 at a result (<c>06 01</c> behind round 1,
        /// <c>06 02</c> behind round 2, t+74.571 and t+163.597).
        /// </summary>
        RoundNumber = 20,

        /// <summary>
        /// Object <c>+0xa1</c>, <c>u8</c>, flags <c>0x0025</c>: the rounds played
        /// since the process started — <c>1</c> at round 1's open, <c>2</c> at round
        /// 2's, <c>3</c> at the next stage's briefing.
        /// </summary>
        RoundsPlayed = 51,
    }

    /// <summary>
    /// Builds one state-<c>0x00</c> transfer from the fields it carries.
    /// </summary>
    /// <remarks>
    /// <b>The map and the values are one statement.</b> A field listed with a value
    /// is in the map and written in field order; a field listed with <c>null</c> is
    /// left out, which is how a stage's first round says nothing about field 13
    /// rather than writing a zero for it. Each value is written big-endian at the
    /// width its table entry declares, and a value that does not fit is refused
    /// rather than truncated.
    /// </remarks>
    /// <param name="fields">The fields to carry, with their values.</param>
    /// <returns>The record body.</returns>
    public static byte[] TransferBody(params (RoundStateField Field, int? Value)[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var present = fields
            .Where(field => field.Value is not null)
            .OrderBy(field => (byte)field.Field)
            .ToArray();
        if (present.Length == 0)
        {
            throw new ArgumentException("A state transfer carries at least one field", nameof(fields));
        }

        var length = ValueOffset;
        foreach (var (field, _) in present)
        {
            length += WidthOf(field);
        }

        var body = new byte[length];
        body[0] = PayloadType;
        body[StateIndexOffset] = 0;

        var offset = ValueOffset;
        var previous = -1;
        foreach (var (field, value) in present)
        {
            var index = (int)field;
            if (index == previous)
            {
                throw new ArgumentException($"Field {index} is listed twice", nameof(fields));
            }

            previous = index;
            body[MapOffset + (index / 8)] |= (byte)(1 << (index % 8));

            var width = WidthOf(field);
            var written = value!.Value;
            if (written < 0 || written >= 1 << (8 * width))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fields),
                    written,
                    $"Field {index} holds {width} byte(s)");
            }

            for (var byteIndex = width - 1; byteIndex >= 0; byteIndex--)
            {
                body[offset++] = (byte)(written >> (8 * byteIndex));
            }
        }

        return body;
    }

    /// <summary>
    /// The width the client's field table gives a field, which is the number of
    /// bytes the transfer carries for it.
    /// </summary>
    /// <param name="field">Field to measure.</param>
    /// <returns>The field's width in bytes.</returns>
    private static int WidthOf(RoundStateField field) => field switch
    {
        RoundStateField.RoomState => 1,
        RoundStateField.Population => 1,
        RoundStateField.FollowsAnotherRound => 1,
        RoundStateField.RoundState => 2,
        RoundStateField.RoundsPlayed => 1,
        _ => throw new ArgumentOutOfRangeException(
            nameof(field),
            field,
            "No width is known for this field"),
    };

    /// <summary>
    /// The two state-0 transfers a round's open writes in front of the room's own
    /// state record, in send order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both are measured on the round's own stream.</b>
    /// <c>rounded-ended-by-reaching-tickets-limit</c> writes them at t+96.112 and
    /// t+96.482, between the lobby-slot confirmation (<c>81 01</c>, t+95.763) and
    /// the pair the client answers (<c>85 02</c>/<c>86 02</c>, t+96.497); the
    /// round's room state (<c>0b 00 01 00 … 01</c>) closes the same frame as the
    /// second.
    /// </para>
    /// <para>
    /// The first is the hold: field 0 at <see cref="PlacementHoldValue"/>, field 13
    /// when the round follows another of its stage, and field 51 at the rounds
    /// played — a round that follows another opens as the twelve-byte <c>07 01 02</c>.
    /// The second is the release: field 0 back at <c>0</c> and field 19 at
    /// <see cref="RoundOpenStateValue"/>, which is the twelve bytes <c>00 02 00</c>
    /// after the map. The room state behind it then states the phase the round
    /// plays on.
    /// </para>
    /// <para>
    /// <b>A room's first round opens on the two plain phase values and no
    /// hold.</b> <c>briefing-then-solo-game.pcapng</c> opens its round at
    /// t+146.007 with two ten-byte records — <c>0b 00 01 00 00 00 00 00 00 00</c>
    /// then <c>0b 00 01 00 00 00 00 00 00 01</c>, field 0 at <see cref="RoomState.NotReady"/>
    /// and then at <see cref="RoomState.Transition"/> — and no field 51 at all,
    /// because that room has played nothing. The hold pair belongs to the rounds
    /// behind it, which is where the capture's <c>07 01 02</c> is read from.
    /// </para>
    /// </remarks>
    /// <param name="roundNumber">The round the open is writing, its stage's first being 1.</param>
    /// <returns>The two record bodies, in the order the capture writes them.</returns>
    public static IReadOnlyList<byte[]> RoundOpenTransfers(int roundNumber)
    {
        if (roundNumber <= 1)
        {
            return
            [
                TransferBody((RoundStateField.RoomState, (int)RoomState.NotReady)),
                TransferBody((RoundStateField.RoomState, (int)RoomState.Transition)),
            ];
        }

        var played = Math.Clamp(roundNumber, 0, byte.MaxValue);
        return
        [
            TransferBody(
                (RoundStateField.RoomState, PlacementHoldValue),
                (RoundStateField.FollowsAnotherRound, 1),
                (RoundStateField.RoundsPlayed, played)),
            TransferBody(
                (RoundStateField.RoomState, (int)RoomState.NotReady),
                (RoundStateField.RoundState, RoundOpenStateValue)),
        ];
    }

    /// <summary>
    /// The two state-0 transfers a round's close leaves behind it, in send order.
    /// </summary>
    /// <remarks>
    /// <b>The first clears field 19, the second is the result's own.</b>
    /// <c>rounded-ended-by-reaching-tickets-limit</c> writes field 19 at
    /// <c>0x0000</c> in the frame that closes the round (t+74.067, t+163.096) and
    /// then, beside the result write, field 0 at <see cref="RoundEndStateValue"/> and
    /// field 20 at the round (t+74.571, t+163.597). The open's pair is
    /// <see cref="RoundOpenTransfers"/>, which states field 19 again at
    /// <see cref="RoundOpenStateValue"/>; the <c>0x0100</c> the capture also writes
    /// belongs to its score-award frames and not to this boundary.
    /// </remarks>
    /// <param name="roundNumber">The round that closed.</param>
    /// <returns>The two record bodies, in the order the capture writes them.</returns>
    public static IReadOnlyList<byte[]> RoundEndTransfers(int roundNumber) =>
    [
        TransferBody((RoundStateField.RoundState, 0)),
        TransferBody(
            (RoundStateField.RoomState, RoundEndStateValue),
            (RoundStateField.RoundNumber, Math.Clamp(roundNumber, 0, byte.MaxValue))),
    ];

    /// <summary>
    /// The state transfer a room's transition writes: state <c>0x00</c>, field
    /// <c>0</c> at the room's state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The client gates its join on this field, and it is a room state and not
    /// a round.</b> The state machine at <c>0x2882B4</c> reads field 0 through
    /// <c>0x27E480</c> and treats <c>0</c> and <c>7</c> as "wait and ask again":
    /// any other value sends its lobby-slot echo (<c>82</c>/<c>84</c>) and moves
    /// to the next state. The values the captures carry are <see cref="RoomState"/>'s
    /// three measured phases and three that keep their wire value behind an
    /// unknown name, and the same value repeats while its phase lasts — which is
    /// what a round number could not do.
    /// </para>
    /// <para>
    /// <b>[I] for the field's name.</b> The schema names no field; the values are
    /// what the room body at <c>0x09</c> states and what the two writers here are
    /// measured to agree on.
    /// </para>
    /// </remarks>
    /// <param name="state">The room state to state.</param>
    /// <returns>The record body.</returns>
    public static byte[] RoomStateBody(RoomState state) =>
        TransferBody((RoundStateField.RoomState, (int)state));

    /// <summary>
    /// The round-state transfer a join writes: state <c>0x00</c>, field <c>10</c>
    /// at the room's population.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a state transfer, not a settings body.</b> The ten bytes are the
    /// dispatch key the client's registry is keyed by (<c>0x0b</c>), the state index
    /// (<c>0x00</c>, the round's own state), the state's presence map — 55 fields, so
    /// <c>ceil(55 / 8) = 7</c> bytes — and then one value for the one bit the map
    /// sets. That is <c>1 + 1 + 7 + 1 = 10</c>, and it is the whole record:
    /// `mgo2-game-started-on-briefing` t+93.147 and t+189.909, `mgo2-game2`
    /// t+120.811 and the red-team capture all write
    /// <c>0b 00 00 04 00 00 00 00 00 NN</c>, where the <c>0x04</c> at <c>+3</c> is
    /// bit 2 of the map's second byte — field **10** at state offset <c>83</c> — and
    /// <c>NN</c> is its one-byte value. All 188 copies in the fourteen captures fit
    /// that parse with exactly one field
    /// (<c>docs/analysis/join-stall-after-mode2.md</c> §34).
    /// </para>
    /// <para>
    /// <b>The value is the room's population, the host counted.</b> It is not the
    /// answered peer's slot byte, which is what this record was first read as: the
    /// same captures hand out slots far above the values they write here — `game1`
    /// places a joiner at key <c>0x14</c>, and its join-time value is <c>03</c> —
    /// while the corpus' values are <c>02</c> to <c>09</c> and not one is higher.
    /// What the values track is the number of players: `started-on-briefing` writes
    /// <c>02</c>, <c>03</c>, <c>04</c>, <c>05</c> and <c>06</c> as one player joins
    /// after another, and `game1` alternates <c>03</c> and <c>04</c> as its room
    /// loses and regains a player. The red-team capture settles it: its t+106.610
    /// join publishes the entry for key <c>8</c> and the loadout <c>0b 09</c>, and
    /// the transfer in the same datagram reads <c>07</c> — a population of seven
    /// players in a room whose keys have already run to eight. **[I]** for the name:
    /// the schema does not name the field, and no call site of <c>0x27E480</c> in the
    /// image passes the field's offset, so the consumer that would name it is not
    /// read; the count is the reading every captured value fits.
    /// </para>
    /// <para>
    /// <b>The reference does not always write it.</b> 7 of the 9 reference gameplay
    /// captures hold the record, and one join (`mgo2-game-failed-memoryviolation`)
    /// writes only the field-0 variant — so its absence is not a stall on its own.
    /// This host wrote none of it before this record existed, and writes it on every
    /// join because its value is the room's and not the peer's: a burst that could
    /// not name the answering peer can still count the room.
    /// </para>
    /// </remarks>
    /// <param name="playerCount">
    /// How many players the room holds, the host counted — the same figure the
    /// client's own player list has once this burst is in.
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] JoinPlayerCountBody(byte playerCount) =>
        TransferBody((RoundStateField.Population, playerCount));
}
