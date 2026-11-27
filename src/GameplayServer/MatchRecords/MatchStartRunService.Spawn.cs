using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The spawn half of the round opener: the lobby-slot echo a client answers the
/// round's pair with, which is what the round's run waits for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The run follows the echo, not the report.</b> A round opened behind the
/// result screens is handed its lobby-slot pair (<c>85 NN</c>/<c>86 NN</c>) at the
/// open, and the client answers it with <c>82 NN</c>/<c>84 NN</c>. The run that
/// carries the vector, the spawn record and the clock follows that answer by
/// 0.196 s in <c>rounded-ended-by-reaching-tickets-limit</c> (echo t+97.320, run
/// t+97.516), while the join report ahead of both by 1.75 s is the record the
/// client sends on its way into the room, beside the confirmation (t+95.763).
/// Written on the report alone the run reaches a client that has not read the
/// pair yet, and the round it should spawn into is the one it does not.
/// </para>
/// <para>
/// <b>The wait ends on an echo or on the measured bound.</b> A client that never
/// echoes is not held out of a round it has already been told about, so the wait
/// gives up and the run is written anyway; a round that closes under the wait
/// writes nothing, because its records are its own.
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// The longest the run waits for the client's lobby-slot echo before writing
    /// anyway: a client that stops answering still gets the round it was shown,
    /// and a room held on a screen that never advances is the stall this replaces.
    /// </summary>
    private const int ArrayEchoWaitMilliseconds = 10_000;

    /// <summary>
    /// Lobby-slot echoes heard, by the endpoint that sent them. A run is a peer's
    /// own record, so the count it waits on is that peer's and not the room's.
    /// </summary>
    private readonly Dictionary<string, int> arrayEchoes = [];

    /// <summary>
    /// Echoes a peer has answered the round's pair with, counted since the process
    /// started.
    /// </summary>
    /// <param name="remoteAddress">Endpoint of the peer, as its frames arrive.</param>
    /// <returns>The number of that peer's echoes this host has heard.</returns>
    public int ArrayEchoesHeard(string remoteAddress)
    {
        lock (gate)
        {
            return arrayEchoes.TryGetValue(remoteAddress, out var heard) ? heard : 0;
        }
    }

    /// <summary>
    /// Notes that a peer answered a lobby-slot pair, which is the record the
    /// round's run is written behind.
    /// </summary>
    /// <param name="remoteAddress">Endpoint of the peer that answered.</param>
    public void NoteArrayEcho(string remoteAddress)
    {
        lock (gate)
        {
            arrayEchoes.TryGetValue(remoteAddress, out var heard);
            arrayEchoes[remoteAddress] = heard + 1;
        }
    }

    /// <summary>
    /// Waits for the lobby-slot echo the round's spawn follows: the record a
    /// client sends once it has read the pair the open published, which is the
    /// screen it leaves for its weapon select.
    /// </summary>
    /// <param name="context">Context the run is written through.</param>
    /// <param name="echoesHeard">
    /// <see cref="ArrayEchoesHeard"/> as it stood before the round opened: the run
    /// waits for an echo behind that count, which is the one the client sends after
    /// reading the pair the open published rather than one it already answered.
    /// </param>
    private async Task WaitForArrayEchoAsync(PeerContext context, int echoesHeard)
    {
        var session = context.Session;
        var remote = context.Remote.ToString();
        var waited = 0;

        while (MatchRecordPeerUtils.IsAlive(session)
            && roundState.IsOpen
            && ArrayEchoesHeard(remote) <= echoesHeard
            && waited < ArrayEchoWaitMilliseconds)
        {
            await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);
            waited += MatchRoundStateService.BriefingRunPollMilliseconds;
        }

        if (ArrayEchoesHeard(remote) <= echoesHeard)
        {
            logger.LogDebug(
                "UDP {LocalPort}: no lobby-slot echo came from {RemoteAddress} {Seconds} s after the round "
                + "opened; writing its run anyway",
                context.LocalPort,
                context.Remote,
                waited / 1000);
        }

        await WaitForOpenToRunGapAsync(context);
    }

    /// <summary>
    /// Waits the rest of the measured open-to-run gap, which the echo gate spends.
    /// </summary>
    /// <remarks>
    /// **The run owns the rest of the measured gap.** The record a gate waits on
    /// sits inside it — the reference's open publishes the pair at t+96.497, reads
    /// the echo at t+97.320 and writes the run at t+97.516; its briefing report is
    /// at t+146.831 against a run at t+147.650 — so the wait is measured from the
    /// open and never spent twice. Written on the record's own tick the run reached
    /// a client still reading it and was dropped; the repeat behind it carries no
    /// opening answer, so nothing else starts the round. A record that lands after
    /// the gap writes at once.
    /// </remarks>
    /// <param name="context">Context the run is written through.</param>
    private async Task WaitForOpenToRunGapAsync(PeerContext context)
    {
        if (roundState.OpenedAt is not { } openedAt)
        {
            return;
        }

        var elapsed = (int)(DateTimeOffset.UtcNow - openedAt).TotalMilliseconds;
        var remaining = MatchRecordTimingUtils.RoundStartRunGapMilliseconds - elapsed;
        if (remaining > 0 && MatchRecordPeerUtils.IsAlive(context.Session) && roundState.IsOpen)
        {
            await Task.Delay(remaining);
        }
    }
}
