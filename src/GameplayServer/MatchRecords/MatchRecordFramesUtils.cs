using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The frames the dedicated host keeps writing to a peer after the join, as
/// measured from the solo window of <c>docs/mgo2-game.pcapng</c> — the thirteen
/// minutes in which the joining client was alone with the host, before the
/// second player arrives at t+154 s.
/// </summary>
/// <remarks>
/// <para>
/// The order, the frame counts and the delays below are the capture's; the
/// bodies are <see cref="MatchRecordBodiesUtils"/>'. What the replay console's
/// own parser adds (<c>docs/analysis/replay-console/rpdt.js</c>, mapped in
/// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>) is a reading of those bodies: a
/// session identifier's low eleven bits name a stream and the payload's first
/// byte an operation in it, so the <c>0x1a54</c> / <c>0x9a54</c> record this
/// file used to call a *slot* is the operations stream's round clock and its
/// ninety-byte form the same stream's state block.
/// </para>
/// <para>
/// Three phases are reproduced, on the capture's own clock, counted from the
/// roster answer:
/// </para>
/// <list type="number">
/// <item>
/// The <b>sparse phase</b> (t+4.9 s to t+42.2 s): a <c>0xd001</c> control byte
/// and a <c>0x0002</c> state blob, several seconds apart. It ends there: the
/// run that follows is the host's answer to the client's readiness report,
/// not a step on this clock (see <see cref="SparseSteps"/>).
/// </item>
/// <item>
/// The <b>match-start run</b>, written once the client has asked for it: the
/// frame that opens the round, carrying the host's own short entry and the
/// ninety-byte operations state block under it.
/// </item>
/// <item>
/// The <b>steady phase</b> (t+42.2 s on): the two <c>0x0a61 fe</c> rules records
/// every frame, the vitals record joining them 27.0 s in, with a round-clock,
/// state-blob or control record inserted in front of them from time to time and
/// the item-box state group in front of one frame in every eight.
/// </item>
/// </list>
/// <para>
/// <b>What the replay reproduces from the run on</b>: the beat frame's
/// <see cref="VitalsOnsetFrames"/>-frame delay before the vitals record joins it,
/// the run's four 11-record repeats (see
/// <see cref="MatchStartRunFramesUtils.MatchStartRunRepeat"/>), and
/// the item-box state group riding in every
/// <see cref="ItemBoxFramesUtils.ItemBoxPeriodFrames"/>th frame (see
/// <see cref="ItemBoxFramesUtils.ItemBoxGroup"/>).
/// </para>
/// <para>
/// <b>What the replay leaves out</b>, so a later change does not rediscover it:
/// the item boxes' <em>spawn</em> bodies (head codes <c>0x74</c>, <c>0x8c</c> and
/// <c>0xb4</c> under the same <c>0x1a50</c> identifier), which are the live
/// round's own item placement and move irregularly around the group this file
/// does replay; the member-bearing rules-roster records, since
/// <see cref="MatchRecordBodiesUtils.RulesRoster"/> is the empty list; the
/// per-player position and objective frames the host's own character produces;
/// and the sibling-generation identifier set (<c>0x800</c> bit: <c>0x1254</c>,
/// <c>0x12de</c>, <c>0x0261</c>, <c>0x010c</c>), which the capture uses only from
/// its second round on, where this replay plays one round per peer. The steady
/// control's cadence is the capture's mean rather than a rule it holds: the solo
/// window's gaps run from 1 to 337 frames between the sparse phase's close and
/// the run, so no fixed period reproduces them.
/// </para>
/// </remarks>
public static class MatchRecordFramesUtils
{
    /// <summary>
    /// Measured identifier of the two-byte health/stamina record that rides in
    /// the beat frame. Its body is the one the verified <c>0x0080</c> vitals
    /// record carries, under the same attribute class; its actor is **[U]**.
    /// </summary>
    public const ushort VitalsRecordIdentifier = 0x090c;

    /// <summary>
    /// Measured identifier of one of the two rules-roster records the beat frame
    /// carries. The replay parser registers it and <c>0x0261</c> as its rules
    /// VIDs — one pair, the two vids a <c>0x800</c> bit apart — while the capture
    /// writes both under this one and tells them apart by the class byte above;
    /// group semantics remain **[U]**.
    /// </summary>
    public const ushort RulesRosterIdentifier = 0x0a61;

