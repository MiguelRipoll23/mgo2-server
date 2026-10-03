using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Arithmetic for the frame counter the peer-to-peer header carries.
/// </summary>
/// <remarks>
/// <para>
/// The header is a little-endian <c>u16</c> made of a compression flag in bit
/// 15 and a <b>fifteen-bit</b> monotonic counter in bits 14-0. It is not a
/// sixteen-bit counter: masking with <c>0xffff</c> lets the counter reach
/// <c>0x8000</c>, and bit 15 is then read by the peer as the compression marker
/// on a frame that is not compressed.
/// </para>
/// <para>
/// Measured over a live round of 19 018 datagrams, the counter steps by one on
/// 99.4% of consecutive server-to-client pairs and on 100% of the other
/// direction, and it never wraps within the round
/// (docs/protocol/UDP_GAME_CAPTURE.md §2). It does wrap in a longer session, so
/// ordering has to survive the wrap: that is what <see cref="IsNewer"/> is for.
/// </para>
/// </remarks>
public static class FrameCounterUtility
{
    /// <summary>
    /// Width of the counter: everything below the compression flag.
    /// </summary>
    public const ushort ValueMask = UdpCommandConstants.CounterMask;

    /// <summary>
    /// Half the counter's range, the point beyond which a distance can no longer
    /// mean "later" — a fifteen-bit counter is unambiguous over a window of
    /// 16 384 frames, and an older sequence is closer to that than a newer one.
    /// </summary>
    private const ushort HalfRange = 0x4000;

    /// <summary>Reads the counter out of a raw header word.</summary>
    /// <param name="rawCounter">Header word as it came off the wire.</param>
    /// <returns>The counter, with the compression flag removed.</returns>
    public static ushort Value(ushort rawCounter) => (ushort)(rawCounter & ValueMask);

    /// <summary>Advances the counter, wrapping at the top of its range.</summary>
    /// <param name="current">Current counter.</param>
    /// <returns>The next counter, always inside the fifteen-bit range.</returns>
    public static ushort Next(ushort current) => (ushort)((current + 1) & ValueMask);

    /// <summary>
    /// Whether <paramref name="candidate"/> is a later sequence than
    /// <paramref name="reference"/>, counting forwards across the wrap.
    /// </summary>
    /// <remarks>
    /// A plain <c>&gt;</c> is wrong once the counter has wrapped: after 32 767
    /// the next value is 0, which is not greater than what came before, and every
    /// later frame would read as stale. Measuring the forward distance and
    /// requiring it to fall inside the first half of the range handles the wrap
    /// without needing to know how many times it has gone round.
    /// </remarks>
    /// <param name="candidate">Sequence to test.</param>
    /// <param name="reference">Sequence to compare against.</param>
    /// <returns>True when the candidate is later.</returns>
    public static bool IsNewer(ushort candidate, ushort reference)
    {
        var distance = (ushort)((candidate - reference) & ValueMask);
        return distance != 0 && distance < HalfRange;
    }
}