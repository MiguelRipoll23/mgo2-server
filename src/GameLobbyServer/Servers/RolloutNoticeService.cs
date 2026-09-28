using Mgo2Server.Shared.Domain.News;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Servers;

/// <summary>
/// Tells the players of this lobby that the instance they are connected to is
/// being replaced by a rollout, and asks them to reconnect once it has.
/// <para>
/// A rollout disconnects nobody by itself: the listener closes, the players in
/// the lobby are waited for, and one who leaves is a client the rollout never had
/// to drop. What none of them can see is that the lobby is on its way out at all,
/// so a player whose connection ends — through their own network, through a
/// restart of their client, through the wait finally ending — has no reason to
/// come back to a lobby they think they were thrown out of. The notice is what
/// gives them one: it names the rollout, says how long it takes to reach them,
/// and tells them to reconnect rather than to stop playing.
/// </para>
/// <para>
/// It is written by the lobby itself rather than asked for from the coordinator,
/// because the players it addresses are the ones this process is still holding,
/// and because a lobby whose coordination stream is down is exactly a lobby that
/// can still be rolled out.
/// </para>
/// </summary>
/// <param name="flashNewsService">Service that writes a ticker announcement to this lobby's clients.</param>
/// <param name="logger">Logger of the notice.</param>
public sealed class RolloutNoticeService(
    FlashNewsService flashNewsService,
    ILogger<RolloutNoticeService> logger)
{
    /// <summary>
    /// Hours a rollout takes to disconnect the players of a lobby, which is the
    /// termination grace period every deployment in <c>deploy/</c> gives a pod
    /// (<c>terminationGracePeriodSeconds: 21600</c>). That period is the only
    /// limit a drain has, so it is the honest answer to how much time a player
    /// has left in this lobby.
    /// </summary>
    private const int RolloutHours = 6;

    /// <summary>What the players of a draining lobby are told.</summary>
    public static readonly string Message =
        $"Server rollout in progress: players will be disconnected in {RolloutHours} hours. " +
        "If you are disconnected, reconnect to keep playing.";

    private volatile bool platformRequestedStop;

    /// <summary>
    /// Records that the platform asked this instance to stop, which is what a
    /// rollout is. An interrupt from a terminal is not one, so a lobby stopped by
    /// hand does not announce a rollout that is not happening.
    /// </summary>
    public void RecordPlatformStop() => platformRequestedStop = true;

    /// <summary>
    /// Writes the notice to the players still connected to this lobby, once the
    /// listener that would have turned them away has closed. Announced at that
    /// moment because it is the moment the lobby is draining: the notice goes to
    /// the players the drain is waiting for, and to nobody who arrives later.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the writes.</param>
    /// <returns>How many players were told, and zero when there was no notice to send.</returns>
    public async Task<int> AnnounceAsync(CancellationToken cancellationToken = default)
    {
        if (!platformRequestedStop)
        {
            logger.LogInformation("This instance was not stopped by a rollout; no notice was sent");
            return 0;
        }

        var result = await flashNewsService.BroadcastAsync(
            new FlashNewsAnnouncement(Message),
            cancellationToken);

        if (result.Recipients == 0)
        {
            // The ordinary case for a lobby that was rolled out while it was
            // empty, which is a rollout nobody had to be told about.
            logger.LogInformation("The lobby was empty; the rollout notice reached nobody");
            return 0;
        }

        logger.LogInformation(
            "Told {Recipients} players that a rollout will disconnect them in {RolloutHours} hours",
            result.Recipients,
            RolloutHours);

        return result.Recipients;
    }
}
