using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Sends the post-join burst the recorded host writes once, a measured pause
/// after it has answered a joiner with the roster.
/// </summary>
/// <remarks>
/// <para>
/// The capture holds exactly one burst in the whole round — t+4.9929, outbound
/// counter 5, six records, 2.52 s after the roster answer — and the peers that
/// join at t+154, t+385 and t+493 do not each get one. So it is scheduled on
/// the first roster answer and never again
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4).
/// </para>
/// <para>
/// The send is not awaited. The pause is longer than the accept path should hold
/// the dispatch loop open, and a host that blocked on it would stop reading every
/// other peer for two and a half seconds. The counter is still the session's:
/// the burst goes out on the sequence after the roster trailer because nothing
/// else is written in between, which is what the capture shows.
/// </para>
/// </remarks>
/// <param name="burst">Builder of the burst and the control record that follows it.</param>
/// <param name="logger">Logger of this scheduler.</param>
public sealed class PostJoinBurstSchedulerService(
    PostJoinBurstService burst,
    ILogger<PostJoinBurstSchedulerService> logger)
{
    /// <summary>
    /// Pause the recorded host leaves between the roster answer and the burst,
    /// measured at 2.52 s on outbound counter 5.
    /// </summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(2520);

    /// <summary>Set once the burst has been scheduled, so it leaves one time.</summary>
    private int scheduled;

    /// <summary>
    /// Schedules the burst for the peer the roster was just sent to, once per
    /// host.
    /// </summary>
    /// <param name="context">Context the roster was answered through.</param>
    public void Schedule(PeerContext context)
    {
        if (Interlocked.Exchange(ref scheduled, 1) != 0)
        {
            return;
        }

        _ = SendAsync(context);
    }

    private async Task SendAsync(PeerContext context)
    {
        await Task.Delay(Delay);

        logger.LogDebug(
            "UDP {LocalPort}: sending the post-join burst to {RemoteAddress} after the roster pause",
            context.LocalPort,
            context.Remote);

        await RosterRunSendUtils.SendAsync(context, burst.BuildBurst(), compressed: true);

        var followUp = PostJoinBurstService.BuildFollowUp();
        await context.Send(followUp.Type, followUp.Body, 0);
    }
}
