namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The lobby-slot array handshake: the two arrays the host publishes, the
/// answer a client gives them, and the confirmation that moves the sequence on.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two arrays are the room's, not the recording's.</b> Each names the
/// players of the room it is published in, by their key in the room's 24-entry
/// player table: the occupancy array marks a slot taken, and the team array's
/// byte at that slot is the side its player is on. Read off the captures, the
/// host fills both in as its room fills. `mgo2-game1`, whose times are counted
/// from the handshake of the peer it answers (`UDP_GAME_CAPTURE.md` §4),
/// publishes the pair with every slot free in its run (t+46.4), then with slot
/// `0x14` taken on raw team <c>0</c> (t+73.4), then with `0x0f` beside it on raw
/// team <c>1</c> (t+185.2), then `0x04` (t+220.2) and `0x0b` (t+396.1) and
/// `0x06` (t+506.0) — the five joining players' session slots, which are the
/// keys §8.4 reads out of the same capture's roster records, with the host's own
/// slot 0 excepted. Slot 0 is the host's and stays the sentinel
/// <see cref="TeamArraySlotZero"/>, which is what makes its byte the one the
/// replay parser never reads.
/// </para>
/// <para>
/// Four record kinds and one loop, measured in both reference captures
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2):
/// </para>
/// <code>
/// host   0x9001/0x1001 26 B  85 NN   the team array
/// host   0x9001/0x1001 26 B  86 NN   the occupancy array
/// client 0x9001/0x1001  2 B  82 NN   the client answers
/// client 0x9001/0x1001  2 B  84 NN
/// host   0x1001         2 B  81 NN   the host confirms   &lt;- the only thing that advances
/// host                   26 B 85 NN+1 the pair again, under the next sequence
/// </code>
/// <para>
/// <b>The confirmation is what advances it.</b> A host that writes the pair and
/// never confirms gets one answer and then silence: the client only moves to the
/// next sequence after the host confirms the one before it. That is why the
/// confirmation is not merely an acknowledgement of the answer and why this file
/// carries the sequence rather than a constant.
/// </para>
/// <para>
/// The two answers travel as <c>82</c> and <c>84</c>, one below each array's own
/// identifier, and their byte at <c>[1]</c> is the sequence the client is
/// answering. Both are matched and neither is sent by this host: they are the
/// client's, and the record that answers them is <c>81</c>.
/// </para>
/// </remarks>
public static class TeamArrayFramesUtils
{
    /// <summary>Type byte of the team array, which opens its body.</summary>
    public const byte TeamArrayIdentifier = 0x85;

    /// <summary>Type byte of the occupancy array, which opens its body.</summary>
    public const byte OccupancyArrayIdentifier = 0x86;

    /// <summary>
    /// Body length of either array: the type byte, the sequence byte, and the
    /// twenty-four slot bytes behind them.
    /// </summary>
    public const int ArrayLength = 26;

    /// <summary>
    /// Slots either array carries, counting slot 0 — the host's own, held out of
    /// both so a player's key is the only thing the pair names.
    /// </summary>
    public const int ArraySlotCount = 24;

    /// <summary>Slot 0's byte in the team array: the host's own, never a team.</summary>
    public const byte TeamArraySlotZero = 0xfe;

    /// <summary>Team array's byte for a slot nobody in the room holds.</summary>
    public const byte TeamArrayVacant = 0xff;

    /// <summary>
    /// Team array's byte for a slot whose player the room's rows put on no side:
    /// <c>0xe0</c>, the value the recorded host wrote for slots it marked taken
    /// before it could name a side — `mgo2-game1` at t+449.0 for slot `0x0b` and
    /// at t+506.0 for `0x06`, which it gave sides to at t+517.5. The parser skips
    /// it exactly as it skips a free slot, so it says "a player is here and no
    /// side is known" rather than leaving the slot empty.
    /// </summary>
    public const byte TeamArrayNoTeam = 0xe0;

    /// <summary>
    /// Occupancy array's byte for a slot the room holds a player in. It is the
    /// only value the parser accepts there, and the team array's byte at the same
    /// slot is then read as that player's side, <c>0</c> or <c>1</c>.
    /// </summary>
    public const byte OccupancyOccupied = 0x07;

    /// <summary>Occupancy array's byte for a slot nobody in the room holds.</summary>
    public const byte OccupancyVacant = 0x7f;

    /// <summary>
    /// Index byte the team array of the round's run carries in all three captured
    /// runs. It and <see cref="RunOccupancyArrayIndex"/> are consecutive, which is
    /// what the replay parser pairs two arrays by — and they are consecutive
    /// because they are the next two values of the roster exchange's own counter
    /// (<see cref="RoomRosterService.TakeRunOrdinals"/>): 7 and 8 are where a
    /// join's records leave it, not a value this host writes. A later pair takes
    /// the values after it. See <c>docs/GAMEPLAY_SERVER.md</c> §7.
    /// </summary>
    public const byte RunTeamArrayIndex = 0x07;

