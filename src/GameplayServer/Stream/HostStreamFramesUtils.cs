using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Stream;

/// <summary>
/// The frames the dedicated host keeps writing to a peer after the join, as
/// measured from the solo window of <c>docs/mgo2-game.pcapng</c> — the thirteen
/// minutes in which the joining client was alone with the host, before the
/// second player arrives at t+154 s.
/// </summary>
/// <remarks>
/// <para>
/// This is a replay, not a simulation: every body below is the byte string the
/// recorded host actually wrote, and the order and delays are the ones the
/// capture shows. The stream is what the earlier reading left out — the server
/// answered the join and then went quiet, while the recorded host keeps the
/// channel moving for the whole round.
/// </para>
/// <para>
/// Three phases are reproduced, on the capture's own clock, counted from the
/// roster answer:
/// </para>
/// <list type="number">
/// <item>
/// The <b>sparse phase</b> (t+4.9 s to t+46.4 s): a <c>0xd001</c> control byte
/// and a <c>0x0002</c> state blob, several seconds apart.
/// </item>
/// <item>
/// The <b>match-start run</b> at t+46.4 s: the frame that opens the round,
/// carrying the host's own short entry and the slot records beneath it.
/// </item>
/// <item>
/// The <b>steady phase</b> (t+46.4 s on): a fixed frame of
/// <c>0x090c fa fa</c> and two <c>0x0a61 fe</c> records every 29 ms, with a
/// slot record, a state blob or a control byte inserted from time to time.
/// </item>
/// </list>
/// <para>
/// <b>What none of this is:</b> understood. The record identifiers are not in
/// <see cref="UdpCommandConstants"/> because nothing has decoded them, the
/// bodies are constant and no field in them is named here, and the fourth byte
/// of each record is the host's own running value carried through from the
/// capture. The stream is sent because the recorded host sends it, on the same
/// grounds as the roster and the post-join burst.
/// </para>
/// </remarks>
public static class HostStreamFramesUtils
{
    /// <summary>Measured identifier of the host's two-byte beat record. **[U]**</summary>
    public const ushort Beat = 0x090c;

    /// <summary>Measured identifier of the host's one-byte tick record. **[U]**</summary>
    public const ushort Tick = 0x0a61;

    /// <summary>Measured identifier of the first slot record the host inserts. **[U]**</summary>
    public const ushort SlotAlpha = 0x1a54;

    /// <summary>Measured identifier of the second slot record the host inserts. **[U]**</summary>
    public const ushort SlotBeta = 0x9a54;

    /// <summary>Measured identifier that opens the match-start run. **[U]**</summary>
    public const ushort MatchStartMarker = 0x5ade;

    /// <summary>Measured identifier carried inside the match-start run. **[U]**</summary>
    public const ushort MatchStartPayload = 0x1ade;

    /// <summary>Period of one steady frame, measured at 29 ms across the round.</summary>
    public const int SteadyFramePeriodMilliseconds = 29;

    /// <summary>
    /// Fourth byte the steady phase starts from, which continues the running
    /// value the match-start run's first entry carried. **[U]**
    /// </summary>
    public const byte SteadyOrdinalStart = MatchStartEntryFourthByte;

    /// <summary>Steady frames between one <see cref="SlotAlpha"/> insertion and the next.</summary>
    public const int SlotAlphaPeriodFrames = 86;

    /// <summary>Steady frames between one <see cref="SlotBeta"/> insertion and the next.</summary>
    public const int SlotBetaPeriodFrames = 86;

    /// <summary>Steady frames between one state-blob insertion and the next.</summary>
    public const int StateBlobPeriodFrames = 214;

    /// <summary>Steady frames between one control-byte insertion and the next.</summary>
    public const int ControlPeriodFrames = 207;

    /// <summary>Offset of the first <see cref="SlotBeta"/> insertion within its period.</summary>
    public const int SlotBetaPhaseFrames = 43;

    /// <summary>Fourth byte of the beat record, as measured.</summary>
    private const byte BeatFourthByte = 0x03;

    /// <summary>Fourth bytes of the paired tick records, as measured.</summary>
    private const byte TickFourthByteFirst = 0x00;

    /// <summary>Fourth byte of the second tick record of the pair.</summary>
    private const byte TickFourthByteSecond = 0x01;

