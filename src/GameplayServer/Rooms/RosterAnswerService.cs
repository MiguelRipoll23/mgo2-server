using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Decides which copy of a peer's profile is answered: the copy the client
/// re-sends, in the pass that received it — or the first copy once the client's
/// own retry interval has passed without one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The recorded host answers the re-send.</b> Measured: the joiner of
/// `mgo2-game1` sends its profile twice, 1.934 s apart, and the host's roster run
/// follows the second copy by 0.542 s (`docs/protocol/UDP_JOIN_FLOW.md` §7);
/// `mgo2-started-on-solo-game` and `mgo2-game-started-on-briefing` show the same
/// shape, their copies 2.11 s and 2.05 s apart with the roster run behind the
/// second (`docs/analysis/join-stall-after-mode2.md`). A host that answers the
/// first copy satisfies the client before it re-sends, which is what this host
/// did for both of its live captures and neither of those clients re-sent at all.
/// </para>
/// <para>
/// <b>A held copy is never left unanswered.</b> The hold is a deadline past the
/// interval the captures measure (1.93 to 2.11 s): a client that re-sends is
/// answered in the pass its re-send arrives in, and a client that does not is
/// answered when the deadline passes, from the context of the copy that armed it.
/// The second path is a floor rather than a measured behaviour — every captured
/// client re-sends — and it exists so that a client which does not can still be
/// answered.
/// </para>
/// </remarks>
/// <param name="logger">Logger of this service.</param>
public sealed class RosterAnswerService(ILogger<RosterAnswerService> logger)
{
    /// <summary>
    /// The interval the captures measure between a peer's profile and the copy it
    /// re-sends: 1.934 s in `mgo2-game1`, whose own re-dial uses the same timer.
    /// </summary>
    public static readonly TimeSpan ClientRetryInterval = TimeSpan.FromMilliseconds(1930);

    /// <summary>
    /// How long a held profile waits before it is answered anyway: the retry
    /// interval plus the longest latency a captured answer shows behind a re-send
    /// (0.542 s), so the hold never fires while a copy is still in flight.
    /// </summary>
    public static readonly TimeSpan HoldDeadline = ClientRetryInterval + TimeSpan.FromMilliseconds(570);

    private readonly Dictionary<string, CancellationTokenSource> held = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Whether the profile that has just arrived is the copy to answer now, and
    /// holds it when it is the first.
    /// </summary>
    /// <param name="key">Endpoint the peer is filed under.</param>
    /// <param name="context">Context of the profile that has just arrived.</param>
    /// <param name="answer">
    /// The answer to write, called in this pass for a re-send and from the held
    /// copy's context when the deadline passes.
    /// </param>
    /// <returns><c>true</c> when the caller should answer in this pass.</returns>
    public bool AnswerNowOrHold(string key, PeerContext context, Func<Task> answer)
    {
        CancellationTokenSource cancellation;
        lock (gate)
        {
            if (held.Remove(key, out var previous))
            {
                // The client's re-send, which is the copy the captures answer and
                // the one the held deadline no longer owns.
                previous.Cancel();
                previous.Dispose();

                logger.LogDebug(
                    "UDP {LocalPort}: {RemoteAddress} has re-sent its profile; the roster is answered in this pass, "
                    + "as the recorded host answers it",
                    context.LocalPort,
                    context.Remote);
                return true;
            }

            cancellation = new CancellationTokenSource();
            held[key] = cancellation;
        }

        logger.LogDebug(
            "UDP {LocalPort}: the first copy of {RemoteAddress}'s profile is held for {HoldDeadline}; the "
            + "recorded host answers the copy the client re-sends",
            context.LocalPort,
            context.Remote,
            HoldDeadline);

        _ = AnswerWhenHeldAsync(key, context, answer, cancellation);
        return false;
    }

    /// <summary>
    /// Answers a held profile once the hold's deadline passes, unless the client's
    /// re-send took it first.
    /// </summary>
    /// <param name="key">Endpoint the peer is filed under.</param>
    /// <param name="context">Context of the held copy.</param>
    /// <param name="answer">The answer to write.</param>
    /// <param name="cancellation">Token the hold is cancelled with.</param>
    private async Task AnswerWhenHeldAsync(
        string key,
        PeerContext context,
        Func<Task> answer,
        CancellationTokenSource cancellation)
    {
        try
        {
            try
            {
                await Task.Delay(HoldDeadline, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            lock (gate)
            {
                if (!held.Remove(key))
                {
                    return;
                }
            }

            logger.LogInformation(
                "UDP {LocalPort}: {RemoteAddress} has not re-sent its profile in {HoldDeadline}; answering the "
                + "copy it first sent, because a client is answered either way",
                context.LocalPort,
                context.Remote,
                HoldDeadline);

            await answer();
        }
        catch (Exception exception)
        {
            // A failed send here says the peer is gone, which the session reaper
            // is the one to act on; the hold has nothing left to answer either way.
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the held roster answer to {RemoteAddress} ended without being written",
                context.LocalPort,
                context.Remote);
        }
        finally
        {
            cancellation.Dispose();
        }
    }
}
