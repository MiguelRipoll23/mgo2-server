namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The bodies of the round's opening exchange: the client's join report,
/// the host's answer to it, and the companion state record the client pairs with
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It lives apart from <see cref="MatchRecordBodiesUtils"/> for one reason:
/// that file is the beat's, and this is the round's.</b> The three records here
/// are read in both directions and none of them is a byte the beat writes — the
/// report arrives before a round exists, the payload echoes it, and the state
/// answer is the client's own record answered under its own class. A reader
/// looking for the beat's clock, blob or control bodies should not have to step
/// over them.
/// </para>
/// <para>
/// The pairing is measured and the bytes are named in
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2 and
/// <c>docs/protocol/DATA_PHASE_DELIVERY.md</c>; what each byte <em>means</em> is
/// marked **[U]** where it is not measured, one member at a time.
/// </para>
/// </remarks>
public static class MatchStartBodiesUtils
{
    /// <summary>
    /// Length of the answer to a client's join state record: four bytes in
    /// both captures, and the class byte with it.
    /// </summary>
    public const int JoinStateAnswerLength = 4;

    /// <summary>Attribute class the host's join state answer carries.</summary>
    public const byte JoinStateAnswerClass = 0x06;

    /// <summary>Attribute class a client's join state record carries.</summary>
    public const byte JoinStateClass = 0x04;

    /// <summary>
    /// Opcode a client's join report carries, in the record's first byte.
    /// The host's reply to the same record begins
    /// <see cref="MatchStartPayloadOpcode"/> in that byte, which is what makes the
    /// two a report and its answer rather than two readings of one record.
    /// </summary>
    /// <remarks>
    /// The report is the client saying it has fully joined: it has the room's map
    /// loaded and is on the briefing screen. It is **not** the player's readiness,
    /// which travels on the operations stream as <c>1f &lt;slot&gt; 01</c> and is
    /// what opens a round (<see cref="BriefingReadyBodiesUtils"/>,
    /// <see cref="MatchStartRunService.OpenWhenEveryPlayerIsReady"/>). The byte the
    /// record carries is the client's own lobby slot. It is recognised and handed to
    /// <see cref="MatchStartRunService"/>, which writes the briefing's run that
    /// answers it on the same record read the other way.
    /// </remarks>
    public const byte JoinReportOpcode = 0xe1;

    /// <summary>Opcode the host's match-start payload carries in its first byte.</summary>
    public const byte MatchStartPayloadOpcode = 0xe0;

    /// <summary>
    /// Byte a round's run echoes when the peer it is written to has not sent a join
    /// report: the recorded host's own, <c>0x14</c>, from game1's first
    /// round's <c>0xe0 0x14</c>. No run is written without a report to echo, so
    /// this is the measured value and never a substitute for the report's own.
    /// </summary>
    public const byte DefaultJoinReportValue = 0x14;

    /// <summary>
    /// Length of the join report: the opcode and the one byte the host
    /// echoes back. Every report in the captures is exactly this long.
    /// </summary>
    public const int JoinReportLength = 2;

    /// <summary>
    /// Offset of the byte the report carries and the reply echoes.
    /// </summary>
    public const int JoinReportValueIndex = 1;

    /// <summary>
    /// Whether a body is a client's join report: the report opcode followed
    /// by the byte the host echoes back.
    /// </summary>
    /// <param name="body">Body of an inbound payload record.</param>
    /// <returns><c>true</c> when the body says the client has joined the room.</returns>
    public static bool IsJoinReport(ReadOnlySpan<byte> body) =>
        body.Length == JoinReportLength && body[0] == JoinReportOpcode;