    /// <summary>
    /// The host's own value carried on the match-start run's join-tagged entry,
    /// which continues the roster's running number. **[U]**
    /// </summary>
    private const byte MatchStartEntryFourthByte = 0x07;

    /// <summary>Fourth byte the sparse state-blob frames carry, as measured.</summary>
    private const byte StateBlobFourthByte = 0x01;

    /// <summary>Body of the beat record: two bytes, constant across the round.</summary>
    private static ReadOnlySpan<byte> BeatBody => [0xfa, 0xfa];

    /// <summary>Body of the tick record: one byte, constant across the round.</summary>
    private static ReadOnlySpan<byte> TickBody => [0xfe];

    /// <summary>Body shared by both slot records.</summary>
    private static ReadOnlySpan<byte> SlotBody => [0x01, 0x20, 0x0d];

    /// <summary>Two-byte form of the first slot record, sent in the match-start run.</summary>
    private static ReadOnlySpan<byte> SlotAlphaShortBody => [0x0f, 0x00];

    /// <summary>Control byte of an early sparse-phase record.</summary>
    private static ReadOnlySpan<byte> ControlBodyEarly => [0x02];

    /// <summary>Control byte of an alternating sparse-phase record.</summary>
    private static ReadOnlySpan<byte> ControlBodyAlternate => [0x03];

    /// <summary>Control byte a steady-phase record carries.</summary>
    private static ReadOnlySpan<byte> ControlBodySteady => [0x04];

    /// <summary>One-byte control body seen once in the steady phase.</summary>
    private static ReadOnlySpan<byte> ControlBodyRare => [0x25];

    /// <summary>Fourth byte of an early control record, as measured.</summary>
    private const byte ControlFourthByteEarly = 0x02;

    /// <summary>Fourth byte of the alternating control record, as measured.</summary>
    private const byte ControlFourthByteAlternate = 0x03;

    /// <summary>Fourth byte of the rare control record, as measured.</summary>
    private const byte ControlFourthByteRare = 0x0e;

    /// <summary>
    /// State blob the sparse phase carries: 18 zero bytes, the <c>0x40</c>
    /// marker, a zero and a big-endian value; the capture's sparse-phase value
    /// is 100, with record fourth byte <c>0x01</c>.
    /// </summary>
    private static ReadOnlySpan<byte> SparseStateBlobBody =>
        [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
         0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x64];

    /// <summary>Steady state blob whose last field is zero.</summary>
    private static ReadOnlySpan<byte> SteadyZeroStateBlobBody =>
        [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
         0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00];

    /// <summary>Fourth byte paired with a zero-valued steady blob.</summary>
    private const byte StateBlobSteadyZeroFourthByte = 0x15;

    /// <summary>Fourth byte paired with a 100-valued steady blob.</summary>
    private const byte StateBlobSteadyLiveFourthByte = 0x01;

    /// <summary>
    /// Body of the ninety-byte slot record carried in the match-start run,
    /// copied from the capture byte for byte.
    /// </summary>
    private static ReadOnlySpan<byte> MatchStartSlotBody =>
    [
        0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x18, 0x18, 0xff, 0xff, 0xff, 0xff, 0x18, 0x18, 0x18,
        0x18, 0x18, 0x00, 0x00, 0x00, 0x00, 0xfe, 0xfe, 0xfe, 0xfe, 0x3c, 0x3c,
        0x3c, 0x3c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0xff, 0x20, 0x0d,
    ];

    /// <summary>Twenty-six-byte join-tagged entry of the match-start run.</summary>
    private static ReadOnlySpan<byte> MatchStartEntryBody =>
    [
        0x85, 0xf3, 0xfe, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
        0xff, 0xff,
    ];

    /// <summary>Twenty-six-byte roster-tagged entry of the match-start run.</summary>
    private static ReadOnlySpan<byte> MatchStartRosterBody =>
    [
        0x86, 0xf3, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f,
        0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f, 0x7f,
        0x7f, 0x7f,
    ];

    /// <summary>First twenty-byte record of the match-start run's close pair.</summary>
    private static ReadOnlySpan<byte> MatchStartCloseFirstBody =>
    [
        0x07, 0x0d, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x02, 0x42,
        0xf5, 0x9f, 0xda, 0xff, 0xc3, 0xec, 0x7d, 0x00, 0x00, 0x00,
    ];