    /// <summary>
    /// Measured identifier of the operations record that carries the round
    /// clock. **[I]**
    /// </summary>
    public const ushort RoundClockIdentifier = 0x1a54;

    /// <summary>
    /// Measured identifier of the same operations record with the <c>0x800</c>
    /// bit set, which the recorded host also inserts. **[I]**
    /// </summary>
    public const ushort RoundClockAlternateIdentifier = 0x9a54;

    /// <summary>Measured identifier of the marker that opens the match-start run. **[U]**</summary>
    public const ushort MatchStartMarker = 0x5ade;

    /// <summary>Measured identifier carried inside the match-start run. **[U]**</summary>
    public const ushort MatchStartPayload = 0x1ade;

    /// <summary>
    /// Period of one beat frame. Measured at 30.0 frames a second across the solo
    /// beat (2 478 vitals-carrying frames over 82.57 s, 3 255 counter steps over
    /// 109.58 s), and the rate the replay parser reports for its own recordings —
    /// <c>tickRate</c> 30.303, 33 ms — on three of the checked-in files. An
    /// earlier reading put this at 29 ms.
    /// </summary>
    public const int FramePeriodMilliseconds = 33;

    /// <summary>
    /// Session-record flags2 value copied from the match-start run's team array,
    /// the run's seventh index byte. Its meaning beyond that is unresolved.
    /// </summary>
    public const byte SteadySessionFlagsStart = TeamArrayIndex;

    /// <summary>
    /// Beat frames between one <see cref="RoundClockIdentifier"/> insertion and
    /// the next: 90, so 2.96 s. Measured over the capture's solo window, where
    /// each form recurs every ~91 frames (3.0 s) — 26 of one form and 18 of the
    /// other over 1 812 frames — and the two together every 45.9 (1.5 s).
    /// </summary>
    public const int RoundClockPeriodFrames = 90;

    /// <summary>
    /// Beat frames between one <see cref="RoundClockAlternateIdentifier"/>
    /// insertion and the next, measured the same way.
    /// </summary>
    public const int RoundClockAlternatePeriodFrames = 90;

    /// <summary>
    /// Offset of the first alternate clock insertion within its period: half of
    /// it, so a clock of one form or the other follows every
    /// <see cref="RoundClockPeriodFrames"/> / 2 frames — the measured 1.48 s.
    /// </summary>
    public const int RoundClockAlternatePhaseFrames = 45;

    /// <summary>Beat frames between one state-blob insertion and the next: 5.98 s measured.</summary>
    public const int StateBlobPeriodFrames = 181;

    /// <summary>
    /// How long one round lasts before the next is opened: 147.6 s, measured
    /// between the capture's first round's run (t+46.42 s) and its second
    /// round's (t+194.04 s).
    /// </summary>
    /// <remarks>
    /// A round ends when a team reaches the goal or the round's timer runs out,
    /// whichever comes first. The capture's first round ended by the goal — it
    /// is 147.6 s against the 420 s the round's own clock carries
    /// (<c>MatchRecordBodiesUtils.RoundClockUnits</c> is 3 360 units of 125 ms) —
    /// and this replay, which does not simulate a match and so cannot see a
    /// team reach one, opens the next round at the measured length and lets the
    /// second round's identifiers carry the difference. A round that reached
    /// the timer instead would be the longer of the two.
    /// </remarks>
    public const int RoundLengthMilliseconds = 147620;

    /// <summary>
    /// Beat frame at which the vitals record starts riding in the beat frame:
    /// 818 frames, 27.0 s after the match-start run. The capture's run is at
    /// t+46.42 s and its first <see cref="VitalsRecordIdentifier"/> is at
    /// t+73.43 s; before that the beat frame is the rules-roster pair alone.
    /// </summary>
    public const int VitalsOnsetFrames = 818;


    /// <summary>
    /// Beat frames between one control-byte insertion and the next: 130, the
    /// capture's mean. Its gaps are not uniform — the solo window alternates
    /// ~83 and ~180 — so this is the average interval rather than a cadence the
    /// host holds.
    /// </summary>
    public const int ControlPeriodFrames = 130;

