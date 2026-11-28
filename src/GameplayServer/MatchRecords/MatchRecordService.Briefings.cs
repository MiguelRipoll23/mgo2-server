using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The second start of a briefing's per-peer writers: the countdown and the
/// ready set a stage's briefing after its last round is owed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The writers that start at the join end at the first round's open.</b> The
/// countdown returns when a round plays (<see cref="BriefingCountdownService"/>)
/// and the ready set after the run it was armed on
/// (<see cref="BriefingReadyPublishService"/>), so a room that plays another stage
/// would wait out its next briefing without either record. This watcher is the
/// second start: it watches the stage-end briefings
/// (<see cref="MatchStartRunService.StageBriefingNumber"/>) and starts both
/// writers again for every peer still in the room.
/// </para>
/// <para>
/// <b>Writers still waiting for their run are not started twice.</b> A peer that
/// joined while the stage's result screens were up has its join-time writers
/// parked on the run the next briefing will write; starting another pair would
/// number the countdown twice. The join-time pair is left to serve that briefing,
/// and only a pair that has finished is replaced.
/// </para>
/// </remarks>
public sealed partial class MatchRecordService
{
    /// <summary>
    /// The writers last started for each peer, so a briefing is never served by
    /// two copies of the same task.
    /// </summary>
    private readonly Dictionary<PeerSession, BriefingWriters> briefingWriters = [];

    /// <summary>
    /// The two per-peer tasks a briefing is written by.
    /// </summary>
    /// <param name="Countdown">The briefing's countdown publication.</param>
    /// <param name="Ready">The briefing's ready-set publication.</param>
    /// <param name="Room">The room's rules, arrays and count.</param>
    private readonly record struct BriefingWriters(Task Countdown, Task Ready, Task Room);

    /// <summary>
    /// Records the writers started for a peer's current briefing.
    /// </summary>
    /// <param name="session">Session the writers are writing to.</param>
    /// <param name="countdown">The countdown publication.</param>
    /// <param name="ready">The ready-set publication.</param>
    /// <param name="room">The room's own rules, arrays and count.</param>
    private void NoteBriefingWriters(PeerSession session, Task countdown, Task ready, Task room)
    {
        lock (gate)
        {
            briefingWriters[session] = new BriefingWriters(countdown, ready, room);
        }
    }

    /// <summary>
    /// Watches for a stage-end briefing and starts the countdown and ready set
    /// for it again, once per peer.
    /// </summary>
    /// <remarks>
    /// <b>It watches the stage end and not every briefing.</b> The join's own
    /// briefing is served by the writers <see cref="RunAsync"/> starts, and a
    /// briefing that follows a round inside a stage has no countdown of its own.
    /// What is left is the briefing a completed stage opens, which the stage end
    /// numbers and the room has nobody else to start.
    /// </remarks>
    /// <param name="context">Context the restarted writers write through.</param>
    private async Task WatchStageBriefingsAsync(PeerContext context)
    {
        var session = context.Session;

        // **A briefing already running when this watcher starts is still owed to
        // the peer.** A peer that enters during the stage's result screens starts
        // this watcher after the stage end may have numbered the briefing, and one
        // that starts observing at that number never opens it: the client sits on
        // its result screen while the run watcher waits for an opening that this
        // watcher alone writes. Starting at zero lets the first pass see the
        // briefing the room is already in, and the guards below keep a between-
        // rounds join from opening one (only a stage's last round has a stage-end
        // briefing).
        var observed = 0;

        try
        {
            while (IsAlive(session))
            {
                await Task.Delay(MatchRoundStateService.BriefingRunPollMilliseconds);

                if (!IsAlive(session))
                {
                    return;
                }

                var current = matchStart.StageBriefingNumber;
                if (current == observed)
                {
                    continue;
                }

                observed = current;
                if (roundState.IsOpen
                    || !roundState.HasRoundEnded
                    || !roundState.LastRoundClosedGame)
                {
                    continue;
                }

                if (!await StartStageBriefingWritersAsync(context, current))
                {
                    logger.LogDebug(
                        "UDP {LocalPort}: the stage-end briefing {BriefingNumber} begins while the writers "
                        + "started at the join are still waiting for a run, so they serve it",
                        context.LocalPort,
                        current);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the stage-briefing watcher for {RemoteAddress} ended",
                context.LocalPort,
                session.RemoteAddress);
        }
    }

    /// <summary>
    /// Starts the countdown, ready-set and room writers for a stage-end briefing
    /// unless the set already running will serve it, and opens the briefing for the
    /// peer.
    /// </summary>
    /// <param name="context">Context the writers write through.</param>
    /// <param name="briefing">The stage-end briefing now beginning, for the log.</param>
    /// <returns><c>true</c> when a new pair of writers was started.</returns>
    private async Task<bool> StartStageBriefingWritersAsync(PeerContext context, int briefing)
    {
        // **The opening is owed to every peer of the stage-end briefing, and it is
        // awaited.** Its paced records are what the client reads before its report
        // opens the round, so the report's round run must not overtake them; the
        // wait is also what lets this service note the instant the round may open
        // (<see cref="MatchStartRunService.NoteStageOpeningPublished"/>). A peer
        // whose join-time writers are still waiting for this briefing's run is
        // served by them, but it is still owed the opening.
        await opening.PublishAsync(context);
        matchStart.NoteStageOpeningPublished(context.Session);

        lock (gate)
        {
            if (briefingWriters.TryGetValue(context.Session, out var running)
                && (!running.Countdown.IsCompleted || !running.Ready.IsCompleted || !running.Room.IsCompleted))
            {
                return false;
            }

            var countdown = countdowns.PublishAsync(context);
            var ready = readyPublications.PublishAsync(context);
            var room = roomPublications.PublishAsync(context);
            briefingWriters[context.Session] = new BriefingWriters(countdown, ready, room);

            logger.LogInformation(
                "UDP {LocalPort}: the stage-end briefing {BriefingNumber} begins, so its countdown and ready "
                + "set start again for {RemoteAddress}",
                context.LocalPort,
                briefing,
                context.Remote);
            return true;
        }
    }
}