    /// <summary>Second twenty-byte record of the match-start run's close pair.</summary>
    private static ReadOnlySpan<byte> MatchStartCloseSecondBody =>
    [
        0x07, 0x0d, 0x00, 0x02, 0x01, 0x00, 0x00, 0x00, 0x02, 0xf4,
        0x01, 0xc0, 0xea, 0xff, 0x83, 0xaf, 0x02, 0x00, 0x00, 0x00,
    ];

    /// <summary>Six-byte body of the payload record inside the match-start run.</summary>
    private static ReadOnlySpan<byte> MatchStartPayloadBody =>
        [0xe0, 0x14, 0x00, 0x00, 0x00, 0x00];

    /// <summary>One message of the stream, with the fourth byte it travels with.</summary>
    /// <param name="Type">Message identifier.</param>
    /// <param name="Body">Message body.</param>
    /// <param name="FourthByte">Value of the record's fourth byte.</param>
    public sealed record StreamMessage(ushort Type, byte[] Body, byte FourthByte = 0);

    /// <summary>One step of the stream: records to write together, then the wait before the next.</summary>
    /// <param name="DelayMilliseconds">Delay before this step, in milliseconds.</param>
    /// <param name="Records">Records written together as one datagram.</param>
    /// <param name="Compressed">
    /// Whether the datagram goes out LZSS-compressed. The recorded host marks
    /// the state blobs and the match-start run and leaves the one-byte control
    /// records and the steady frames plain, so the marker follows the frame
    /// rather than the peer — the same rule the roster run follows.
    /// </param>
    public sealed record StreamStep(
        int DelayMilliseconds,
        IReadOnlyList<StreamMessage> Records,
        bool Compressed = false);

