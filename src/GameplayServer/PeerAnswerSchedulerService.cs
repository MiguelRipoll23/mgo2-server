using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer;

/// <summary>
/// Sends the answers a peer's numbered records are owed, a measured beat after
/// the frame that carried the records rather than inside its dispatch.
/// </summary>
/// <remarks>
/// <para>
/// The recorded host never answers a record in the same pass that received it.
/// Over the reference round (<c>mgo2-game1.pcapng</c>, joiner
/// <c>10.2.0.2:5730</c> ↔ host <c>99.66.131.177:5731</c>) 2 801 answers pair
/// with the record they answer, and the gap runs from <b>13 ms</b> at its
/// shortest, 334 ms at the tenth percentile and 1.1 s at the median: a console
/// host dequeue, act and write on its next tick, so not one answer lands in the
/// millisecond its record arrived. Answers served from inside the receive — the
/// shape two live joins against this server had — are therefore outside the
/// only envelope a peer has ever been shown, and a client whose bookkeeping for
/// the record it built appears a pass later has nothing to match them to.
/// </para>
/// <para>
/// The pause is not awaited. It is longer than the dispatch loop should hold,
/// and a host that blocked on it would stop reading every other peer of the
/// room; the counter is still the session's, so the answer goes out on the
/// sequence after whatever the host wrote meanwhile, exactly as the recorded
/// host's does.
/// </para>
/// <para>
/// The value is the capture's tenth percentile rounded down, chosen to sit
/// inside the measured envelope rather than at its edge. It is one measurement,
/// not a rule, and what settles it is a live join.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this scheduler.</param>
public sealed class PeerAnswerSchedulerService(ILogger<PeerAnswerSchedulerService> logger)
{
    /// <summary>
    /// Pause between receiving a numbered record and writing its answer, set
    /// just under the reference's tenth percentile (334 ms).
    /// </summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Schedules the answers to the numbered records of one frame.
    /// </summary>
    /// <param name="session">Session the records arrived on.</param>
    /// <param name="answers">Answers the frame's records are owed, in order.</param>
    /// <param name="send">Writer of one run of messages for that session.</param>
    public void Schedule(
        PeerSession session,
        IReadOnlyList<UdpMessage> answers,
        Action<PeerSession, IReadOnlyList<UdpMessage>> send)
    {
        if (answers.Count == 0)
        {
            return;
        }

        logger.LogDebug(
            "UDP {RemoteAddress}: scheduling {AnswerCount} answers after {Delay}",
            session.RemoteAddress,
            answers.Count,
            Delay);
        _ = SendAsync(session, answers, send);
    }

    private async Task SendAsync(
        PeerSession session,
        IReadOnlyList<UdpMessage> answers,
        Action<PeerSession, IReadOnlyList<UdpMessage>> send)
    {
        try
        {
            await Task.Delay(Delay);
            send(session, answers);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "UDP {RemoteAddress}: paced answers were not sent",
                session.RemoteAddress);
        }
    }
}