    /// <summary>
    /// Attribute class of the vitals record, as measured. A tick record's class
    /// byte is its payload type under the replay framing, and this value is the
    /// replay parser's <c>TYPE_HEALTH</c> discriminator, which is why its health
    /// shape reads <c>[0x03, health, stamina]</c>. The verified <c>0x0080</c>
    /// vitals record travels under the same one.
    /// </summary>
    private const byte VitalsAttributeClass = 0x03;

    /// <summary>
    /// Attribute class of the first rules-roster record of a beat frame. It is
    /// the replay parser's <c>TYPE_DAMAGE</c>, under which it parses the record's
    /// body as a compact group/member list; the recorded body is the list-end
    /// byte alone, so the list is empty.
    /// </summary>
    private const byte RulesRosterMemberListClass = 0x00;

    /// <summary>
    /// Attribute class of the second rules-roster record of a beat frame. It is
    /// the replay parser's <c>TYPE_STATE</c>, under which a body of the list-end
    /// byte alone is the empty rules list — the shape the parser names without
    /// inferring anything.
    /// </summary>
    private const byte RulesRosterEmptyListClass = 0x01;

    /// <summary>
    /// Index byte — the record's fourth byte — on the match-start run's team
    /// array. The occupancy array of the same pair follows at the next index,
    /// which is what the replay parser's join of the two requires.
    /// </summary>
    public const byte TeamArrayIndex = 0x07;

    /// <summary>Attribute class of the sparse state-blob tick record.</summary>
    private const byte StateBlobAttributeClass = 0x01;

    /// <summary>Attribute class paired with a zero-valued steady blob.</summary>
    private const byte StateBlobSteadyZeroAttributeClass = 0x15;

    /// <summary>Attribute class paired with a 100-valued steady blob.</summary>
    private const byte StateBlobSteadyLiveAttributeClass = 0x01;

    /// <summary>Fourth byte of an early control record, as measured.</summary>
    private const byte ControlFourthByteEarly = 0x02;

    /// <summary>Fourth byte of the alternating control record, as measured.</summary>
    private const byte ControlFourthByteAlternate = 0x03;

    /// <summary>Fourth byte of the rare control record, as measured.</summary>
    private const byte ControlFourthByteRare = 0x0e;

    /// <summary>One message of the stream, with the fourth byte it travels with.</summary>
    /// <param name="Type">Message identifier.</param>
    /// <param name="Body">Message body.</param>
    /// <param name="FourthByte">Value of the record's fourth byte.</param>
    public sealed record RecordMessage(ushort Type, byte[] Body, byte FourthByte = 0);

    /// <summary>One step of the stream: records to write together, then the wait before the next.</summary>
    /// <param name="DelayMilliseconds">Delay before this step, in milliseconds.</param>
    /// <param name="Records">Records written together as one datagram.</param>
    /// <param name="Compressed">
    /// Whether the datagram goes out LZSS-compressed. The recorded host marks the
    /// state blobs and the match-start run and leaves the one-byte control records
    /// and the beat frames plain, so the marker follows the frame rather than the
    /// peer — the same rule the roster run follows.
    /// </param>
    public sealed record RecordStep(
        int DelayMilliseconds,
        IReadOnlyList<RecordMessage> Records,
        bool Compressed = false);