    /// <summary>
    /// Index byte the occupancy array of the round's run carries in all three
    /// captured runs; see <see cref="RunTeamArrayIndex"/>.
    /// </summary>
    public const byte RunOccupancyArrayIndex = 0x08;

    /// <summary>
    /// The sequence byte both records of a pair carry, as the round's opening run
    /// publishes it: <c>0xf3</c> in `mgo2-game1`, <c>0x00</c> in `mgo2-game2` and
    /// <c>0x6b</c> in `mgo2-game3`. The parser requires the two to agree, and
    /// <see cref="NextSequence"/> is what the pair after it is published under —
    /// the loop the client answers and the host confirms.
    /// </summary>
    public const byte OpeningSequence = 0xf3;

    /// <summary>
    /// Builds the pair as the room's own players state it: the slot each one is
    /// filed in is marked occupied, and the team array names the side it is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every part of both bodies is measured. The type bytes, the length, slot
    /// 0's sentinel and the two free-slot bytes are the captures' own; the
    /// occupied mark <c>0x07</c> is the only value the parser accepts; and the
    /// side is a raw <c>0</c> or <c>1</c>, which is what the host writes for a
    /// player whose side it knows — `mgo2-game1` writes `0x0f` on <c>1</c> and
    /// `0x14` on <c>0</c>, and then `0x14` on <c>1</c> once that player changed
    /// sides, and `mgo2-game3` writes `0x10` on <c>0</c>.
    /// </para>
    /// <para>
    /// A player the room puts on no side — <c>game_players.team</c> holding the
    /// third role or the client's own 0xFE — is still a player of the room, so
    /// its slot is marked occupied and its team byte is
    /// <see cref="TeamArrayNoTeam"/> rather than a side chosen for it.
    /// </para>
    /// </remarks>
    /// <param name="sequence">Sequence byte both records of the pair carry.</param>
    /// <param name="slots">
    /// The room's players, by their key in its 24-entry player table: 1 to 23,
    /// since slot 0 is the host's own and is never one of them.
    /// </param>
    /// <returns>The two bodies, in the order they are published: teams, occupancy.</returns>
    public static (byte[] Teams, byte[] Occupancy) BuildPair(
        byte sequence,
        IReadOnlyList<LobbyTeamSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        return (
            BuildArray(TeamArrayIdentifier, sequence, slots, teams: true),
            BuildArray(OccupancyArrayIdentifier, sequence, slots, teams: false));
    }

    /// <summary>
    /// Builds one array of the pair: its type, the sequence byte, slot 0's byte,
    /// and the twenty-three slots behind it, each written from the room's players.
    /// </summary>
    /// <param name="type">One of <see cref="TeamArrayIdentifier"/> or <see cref="OccupancyArrayIdentifier"/>.</param>
    /// <param name="sequence">Sequence byte the record carries.</param>
    /// <param name="slots">The room's players, by their key in the player table.</param>
    /// <param name="teams">True for the team array, false for the occupancy array.</param>
    /// <returns>The record body.</returns>
    private static byte[] BuildArray(
        byte type,
        byte sequence,
        IReadOnlyList<LobbyTeamSlot> slots,
        bool teams)
    {
        var body = new byte[ArrayLength];
        body[0] = type;
        body[1] = sequence;
        body[2] = teams ? TeamArraySlotZero : OccupancyVacant;
        body.AsSpan(3).Fill(teams ? TeamArrayVacant : OccupancyVacant);

        foreach (var slot in slots)
        {
            if (slot.Slot is <= 0 or >= ArraySlotCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(slots),
                    slot.Slot,
                    $"A lobby slot is a key of the room's {ArraySlotCount}-entry player table, and slot 0 is the host's own.");
            }

            // Slot s is the body's byte at 2 + s: slot 0 is the sentinel at byte
            // 2, and the last slot is the body's last byte.
            var index = 2 + slot.Slot;
            if (!teams)
            {
                body[index] = OccupancyOccupied;
                continue;
            }

            body[index] = slot.Team is 0 or 1 ? (byte)slot.Team.Value : TeamArrayNoTeam;
        }

