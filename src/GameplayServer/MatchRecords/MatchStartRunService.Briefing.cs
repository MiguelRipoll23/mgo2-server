using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The briefing half of the round opener: the deadline that ends a room's wait
/// for reports, and the number that keeps one deadline acting on its own
/// briefing rather than on the one after it.
/// </summary>
/// <remarks>
/// The reports themselves are taken in <see cref="MatchStartRunService.Open"/>,
/// which gates them on the room's membership, and the run each round's open
/// writes lives in <c>MatchStartRunService.Runs.cs</c>. This file is the wait
/// between the two: a briefing begins where the room's rounds do and ends when
/// its own time runs out.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Begins the room's briefing: the wait for every player to report, under a
    /// deadline of the room's own briefing time.
    /// </summary>
    /// <remarks>
    /// A briefing begins where the room's rounds do, which is the first peer's join
    /// and every round's end, and each one gets a number so a deadline that was
    /// armed for it acts on it and not on whatever briefing is current when it
    /// lands.
    /// </remarks>
    private void BeginBriefing()
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            // No published room is no round to open and no briefing time to time
            // it against; the reports that arrive will open nothing until it exists.
            logger.LogDebug(
                "A briefing began before this host's room was published; no deadline is armed for it");
            return;
        }

        int briefing;
        lock (gate)
        {
            briefing = ++briefingNumber;
            lastReportIdentifier = null;
            briefingGeneration = null;
        }

        // The countdown goes to the peers the deadline is armed for, so it is
        // armed with it: a client waiting out its own briefing timer is told how
        // much of it is left, and one that never received the deadline is the
        // client that never received this either. It is the seven-byte 0x0b
        // operations record, counting this same briefing time down in seconds
        // (`BriefingCountdownBodiesUtils`).
        //
        // The briefing writes it on the operations stream, which it opens: the
        // capture's countdown opens the stream and its round's run opens the same
        // stream again, so the stream is restarted here rather than continued from
        // the round before it.
        sequences.Restart(BriefingCountdownService.Identifier);
        countdown.Begin(DateTimeOffset.UtcNow, settings.BriefingTimeMinutes);

        _ = OpenWhenBriefingEndsAsync(briefing, settings.BriefingTimeMinutes);
    }

    /// <summary>
    /// Opens the room's round when its briefing time runs out without every player
    /// having reported.
    /// </summary>
    /// <remarks>
    /// <b>It is the client's own wait that makes this a deadline.</b> A client in a
    /// briefing waits for the host's round and stops waiting when its own briefing
    /// time is up, so a room that waited on a player who never reports leaves the
    /// players it did hear from past the end of their timer with nothing to play
    /// (<c>docs/GAMEPLAY_SERVER.md</c> §4.2). The round it opens is the round those
    /// reports asked for — same rule, same map, same clock — and the player still to
    /// report is served it as a late joiner is, which is what §4.1 measures a room
    /// doing with a peer that arrives mid-round. A briefing whose reporters have
    /// all left opens nothing either: the round is opened for players, and a room
    /// that has reaped every one of them has no report left to open one under.
    /// </remarks>
    /// <param name="briefing">Briefing this deadline belongs to.</param>
    /// <param name="briefingMinutes">Minutes the briefing runs for.</param>
    private async Task OpenWhenBriefingEndsAsync(int briefing, int briefingMinutes)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(briefingMinutes));

            if (roundState.IsOpen)
            {
                // A report opened the round inside the briefing, which is what the
                // deadline is there to save the room from waiting out.
                return;
            }

            ushort reportIdentifier;
            lock (gate)
            {
                if (briefingNumber != briefing || lastReportIdentifier is not { } reported)
                {
                    // Another briefing has begun — its own deadline acts for it — or
                    // nothing in this one has answered the room.
                    return;
                }

                reportIdentifier = reported;
            }

            if (ready.ReadyCount == 0)
            {
                // A round is opened for players, and every player this briefing
                // heard from has left the room: their states are dropped when
                // their sessions are reaped, so there is no report left to open
                // one under. Without this the deadline would start a round for a
                // room nobody is in, and the next player to arrive would be
                // served that round instead of a briefing of its own.
                logger.LogDebug(
                    "The room's {BriefingTime}-minute briefing ran out with no player left that had "
                    + "reported; no round is opened",
                    briefingMinutes);
                return;
            }

            logger.LogInformation(
                "The room's {BriefingTime}-minute briefing ran out with {PendingCount} player(s) still to "
                + "report; the round opens for the players that reported, on 0x{Identifier:x4}",
                briefingMinutes,
                ready.PendingCount,
                reportIdentifier);
            OpenRound(
                MatchRecordGenerationUtils.OfReadinessReport(reportIdentifier),
                reportIdentifier,
                waitForEveryone: false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The briefing deadline for briefing {Briefing} ended without opening a round",
                briefing);
        }
    }
}
