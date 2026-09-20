using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Waits for the connections an instance is still serving to leave before it
/// stops.
/// <para>
/// A stop closes the listener and takes nobody new; the connections that are
/// still open are the players who are in the middle of something, and they are
/// kept until they leave on their own. This is what a rollout waits for, so a
/// deployment does not disconnect the lobby it is replacing.
/// </para>
/// <para>
/// The wait has no deadline of its own. The deployment gives a pod a termination
/// grace period, which is the limit that turns a drain nobody ever finishes into
/// a stop; a second limit here could only be the smaller one, and it would be
/// the one that hangs up on the players this exists to keep.
/// </para>
/// </summary>
public static class ConnectionDrainUtils
{
    /// <summary>Interval between two readings of the connection count.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Interval between two progress reports, so a drain that lasts hours is
    /// visible while it is happening rather than only when it ends.
    /// </summary>
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Waits until nothing is connected any more.
    /// </summary>
    /// <param name="subject">What is being waited for, named in the log lines.</param>
    /// <param name="liveCount">Reads how many connections are still being served.</param>
    /// <param name="logger">Logger the progress and the outcome are written to.</param>
    /// <param name="cancellationToken">Token that abandons the wait.</param>
    /// <returns><c>true</c> when every connection had left, <c>false</c> when the wait was abandoned.</returns>
    public static async Task<bool> WaitForConnectionsToLeaveAsync(
        string subject,
        Func<int> liveCount,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveCount);
        ArgumentNullException.ThrowIfNull(logger);

        var started = DateTimeOffset.UtcNow;
        if (liveCount() <= 0)
        {
            // The ordinary case for a lobby that was rolled out while it was
            // empty, and the one that must not wait at all.
            logger.LogInformation("[{Subject}] Nobody is connected; stopping at once", subject);
            return true;
        }

        logger.LogInformation(
            "[{Subject}] Waiting for {Count} connections to leave before stopping",
            subject,
            liveCount());

        var lastReport = DateTimeOffset.UtcNow;
        while (true)
        {
            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "[{Subject}] Gave up waiting for {Count} connections to leave",
                    subject,
                    liveCount());
                return false;
            }

            var remaining = liveCount();
            if (remaining <= 0)
            {
                logger.LogInformation(
                    "[{Subject}] Last connection left after {Elapsed}; stopping",
                    subject,
                    FormatElapsed(DateTimeOffset.UtcNow - started));
                return true;
            }

            if (DateTimeOffset.UtcNow - lastReport >= ReportInterval)
            {
                lastReport = DateTimeOffset.UtcNow;
                logger.LogInformation(
                    "[{Subject}] {Count} connections still being served after {Elapsed}",
                    subject,
                    remaining,
                    FormatElapsed(DateTimeOffset.UtcNow - started));
            }
        }
    }

    /// <summary>Formats a drain's duration for the log lines.</summary>
    /// <param name="elapsed">How long the drain has lasted so far.</param>
    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
        {
            return $"{elapsed.TotalHours:0.#}h";
        }

        return elapsed.TotalMinutes >= 1
            ? $"{elapsed.TotalMinutes:0.#}m"
            : $"{elapsed.TotalSeconds:0.#}s";
    }
}