    /// <summary>
    /// The sparse phase, counted from the roster answer, and the match-start run
    /// that ends it. Each delay is measured between consecutive captured frames,
    /// so a zero means the two went out back to back.
    /// </summary>
    /// <returns>The steps in send order, ending with the match-start run.</returns>
    public static List<StreamStep> SparseSteps() =>
    [
        new(4930, [new(UdpCommandConstants.InGameControl, [.. ControlBodyEarly], 0x02)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(0, [new(UdpCommandConstants.InGameControl, [.. ControlBodyEarly], 0x02)]),
        new(4800, [new(UdpCommandConstants.InGameControl, [.. ControlBodyAlternate], 0x03)]),
        new(2400, [new(UdpCommandConstants.InGameControl, [.. ControlBodyEarly], 0x03)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(2370, [new(UdpCommandConstants.InGameControl, [.. ControlBodyEarly], 0x04)]),
        new(2750, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(2370, [new(UdpCommandConstants.InGameControl, [.. ControlBodyAlternate], 0x05)]),
        new(4750, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(0, [new(UdpCommandConstants.InGameControl, [.. ControlBodyEarly], 0x06)]),
        new(4770, [new(UdpCommandConstants.InGameControl, [.. ControlBodyAlternate], 0x07)]),
        new(110, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(4250, [new(UdpCommandConstants.InGameControl, [.. ControlBodyEarly], 0x08)]),
        new(2490, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(6250, [new(UdpCommandConstants.InGameControl, [.. ControlBodyAlternate], 0x09)]),
        new(0, [new(UdpCommandConstants.GameStateBlob, [.. SparseStateBlobBody], StateBlobFourthByte)], Compressed: true),
        new(0, [new(MatchStartMarker, [])]),
        new(0, MatchStartRun(), Compressed: true),
    ];

    /// <summary>
    /// The frame that opens the round at t+46.4 s: two empty
    /// <see cref="UdpCommandConstants.RosterHead"/> records, the ninety-byte slot
    /// record, the host's own short entries and the close, then the payload
    /// record and the first tick pair.
    /// </summary>
    /// <returns>The records of the run, in the captured order.</returns>
    public static List<StreamMessage> MatchStartRun() =>
    [
        new(UdpCommandConstants.RosterHead, [], 10),
        new(UdpCommandConstants.RosterHead, [], 11),
        new(SlotAlpha, [.. MatchStartSlotBody], 0),
        new(SlotAlpha, [.. SlotAlphaShortBody], 1),
        new(UdpCommandConstants.JoinRequest, [.. MatchStartEntryBody], MatchStartEntryFourthByte),
        new(UdpCommandConstants.PlayerProfile, [.. MatchStartRosterBody], 8),
        new(UdpCommandConstants.PlayerProfile, [.. MatchStartCloseFirstBody], 9),
        new(UdpCommandConstants.PlayerProfile, [.. MatchStartCloseSecondBody], 10),
        new(UdpCommandConstants.PlayerProfile, [.. PlayerProfileRecordUtility.BuildRosterClose()], 11),
        new(SlotAlpha, [.. SlotBody], 2),
        new(MatchStartPayload, [.. MatchStartPayloadBody], 0),
        new(Tick, [.. TickBody], 0),
        new(Tick, [.. TickBody], 1),
    ];

    /// <summary>
    /// The steady frame: the beat record and the tick pair, with an optional
    /// insertion in front of them.
    /// </summary>
    /// <param name="insertion">Record to place at the head of the frame, or null.</param>
    /// <returns>The records of one steady frame.</returns>
    public static List<StreamMessage> SteadyFrame(StreamMessage? insertion = null)
    {
        var frame = new List<StreamMessage>(insertion is null ? 3 : 4);
        if (insertion is not null)
        {
            frame.Add(insertion);
        }

        frame.Add(new(Beat, [.. BeatBody], BeatFourthByte));
        frame.Add(new(Tick, [.. TickBody], TickFourthByteFirst));
        frame.Add(new(Tick, [.. TickBody], TickFourthByteSecond));
        return frame;
    }

    /// <summary>The slot insertion both slot identifiers carry.</summary>
    /// <param name="beta">True for <see cref="SlotBeta"/>, false for <see cref="SlotAlpha"/>.</param>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <returns>The insertion record.</returns>
    public static StreamMessage SlotInsertion(bool beta, byte fourthByte) =>
        new(beta ? SlotBeta : SlotAlpha, [.. SlotBody], fourthByte);

    /// <summary>A state-blob insertion from the recorded steady stream.</summary>
    /// <param name="live">True for the captured 100-valued variant.</param>
    /// <returns>The insertion record.</returns>
    public static StreamMessage StateBlobInsertion(bool live) =>
        live
            ? new(
                UdpCommandConstants.GameStateBlob,
                [.. SparseStateBlobBody],
                StateBlobSteadyLiveFourthByte)
            : new(
                UdpCommandConstants.GameStateBlob,
                [.. SteadyZeroStateBlobBody],
                StateBlobSteadyZeroFourthByte);

    /// <summary>A control-byte insertion.</summary>
    /// <param name="body">Control body to carry.</param>
    /// <param name="fourthByte">Value of the record's fourth byte.</param>
    /// <returns>The insertion record.</returns>
    public static StreamMessage ControlInsertion(byte[] body, byte fourthByte) =>
        new(UdpCommandConstants.InGameControl, body, fourthByte);

    /// <summary>Body of the early sparse-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] EarlyControlBody() => [.. ControlBodyEarly];

    /// <summary>Body of the alternating sparse-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] AlternateControlBody() => [.. ControlBodyAlternate];

    /// <summary>Body of the steady-phase control record.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] SteadyControlBody() => [.. ControlBodySteady];

    /// <summary>Body of the control record seen once in the steady phase.</summary>
    /// <returns>The body bytes.</returns>
    public static byte[] RareControlBody() => [.. ControlBodyRare];

    /// <summary>Fourth byte of the early sparse-phase control record.</summary>
    public const byte EarlyControlFourthByte = ControlFourthByteEarly;

    /// <summary>Fourth byte of the alternating sparse-phase control record.</summary>
    public const byte AlternateControlFourthByte = ControlFourthByteAlternate;

    /// <summary>Fourth byte of the rare control record.</summary>
    public const byte RareControlFourthByte = ControlFourthByteRare;

    /// <summary>Converts one stream message into the codec's message type.</summary>
    /// <param name="message">Message to convert.</param>
    /// <returns>The message the frame builder serializes.</returns>
    public static UdpMessage ToUdpMessage(StreamMessage message) =>
        FrameBuilderUtility.MessageOf(message.Type, message.Body, message.FourthByte);
}
