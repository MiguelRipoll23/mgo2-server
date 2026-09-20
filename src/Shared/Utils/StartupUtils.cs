using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Runs the database work a server does before it serves anybody.
/// <para>
/// A failure here costs more than a failed query. The exception ends the process,
/// and the platform starts another one that repeats the whole startup — its
/// just-in-time compilation and the model it builds — only to fail again, on
/// every instance at once, which is a node's worth of CPU spent learning what one
/// attempt already knew. A few attempts inside the process ride out a database
/// that is briefly away, and leave a restart for a failure that is real.
/// </para>
/// </summary>
public static class StartupUtils
{
    /// <summary>Attempts a startup step is given, the first one included.</summary>
    private const int Attempts = 5;

    /// <summary>Wait before the second attempt, doubled after every failure.</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);

    /// <summary>Longest the wait between two attempts grows to.</summary>
    private static readonly TimeSpan DelayCeiling = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Runs a startup step, retrying it a few times with a growing wait before the
    /// failure is allowed to end the process.
    /// </summary>
    /// <typeparam name="T">Result of the step.</typeparam>
    /// <param name="subject">Step being run, named in the log lines.</param>
    /// <param name="step">The step itself.</param>
    /// <param name="logger">Logger the attempts are reported to.</param>
    /// <param name="cancellationToken">Token that stops the startup.</param>
    /// <param name="initialDelay">
    /// Wait before the second attempt, doubled after every failure. The default
    /// suits a server starting against a database that is restarting beside it; a
    /// caller that can wait longer, or cannot wait at all, states its own.
    /// </param>
    /// <exception cref="Exception">The failure of the last attempt, when every attempt fails.</exception>
    public static async Task<T> RetryAsync<T>(
        string subject,
        Func<CancellationToken, Task<T>> step,
        ILogger logger,
        CancellationToken cancellationToken = default,
        TimeSpan? initialDelay = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(logger);

        var delay = initialDelay ?? InitialDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await step(cancellationToken);
            }
            catch (Exception exception)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // A step that failed because the process is stopping is the
                    // stop, not a failure of the step: reporting it as cancellation
                    // is what lets the caller treat it as the normal shutdown it is.
                    throw new OperationCanceledException(
                        $"The startup stopped while '{subject}' was failing.",
                        exception,
                        cancellationToken);
                }

                if (attempt >= Attempts)
                {
                    throw;
                }

                logger.LogWarning(
                    exception,
                    "Startup step '{Subject}' failed on attempt {Attempt} of {Attempts}; trying again in {Delay}s",
                    subject,
                    attempt,
                    Attempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                delay = delay + delay > DelayCeiling ? DelayCeiling : delay + delay;
            }
        }
    }

    /// <summary>
    /// Runs a startup step that produces nothing, with the same attempts as the
    /// overload above.
    /// </summary>
    /// <param name="subject">Step being run, named in the log lines.</param>
    /// <param name="step">The step itself.</param>
    /// <param name="logger">Logger the attempts are reported to.</param>
    /// <param name="cancellationToken">Token that stops the startup.</param>
    /// <param name="initialDelay">Wait before the second attempt, doubled after every failure.</param>
    /// <exception cref="Exception">The failure of the last attempt, when every attempt fails.</exception>
    public static Task RetryAsync(
        string subject,
        Func<CancellationToken, Task> step,
        ILogger logger,
        CancellationToken cancellationToken = default,
        TimeSpan? initialDelay = null) =>
        RetryAsync(
            subject,
            async token =>
            {
                await step(token);
                return true;
            },
            logger,
            cancellationToken,
            initialDelay);

    /// <summary>Runs a startup step whose failure must not stop the server.</summary>
    /// <param name="subject">Step being run, named in the log lines.</param>
    /// <param name="step">The step itself.</param>
    /// <param name="logger">Logger the failure is reported to.</param>
    /// <param name="cancellationToken">Token that stops the startup.</param>
    public static async Task BestEffortAsync(
        string subject,
        Func<CancellationToken, Task> step,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            await step(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // What is published here is a figure for the dashboards. A figure that
            // could not be published is worth a line, not a server that refuses to
            // start while the players of its lobby are waiting.
            logger.LogWarning(
                exception,
                "Startup step '{Subject}' failed; the server starts without it",
                subject);
        }
    }
}