    /// <summary>
    /// The host's payload record, the answer to a client's join report: the
    /// payload opcode and the byte the client reported, then the trailing zeros
    /// the recorded bodies carry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The echo is the whole of the answer this server can read. Across the
    /// captures every report/answer pair agrees on that one byte — <c>e1 14</c>
    /// is answered <c>e0 14 …</c> and <c>e1 01</c> is answered <c>e0 01 …</c> —
    /// and answering with anything but the client's own byte would be a value
    /// this server has no source for. What the byte means is **[U]**: game1
    /// sends 0x14 and game2 0x01 on the same record, so it is neither a
    /// constant nor a count this server can derive.
    /// </para>
    /// <para>
    /// <b>The trailing length is not settled.</b> game1 answers in six bytes
    /// (<c>e0 14 00 00 00 00</c>) and game2 in five (<c>e0 01 00 00 00</c>), on
    /// the same record and the same report shape; game1 later grows the same
    /// record to fourteen bytes mid-round. Nothing measured here separates the
    /// two — both captures hold four roster entries and neither occupies a slot
    /// in the array that opens the run — so this writes game1's six-byte form,
    /// the capture the rest of the replay is measured from, rather than
    /// inventing a rule. A round opened against a game2-shaped host would carry
    /// one zero more than that host writes. Recorded as **[U]** in
    /// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2.
    /// </para>
    /// </remarks>
    /// <param name="reported">Byte the client's join report carried.</param>
    /// <returns>The record body.</returns>
    public static byte[] MatchStartPayload(byte reported) =>
        [MatchStartPayloadOpcode, reported, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    /// The host's answer to a client's join state record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The client sends its join state as one byte under class
    /// <see cref="JoinStateClass"/>, in the same datagram as the join
    /// report, carrying its own lobby slot. The host answers under class
    /// <see cref="JoinStateAnswerClass"/> in four bytes.
    /// </para>
    /// <para>
    /// <b>The first byte is the room's players, as a bit set that grows as they
    /// report</b> — measured on the two captures that hold a briefing:
    /// <c>mgo2-game-started-on-briefing</c> answers <c>02</c> for its first
    /// reporting player, then <c>06</c>, <c>0e</c> and <c>1e</c> as the other
    /// three report (and <c>16</c> later, once a player has left), and
    /// <c>mgo2-game-joined-when-red-team-then-briefing</c> answers <c>2a</c> —
    /// bits 1, 3 and 5, the slots its reporting players hold. So this is the
    /// room's own set and not an echo of the record's byte, and the bit a player
    /// owns is the slot its report carries — <c>08</c> for a room of one whose
    /// only reporter is slot 3 (`docs/analysis/briefing-start-comparison.md` §3).
    /// </para>
    /// <para>
    /// <b>The first byte is derived, and the trailing three are not.</b>
    /// The set is the room's own reporting players, one bit per player at the
    /// slot its own report byte names (<see cref="Rooms.BriefingStateService.JoinedMask"/>),
    /// so a room whose only reporter carries <c>03</c> writes <c>08</c> and a
    /// room whose reporters carry slots 1–4 writes the run of bits the capture
    /// writes. **[V]** the length, the class and the growth; **[D]** the set,
    /// from the growth.
    /// </para>
    /// <para>
    /// <b>The trailing three bytes are the recorded zeros, and the shape is
    /// host-dependent.</b> Measured over the six captures of a working server,
    /// only the two briefings write this record as <c>[set, 0, 0, 0]</c> —
    /// <c>mgo2-game2</c> answers <c>02 00 00 00</c> and the briefings
    /// <c>02</c>/<c>06</c>/<c>0e</c>/<c>1e</c> then <c>16</c>. The other hosts do
    /// not: <c>mgo2-game1</c> answers <c>00 80 10 00</c>, <c>00 88 10 00</c>,
    /// <c>10 80 00 00</c> and <c>40 08 10 00</c>, and
    /// <c>started-team-selection</c> answers <c>01 00 02 00</c>. So the second
    /// and third bytes *do* move in three of the six — an earlier reading here
    /// said the trailing three had never moved in any capture, and that is
    /// withdrawn. This host writes the briefing form, the one whose first byte is
    /// a set it can derive, and the rest of the record stays the recorded zeros
    /// rather than a value nothing measured. **[U]** what the other hosts' flags
    /// mean and when the record is rewritten.
    /// </para>
    /// </remarks>
    /// <param name="reportingSet">
    /// The room's reporting players, one bit per player in the order they
    /// reported — the first reporter is bit 1.
    /// </param>
    /// <returns>The record body.</returns>
    public static byte[] JoinStateAnswer(byte reportingSet) =>
        [reportingSet, 0x00, 0x00, 0x00];
}
