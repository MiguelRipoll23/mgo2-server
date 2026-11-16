using System.Runtime.CompilerServices;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// Measures the exchange a record carrying the <see cref="RecordClassBits.Timed"/> class bit
/// opens, and reports the margin its answer leaves.
/// </summary>
/// <remarks>
/// <para>
/// <b>The measurement is the image's, from <c>decode_p2p_datagram</c> (<c>0x2666c8</c>).</b>
/// A record carrying <c>0x8000</c> is stamped with the time base when it is written
/// (<c>P2P_ReadTimeBaseInto(session + 0x18)</c>), and its fourth byte is held beside the
/// stamp. The answer to it — the same identifier with <c>0x4000</c> set, the same fourth byte,
/// a one-byte body that is not <c>0xff</c> — closes the exchange:
/// <c>P2P_ElapsedMillisecondsSince(session + 0x18)</c> gives the elapsed milliseconds, the
/// answer's byte is subtracted from them, and the remainder is what the peer's margin is read
/// from. A remainder over <c>600</c> is the signal that the peer is behind, and
/// <see cref="StreamRetransmitService.PaceFromRemainder"/> is where it is acted on.
/// </para>
/// <para>
/// The answer's byte is therefore a duration in milliseconds: it is subtracted from one, which
/// is the only reading that branch leaves.
/// </para>
/// <para>
/// The image stamps a timed record only while its session tick is under <c>200</c>
/// (<c>P2P_SessionTickNumber</c>, <c>0x261600</c>). The analogue here is the session's inbound
/// frame counter, which is the same "how early in this session" quantity.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class TimedExchangeService(ILogger<TimedExchangeService> logger)
{
    /// <summary>
    /// Frames a session must be inside for its timed records to be stamped, which is
    /// <c>P2P_SessionTickNumber() &lt; 200</c> in the image.
    /// </summary>
    public const int MeasurementWindowFrames = 200;

    /// <summary>
    /// Remainder, in milliseconds, above which the peer is behind and its stream is re-sent to
    /// faster: <c>600</c> in the image.
    /// </summary>
    public const int FastResendMilliseconds = 600;

    /// <summary>
    /// Remainder below which only the session's flag is set rather than its interval changed,
    /// which is the image's <c>0x3e9</c>.
    /// </summary>
    public const int IntervalChangeCeilingMilliseconds = 0x3e9;

    /// <summary>
    /// Floor of the paced interval. The image clamps to <c>16 000</c> in the time base's own
    /// unit, which is microseconds.
    /// </summary>
    public const int MinimumIntervalMilliseconds = 16;

    /// <summary>A record this host wrote that is waiting for its timed answer.</summary>
    private sealed class Timed
    {
        /// <summary>Fourth byte of the record, which its answer repeats.</summary>
        public byte FourthByte { get; set; }

        /// <summary>Instant of the write, in milliseconds since boot.</summary>
        public long WrittenAt { get; set; }
    }

    /// <summary>
    /// The exchange each peer's last timed write opened. Held weakly, so a peer that is gone
    /// takes its stamp with it.
    /// </summary>
    private readonly ConditionalWeakTable<PeerSession, Timed> pending = [];

    /// <summary>
    /// Stamps the timed records of one write, which is where their round trip starts.
    /// </summary>
    /// <param name="session">Peer the records were written to.</param>
    /// <param name="messages">Records of the write, in order.</param>
    public void NoteWritten(PeerSession session, IReadOnlyList<UdpMessage> messages)
    {
        if (session.LastInboundSequence >= MeasurementWindowFrames)
        {
            return;
        }

        foreach (var message in messages)
        {
            if (!CarriesTiming(message) || !PeerAcknowledgementUtils.WaitsForAcknowledgement(message))
            {
                continue;
            }

            var timed = pending.GetOrCreateValue(session);
            timed.FourthByte = message.Flags;
            timed.WrittenAt = Environment.TickCount64;
        }
    }

    /// <summary>
    /// Measures a timed answer and returns the peer's margin, or <c>null</c> when the record
    /// does not close one.
    /// </summary>
    /// <remarks>
    /// Every guard is the image's: the answer carries the timed class, its body is one byte,
    /// that byte is not <c>0xff</c>, and it repeats the fourth byte of the record it answers.
    /// </remarks>
    /// <param name="session">Peer that answered.</param>
    /// <param name="answer">The answer, as it arrived.</param>
    /// <returns>The elapsed milliseconds less the answer's byte, or <c>null</c>.</returns>
    public int? NoteAnswer(PeerSession session, UdpMessage answer)
    {
        if (!CarriesTiming(answer) || answer.Body.Length != 1 || answer.Body[0] == 0xff)
        {
            return null;
        }

        if (!pending.TryGetValue(session, out var timed) || timed.FourthByte != answer.Flags)
        {
            return null;
        }

        var elapsed = (int)(Environment.TickCount64 - timed.WrittenAt);
        var margin = elapsed - answer.Body[0];
        logger.LogDebug(
            "Timed answer 0x{AnswerType:x4} ordinal 0x{Ordinal:x2} arrived after {Elapsed} ms and carries "
            + "{Duration} ms, leaving a margin of {Margin} ms",
            answer.Type,
            answer.Flags,
            elapsed,
            answer.Body[0],
            margin);
        return margin;
    }

    /// <summary>
    /// Whether a record carries the timed class bit.
    /// </summary>
    /// <param name="message">Record to inspect.</param>
    /// <returns><c>true</c> when it opens or closes a timed exchange.</returns>
    private static bool CarriesTiming(UdpMessage message) =>
        (message.Type & (ushort)RecordClassBits.Timed) != 0;
}
