using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Runs one operation on a fixed interval until it is stopped. The first run
/// happens immediately, so a worker that keeps a row alive registers it before
/// the first interval elapses. A failing run is logged and the loop continues,
/// because a database that is briefly unavailable must not end the process.
/// </summary>
/// <param name="interval">Time between two runs.</param>
/// <param name="logger">Logger of the worker.</param>
public abstract class PeriodicWorker(TimeSpan interval, ILogger logger)
{
    private CancellationTokenSource? cancellation;
    private Task? loop;

    /// <summary>Whether the worker is running.</summary>
    public bool IsRunning => loop is not null;

    /// <summary>Starts the loop, or does nothing when it already runs.</summary>
    public void Start()
    {
        if (loop is not null)
        {
            return;
        }

        cancellation = new CancellationTokenSource();
        loop = RunAsync(cancellation.Token);
    }

    /// <summary>Stops the loop and waits for the run that is in flight.</summary>
    public async Task StopAsync()
    {
        if (cancellation is null || loop is null)
        {
            return;
        }

        await cancellation.CancelAsync();

        try
        {
            await loop;
        }
        catch (OperationCanceledException)
        {
            // The loop was cancelled, which is how it stops.
        }

        cancellation.Dispose();
        cancellation = null;
        loop = null;
    }

    /// <summary>Performs one run of the worker.</summary>
    /// <param name="cancellationToken">Token that stops the run.</param>
    protected abstract Task RunOnceAsync(CancellationToken cancellationToken);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunOnceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "{Worker} run failed", GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }
}
