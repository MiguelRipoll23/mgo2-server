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
/// and a <c>0x0002</c> state blob, several seconds apart. Its own length is
/// <see cref="SparsePhaseMilliseconds"/>, the sum of the delays
/// <see cref="SparseSteps"/> itself carries, and what follows it is the beat.
/// </item>
/// <item>
/// The <b>match-start run</b> is not one of this file's phases: it is written
/// by <see cref="MatchStartRunService"/> when a client reports itself ready,
/// and its frames are <see cref="MatchStartRunFramesUtils"/>'s. From here the
/// sparse phase runs straight into the beat, which is what keeps the channel
/// moving while the round is still closed.
/// </item>
/// <item>
/// The <b>steady phase</b> (t+42.2 s on): the two <c>0x0a61 fe</c> rules records
/// every frame, the vitals record joining them 27.0 s in, with a round-clock,
/// state-blob or control record inserted in front of them from time to time and
/// the item-box state group behind them in one frame in every eight.
/// </item>
/// </list>
/// <para>
/// <b>What the replay reproduces from the run on</b>: the beat frame's
/// <see cref="VitalsOnsetFrames"/>-frame delay before the vitals record joins it
/// and the item-box state group riding in every
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
/// per-player position and objective frames the host's own character produces.
/// The sibling-generation identifier set (<c>0x800</c> bit: <c>0x1254</c>,
/// <c>0x12de</c>, <c>0x0261</c>, <c>0x010c</c>) was on this list and is not any
/// more: the set follows the round (<see cref="MatchRecordGenerationUtils"/>),
/// and the beat reads <see cref="MatchRoundStateService.Generation"/> on every
/// frame and restarts at each round's own run, so a round after the first is
/// written under its own set rather than the first's. The steady
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
    /// record carries, under the same attribute class.
    /// </summary>
    /// <remarks>
    /// <b>Its actor is the peer the frame is written to, and its value is the
    /// recorded peer's own.</b> A player's tick records are one block of six
    /// identifiers, and this is that block's health slot — one above the player's
    /// state record, one below its position record, with the weapon pair at
    /// <c>+4</c>, the aim record at <c>+6</c> and the damage record at <c>-3</c>:
    /// the replay parser's own role table, whose <c>VID_HEALTH</c> is <c>-1</c>
    /// off a position stream (<c>rpdt.js</c>;
    /// <c>docs/protocol/RPDT_REPLAY_RECORDS.md</c>). The client sets <c>0x8000</c>
    /// on its own where the host does not (<c>0x0862</c> against the client's
    /// <c>0x8063</c>). The block's low byte is the player's own key in the room's
    /// player table — one block per slot, ten apart — so this value belongs to
    /// this capture's peer and not to the beat: it is the slot the room's own
    /// players hold that a beat writes (<see cref="PlayerVitalsFramesUtils"/>),
    /// and this constant is what a peer with no slot yet is written.
    /// </remarks>
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
    /// Measured identifier of the operations stream: the record family that
    /// carries the round's clock, its state block and its outcome, the kill feed
    /// and this host's own periodic insertions.
    /// </summary>
    /// <remarks>
    /// <b>It is a stream and not a record.</b> Every operations record shares this
    /// identifier and is told apart by the first byte of its body
    /// (<see cref="MatchRecordBodiesUtils.OperationsClockOpcode"/>, 
    /// <see cref="MatchRecordBodiesUtils.OperationsStateOpcode"/>, and the
    /// unframed blocks that follow): a live capture writes <c>0x00</c>, <c>0x01</c>,
    /// <c>0x02</c>, <c>0x09</c>, <c>0x0b</c> and <c>0x0f</c> under this one
    /// identifier within a single round. Reading it as "the round clock record"
    /// is what an earlier name did.
    /// </remarks>
    public const ushort OperationsIdentifier = 0x1a54;

    /// <summary>
    /// Measured identifier of the same operations stream with the payload class
    /// bit (<c>0x8000</c>) set, which the recorded host also inserts.
    /// </summary>
    public const ushort OperationsClassBitIdentifier =
        OperationsIdentifier | PeerAcknowledgementUtils.PayloadClassBit;

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
    /// Beat frames between one <see cref="OperationsIdentifier"/> insertion and
    /// the next: 90, so 2.96 s. Measured over the capture's solo window, where
    /// each form recurs every ~91 frames (3.0 s) — 26 of one form and 18 of the
    /// other over 1 812 frames — and the two together every 45.9 (1.5 s).
    /// </summary>
    public const int OperationsPeriodFrames = 90;

    /// <summary>
    /// Beat frames between one <see cref="OperationsClassBitIdentifier"/>
    /// insertion and the next, measured the same way.
    /// </summary>
    public const int OperationsClassBitPeriodFrames = 90;

    /// <summary>
    /// Offset of the first class-bit insertion within its period: half of it, so
    /// an operations insertion of one form or the other follows every
    /// <see cref="OperationsPeriodFrames"/> / 2 frames — the measured 1.48 s.
    /// </summary>
    public const int OperationsClassBitPhaseFrames = 45;

    /// <summary>Beat frames between one state-blob insertion and the next: 5.98 s measured.</summary>
    public const int StateBlobPeriodFrames = 181;

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
    internal const byte RulesRosterMemberListClass = 0x00;

    /// <summary>
    /// Attribute class of the second rules-roster record of a beat frame. It is
    /// the replay parser's <c>TYPE_STATE</c>, under which a body of the list-end
    /// byte alone is the empty rules list — the shape the parser names without
    /// inferring anything.
    /// </summary>
    internal const byte RulesRosterEmptyListClass = 0x01;

    /// <summary>
    /// Index byte — the record's fourth byte — on the match-start run's team
    /// array. The occupancy array of the same pair follows at the next index,
    /// which is what the replay parser's join of the two requires.
    /// </summary>
    public const byte TeamArrayIndex = 0x07;

    /// <summary>Attribute class of the sparse state-blob tick record.</summary>
    public const byte StateBlobSparseAttributeClass = 0x01;

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
    /// <summary>
    /// The beat frame: an optional head insertion, the vitals record once the
    /// capture's round reaches it, the rules-roster pair, and — behind the pair —
    /// the item-box group when the frame carries one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The vitals record is not in every frame. The recorded host writes the
    /// rules-roster pair alone for the round's first 27.0 s and adds
    /// <see cref="VitalsRecordIdentifier"/> only from
    /// <see cref="VitalsOnsetFrames"/> on — 781 of the solo window's frames carry
    /// nothing else, against 2 385 that carry the vitals record.
    /// </para>
    /// <para>
    /// <b>The item-box group follows the pair, not precedes it.</b> game1's
    /// round-1 item-box frames read <c>0a61 0a61 1a50 … 1a5a …</c> — the pair
    /// first, the group behind it — and once the vitals record has joined they
    /// read <c>090c 0a61 0a61 1a50 … 1a5a …</c>, so the group is the frame's last
    /// records. The recorded stream is not uniform across the match — a round-2
    /// frame carrying a state blob reads
    /// <c>1a50 … 1a5a … 0002 090c 0a61 0a61</c>, the group first — so this
    /// follows the opening round, whose frames the client answers first.
    /// </para>
    /// </remarks>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="insertions">Records to place at the head of the frame, or null.</param>
    /// <param name="includeVitals">Whether the vitals record rides in this frame.</param>
    /// <param name="itemBoxes">Item-box group to place behind the rules pair, or null.</param>
    /// <param name="maps">The room's rotation maps, or null for the empty pair.</param>
    /// <returns>The records of one beat frame.</returns>
    public static List<RecordMessage> BeatFrame(
        MatchRecordGenerationUtils generation,
        IReadOnlyList<RecordMessage>? insertions = null,
        bool includeVitals = true,
        IReadOnlyList<RecordMessage>? itemBoxes = null,
        IReadOnlyList<int>? maps = null)
    {
        var frame = new List<RecordMessage>(
            (insertions?.Count ?? 0) + (itemBoxes?.Count ?? 0) + (includeVitals ? 3 : 2));
        if (insertions is not null)
        {
            frame.AddRange(insertions);
        }

        if (includeVitals)
        {
            frame.Add(new(generation.Vitals, MatchRecordBodiesUtils.Vitals(), VitalsAttributeClass));
        }

        frame.AddRange(RulesPairFramesUtils.RulesPair(generation, maps ?? []));

        if (itemBoxes is not null)
        {
            frame.AddRange(itemBoxes);
        }

        return frame;
    }

    /// <summary>
    /// The rules-roster pair as the room's stream carries it, in either phase: the
    /// empty list under each of the two classes.
    /// </summary>
    /// <remarks>
    /// <b>It is not the round's record and the beat is not its only writer.</b> The
    /// recorded host writes the pair from the roster answer onward and on every
    /// tick of a briefing, and the beat goes on writing it a frame at a time once
    /// the round opens: <c>mgo2-game-started-on-briefing</c> writes
    /// <c>0x0261 fe</c> under the two classes 12 to 26 times per ten seconds from
    /// its roster answer to its round — the room's whole briefing, 198 s of it —
    /// and 2 385 frames of its round after that. A host that wrote the pair only
    /// inside its round would leave its peer in a briefing told nothing on the
    /// stream the room's membership travels under.
    /// </remarks>
    /// <param name="generation">Identifier set of the room being briefed or played.</param>
    /// <returns>The two records, in the order a frame carries them.</returns>
    public static List<RecordMessage> RulesPair(MatchRecordGenerationUtils generation) =>
    [
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), RulesRosterMemberListClass),
        new(generation.RulesRoster, MatchRecordBodiesUtils.RulesRoster(), RulesRosterEmptyListClass),
    ];

    /// <summary>
    /// An operations insertion, in either of the two identifiers the capture
    /// uses: the round's clock, as far as it has run.
    /// </summary>
    /// <param name="generation">Identifier set of the round being played.</param>
    /// <param name="alternate">True for the class-bit form of the identifier.</param>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <param name="clockUnits">
    /// Units of the round clock still to run, in the stream's own 125 ms ticks.
    /// It is the caller's countdown and not the round's opening value: the
    /// recorded host moves this record every 1.5 s, from its rule's time towards
    /// zero (<c>docs/protocol/BRIEFING_FLOW.md</c> §3).
    /// </param>
    /// <returns>The insertion record.</returns>
    public static RecordMessage OperationsInsertion(
        MatchRecordGenerationUtils generation,
        bool alternate,
        byte fourthByte,
        int clockUnits) =>
        new(
            alternate ? generation.OperationsClassBit : generation.Operations,
            MatchRecordBodiesUtils.RoundTimeLimitUnits(clockUnits),
            fourthByte);

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