    /// <summary>
    /// The sparse phase, counted from the roster answer. Each delay is measured
    /// between consecutive captured frames, so a zero means the two went out back
    /// to back.
    /// </summary>
    /// <remarks>
    /// <b>The phase ends at the last state blob, not at the round.</b> The
    /// recorded host's match-start run is the <em>answer</em> to the client's
    /// readiness report — the <c>e1 NN</c> payload record that arrives at
    /// roster+40.59 s in game1, 1.66 s before the run — so a server that writes
    /// the run on this phase's own clock opens a round the client never asked
    /// for and then ignores the twenty-three requests that follow it. The run
    /// waits for <see cref="MatchRecordService"/> to be told the request arrived.
    /// </remarks>
    /// <returns>The steps in send order, ending at the last state blob.</returns>
    public static List<RecordStep> SparseSteps() =>
    [
        new(4930, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x02)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
        new(0, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x02)]),
        new(4800, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x03)]),
        new(2400, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x03)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
        new(2370, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x04)]),
        new(2750, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
        new(2370, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x05)]),
        new(4750, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
        new(0, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x06)]),
        new(4770, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x07)]),
        new(110, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
        new(4250, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.EarlyControl(), 0x08)]),
        new(2490, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
        new(6250, [new(UdpCommandConstants.InGameControl, MatchRecordBodiesUtils.AlternateControl(), 0x09)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobAttributeClass)], Compressed: true),
    ];

    /// <summary>
    /// The beat frame: an optional head insertion, the vitals record once the
    /// capture's round reaches it, and the rules-roster pair.
    /// </summary>
    /// <remarks>
    /// The vitals record is not in every frame. The recorded host writes the
    /// rules-roster pair alone for the round's first 27.0 s and adds
    /// <see cref="VitalsRecordIdentifier"/> only from
    /// <see cref="VitalsOnsetFrames"/> on — 781 of the solo window's frames carry
    /// nothing else, against 2 385 that carry the vitals record.
    /// </remarks>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="insertions">Records to place at the head of the frame, or null.</param>
    /// <param name="includeVitals">Whether the vitals record rides in this frame.</param>
    /// <returns>The records of one beat frame.</returns>
    public static List<RecordMessage> BeatFrame(
        MatchRecordGenerationUtils generation,
        IReadOnlyList<RecordMessage>? insertions = null,
        bool includeVitals = true)
    {
        var frame = new List<RecordMessage>((insertions?.Count ?? 0) + (includeVitals ? 3 : 2));
        if (insertions is not null)
        {
            frame.AddRange(insertions);
        }

        if (includeVitals)
        {
            frame.Add(new(generation.Vitals, MatchRecordBodiesUtils.Vitals(), VitalsAttributeClass));
        }

        frame.Add(new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), RulesRosterMemberListClass));
        frame.Add(new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), RulesRosterEmptyListClass));
        return frame;
    }

    /// <summary>The clock insertion, in either of the two identifiers the capture uses.</summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="alternate">True for the sibling form of the identifier.</param>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <returns>The insertion record.</returns>
    public static RecordMessage RoundClockInsertion(
        MatchRecordGenerationUtils generation,
        bool alternate,
        byte fourthByte) =>
        new(alternate ? generation.RoundClockAlternate : generation.RoundClock, MatchRecordBodiesUtils.RoundClock(), fourthByte);

    /// <summary>A state-blob insertion from the recorded steady stream.</summary>
    /// <param name="live">True for the captured 100-valued variant.</param>
    /// <returns>The insertion record.</returns>
    public static RecordMessage StateBlobInsertion(bool live) =>
        live
            ? new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SparseStateBlob(), StateBlobSteadyLiveAttributeClass)
            : new(UdpCommandConstants.GameStateBlob, MatchStateBodiesUtils.SteadyZeroStateBlob(), StateBlobSteadyZeroAttributeClass);

    /// <summary>A control-byte insertion.</summary>
    /// <param name="body">Control body to carry.</param>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <returns>The insertion record.</returns>
    public static RecordMessage ControlInsertion(byte[] body, byte fourthByte) =>
        new(UdpCommandConstants.InGameControl, body, fourthByte);

    /// <summary>Body of the early sparse-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] EarlyControlBody() => MatchRecordBodiesUtils.EarlyControl();

    /// <summary>Body of the alternating sparse-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] AlternateControlBody() => MatchRecordBodiesUtils.AlternateControl();

    /// <summary>Body of the steady-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] SteadyControlBody() => MatchRecordBodiesUtils.SteadyControl();

    /// <summary>Body of the control record seen once in the steady phase.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] RareControlBody() => MatchRecordBodiesUtils.RareControl();

    /// <summary>Fourth byte of the early sparse-phase control record.</summary>
    public const byte EarlyControlFourthByte = ControlFourthByteEarly;

    /// <summary>Fourth byte of the alternating sparse-phase control record.</summary>
    public const byte AlternateControlFourthByte = ControlFourthByteAlternate;

    /// <summary>Fourth byte of the rare control record.</summary>
    public const byte RareControlFourthByte = ControlFourthByteRare;

    /// <summary>Converts one stream message into the codec's message type.</summary>
    /// <param name="message">Message to convert.</param>
    /// <returns>The message the frame builder serializes.</returns>
    public static UdpMessage ToUdpMessage(RecordMessage message) =>
        FrameBuilderUtility.MessageOf(message.Type, message.Body, message.FourthByte);
}
