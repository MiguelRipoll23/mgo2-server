using System.Runtime.CompilerServices;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// The age at which a peer's unanswered records are written again, and how a timed answer
/// shortens it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The default is a measurement, not a preference.</b> The recorded host's copies of a delta
/// are 0.6 s apart, and <see cref="DefaultIntervalMilliseconds"/> is the flat form of that: a
/// record the peer has not answered within it is written again from the hole it sits in.
/// </para>
/// <para>
/// <b>The shortening is <c>decode_p2p_datagram</c>'s <c>0x8000</c> answer branch.</b> A timed
/// answer carries the peer's duration as one body byte and leaves a margin when it is subtracted
/// from the elapsed time (<see cref="TimedExchangeService"/>). A margin of
/// <see cref="TimedExchangeService.FastResendMilliseconds"/> or less is a peer that is keeping
/// up, and nothing changes. Above it the image either sets the session's resend flag
/// (<c>session[5] |= 0x20</c>, while the margin is under
/// <see cref="TimedExchangeService.IntervalChangeCeilingMilliseconds"/>) or shrinks the interval
/// at <c>session[0x24]</c> by two <c>750/1000</c> steps, clamped to <c>16 000</c> of the time
/// base's unit. The flag becomes <see cref="FastIntervalMilliseconds"/>, which is the cadence the
/// receive path already re-sends at, and the shrink is the same two steps floored at
/// <see cref="TimedExchangeService.MinimumIntervalMilliseconds"/>.
/// </para>
/// <para>
/// Nothing grows the interval back: the image only ever shortens, so a peer that falls behind
/// once keeps the faster cadence for the rest of the session.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class RetransmitPacingService(ILogger<RetransmitPacingService> logger)
{
    /// <summary>Age at which a held record is written again, in milliseconds.</summary>
    public const int DefaultIntervalMilliseconds = 700;

    /// <summary>
    /// The faster cadence a margin under the ceiling asks for, which is the receive path's own
    /// age: one frame's worth.
    /// </summary>
    public const int FastIntervalMilliseconds = 50;

    /// <summary>One peer's age-based re-send interval.</summary>
    private sealed class Pacing
    {
        /// <summary>Age at which a held record is written again, in milliseconds.</summary>
        public int IntervalMilliseconds { get; set; } = DefaultIntervalMilliseconds;
    }

    /// <summary>Cadences by peer, held weakly so a peer that is gone takes its cadence with it.</summary>
    private readonly ConditionalWeakTable<PeerSession, Pacing> peers = [];

    private readonly Lock gate = new();

    /// <summary>
    /// The age at which this peer's held records are written again.
    /// </summary>
    /// <param name="session">Peer being written to.</param>
    /// <returns>Milliseconds since a held record's last write that make it due.</returns>
    public int IntervalFor(PeerSession session) => peers.GetOrCreateValue(session).IntervalMilliseconds;

    /// <summary>
    /// Shortens this peer's interval from the margin its timed answer reported.
    /// </summary>
    /// <param name="session">Peer the answer came from.</param>
    /// <param name="remainderMilliseconds">Elapsed milliseconds less the answer's own byte.</param>
    public void PaceFromRemainder(PeerSession session, int remainderMilliseconds)
    {
        if (remainderMilliseconds <= TimedExchangeService.FastResendMilliseconds)
        {
            return;
        }

        var pacing = peers.GetOrCreateValue(session);
        lock (gate)
        {
            if (remainderMilliseconds < TimedExchangeService.IntervalChangeCeilingMilliseconds)
            {
                pacing.IntervalMilliseconds = FastIntervalMilliseconds;
            }
            else
            {
                var step = pacing.IntervalMilliseconds * 750 / 1000;
                step = step * 750 / 1000;
                pacing.IntervalMilliseconds = Math.Max(step, TimedExchangeService.MinimumIntervalMilliseconds);
            }

            logger.LogDebug(
                "Peer {RemoteAddress} left a timed margin of {Margin} ms; its re-send age is now {Interval} ms",
                session.RemoteAddress,
                remainderMilliseconds,
                pacing.IntervalMilliseconds);
        }
    }
}