        return body;
    }

    /// <summary>Type byte the host confirms an answer under.</summary>
    public const byte ConfirmationIdentifier = 0x81;

    /// <summary>Type byte a client answers the team array under.</summary>
    public const byte TeamArrayAnswerIdentifier = 0x82;

    /// <summary>Type byte a client answers the occupancy array under.</summary>
    public const byte OccupancyArrayAnswerIdentifier = 0x84;

    /// <summary>
    /// Attribute class the confirmation travels under. The captures give it
    /// <c>26</c>, where the arrays behind it carry a positional ordinal — so
    /// the confirmation's fourth byte is a class and not a place in a run.
    /// </summary>
    public const byte ConfirmationClass = 26;

    /// <summary>Length of either answer and of the confirmation.</summary>
    public const int AnswerLength = 2;

    /// <summary>
    /// Type byte a client's request to be placed in the room opens with.
    /// </summary>
    /// <remarks>
    /// <b>It is measured, and it is the one request of the exchange this host
    /// used to drop.</b> Every capture holds it — `83 NN e0 7f` while the client
    /// waits for a room, then `83 NN 00 07`/`83 NN 01 07` once it is in one —
    /// and every host answers it within 0.3 to 2.9 s, which is what makes it a
    /// request rather than a report: a copy that draws no answer is re-sent, and
    /// the second byte climbs by one with every re-send (`mgo2-game1`
    /// <c>f3</c>, <c>f4</c>, <c>f5</c>; `mgo2-game2` <c>00</c>, <c>01</c>, <c>02</c>).
    /// The two live captures this host serves hold exactly one copy each and no
    /// answer, because this host wrote none.
    /// </remarks>
    public const byte RoomEntryRequestIdentifier = 0x83;

    /// <summary>
    /// Length of a client's request to be placed in the room: its type byte, the
    /// sequence it names, its own state and its flags. The state and the flags
    /// are not read here — the captures move both without the answer's shape
    /// moving — so the body's first two bytes are the whole of what is answered.
    /// </summary>
    public const int RoomEntryRequestLength = 4;

    /// <summary>
    /// Offset, in a request, of the sequence byte the answer names.
    /// </summary>
    public const int RequestSequenceIndex = 1;

    /// <summary>
    /// Whether a body is a client's request to be placed in the room rather than
    /// something else on the same identifier.
    /// </summary>
    /// <remarks>
    /// It is read beside <see cref="IsArrayEcho"/> because the two are the same
    /// exchange: the arrays are <c>85</c>/<c>86</c>, the client answers them
    /// under <c>82</c>/<c>84</c>, and this, the client's own request for them,
    /// opens <c>83</c>. A body that is neither is a profile or a settings
    /// payload, which is what the handler behind this falls through to.
    /// </remarks>
    /// <param name="body">Body of an inbound record.</param>
    /// <returns><c>true</c> when the body asks for the room.</returns>
    public static bool IsRoomEntryRequest(ReadOnlySpan<byte> body) =>
        body.Length == RoomEntryRequestLength && body[0] == RoomEntryRequestIdentifier;

    /// <summary>The sequence byte a client's request names.</summary>
    /// <param name="body">Body of an inbound request.</param>
    /// <returns>The sequence the client is asking under.</returns>
    public static byte RequestSequenceOf(ReadOnlySpan<byte> body) => body[RequestSequenceIndex];

    /// <summary>Offset of the sequence byte in an answer or the confirmation.</summary>
    public const int SequenceIndex = 1;

    /// <summary>
    /// Whether a body is a client's answer to one of the arrays rather than
    /// something else on the same identifier.
    /// </summary>
    /// <remarks>
    /// The roster types carry a profile, a settings payload and an answer, and
    /// the answer is the only one of the three that is two bytes and opens on
    /// one of the two answer type bytes. Reading it here is what keeps a live
    /// client's answer from being parsed as a profile and answered with a
    /// roster.
    /// </remarks>
    /// <param name="body">Body of an inbound record.</param>
    /// <returns><c>true</c> when the body answers a pair.</returns>
    public static bool IsArrayEcho(ReadOnlySpan<byte> body) =>
        body.Length == AnswerLength
        && body[0] is TeamArrayAnswerIdentifier or OccupancyArrayAnswerIdentifier;

    /// <summary>The sequence byte an answer names.</summary>
    /// <param name="body">Body of an inbound answer.</param>
    /// <returns>The sequence the client is answering.</returns>
    public static byte SequenceOf(ReadOnlySpan<byte> body) => body[SequenceIndex];

    /// <summary>
    /// The sequence after the one a client answered, which is the sequence the
    /// next pair is published under.
    /// </summary>
    /// <param name="sequence">Sequence the client answered.</param>
    /// <returns>The next sequence.</returns>
    public static byte NextSequence(byte sequence) => unchecked((byte)(sequence + 1));

    /// <summary>The confirmation record's body: its type byte and the sequence.</summary>
    /// <param name="sequence">Sequence being confirmed.</param>
    /// <returns>The record body.</returns>
    public static byte[] BuildConfirmation(byte sequence) => [ConfirmationIdentifier, sequence];
}
