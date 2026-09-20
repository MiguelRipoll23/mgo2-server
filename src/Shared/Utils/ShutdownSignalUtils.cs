using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Utils;

/// <summary>
/// Turns the two stops a server is asked for into one call: the interrupt a
/// person sends from a terminal, and the SIGTERM a deployment sends.
/// <para>
/// SIGTERM is the one that matters during a rollout, and it is the one a console
/// application is given nothing for by default. The runtime terminates the
/// process when it arrives, which drops every session that was being served —
/// no matter how long the pod's termination grace period is, because the process
/// is gone long before it. Registering it is what lets the server close its
/// listener and wait for the connections it still has.
/// </para>
/// </summary>
public static class ShutdownSignalUtils
{
    /// <summary>
    /// Requests a stop when the process is interrupted from a terminal or asked
    /// to terminate by the platform it runs on.
    /// </summary>
    /// <param name="requestStop">Called once per signal that arrives.</param>
    /// <param name="logger">Logger the request is reported to.</param>
    /// <returns>Registration that releases both handlers when it is disposed.</returns>
    public static IDisposable OnStopRequested(Action requestStop, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(requestStop);
        ArgumentNullException.ThrowIfNull(logger);

        void Request(string signal)
        {
            logger.LogInformation("Shutdown requested ({Signal})", signal);
            requestStop();
        }

        ConsoleCancelEventHandler interruptHandler = (_, eventArguments) =>
        {
            // The interrupt is not allowed to end the process on the spot: the
            // request has to reach the listener, and the connections it is
            // serving have to be waited for.
            eventArguments.Cancel = true;
            Request("interrupt");
        };

        Console.CancelKeyPress += interruptHandler;

        var termination = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            context =>
            {
                // Defaulted for the same reason, and this is the one a container
                // sends: the process ends when its connections have left, not
                // while it still has them.
                context.Cancel = true;
                Request("SIGTERM");
            });

        return new SignalRegistration(interruptHandler, termination);
    }

    /// <summary>Releases the handlers a registration installed.</summary>
    /// <param name="interruptHandler">Handler subscribed to the console interrupts.</param>
    /// <param name="termination">Registration of the termination signal.</param>
    private sealed class SignalRegistration(
        ConsoleCancelEventHandler interruptHandler,
        PosixSignalRegistration termination) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            Console.CancelKeyPress -= interruptHandler;
            termination.Dispose();
        }
    }
}
