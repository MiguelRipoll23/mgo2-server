using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The beat: the steady frame a peer is written once its round is playing, and
/// the identifier set each frame is stamped with.
/// </summary>
/// <remarks>
/// Split out of <c>MatchRecordService.cs</c> so neither file exceeds the
/// repository's size limit; the service that starts and stops it, and the sparse
/// phase that precedes it, are there.
/// </remarks>
public sealed partial class MatchRecordService
{
    /// <summary>
    /// Beats for as long as the peer is there: the capture's steady phase, at its
    /// 33 ms frame period (30.3 frames a second), under the round's own identifier
    /// set.
    /// </summary>
    /// <remarks>
    /// <b>The set follows the round, and the counters start over with it.</b> The
    /// capture alternates the set with the rounds it plays — <c>0x1a54</c>, then
    /// <c>0x1254</c> a <c>0x800</c> bit along — and the set a round plays under is
    /// the one its own join report arrived on, which
    /// <see cref="MatchRoundStateService.Generation"/> holds. The set is read per
    /// frame rather than captured once, so a beat that spans a round boundary
    /// moves to the next round's identifiers on its next frame instead of
    /// streaming a round its client is not on.
    /// <para>
    /// Each round is its own stream, so its frame index is its own too: every
    /// offset in the beat is measured from that round's opening run, and a frame
    /// index carried across a boundary would place the vitals onset and the
    /// item-box groups at the wrong end of the new round.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    private async Task BeatAsync(PeerContext context)
    {
        var session = context.Session;
        var beat = new BeatCounters { FrameIndex = FirstBeatFrameIndex };
        var round = roundState.RoundNumber;
        var generation = BeatGenerationFor(context);

        // The period is held by a timer and not by a delay behind the send: a
        // `Task.Delay(33)` costs the send as well, and the measured beat came out
        // at 28 frames a second against the recording's 30.3. The timer ticks on
        // its own clock, so a slow frame does not push the next one out.
        using var period = new PeriodicTimer(
            TimeSpan.FromMilliseconds(MatchRecordFramesUtils.FramePeriodMilliseconds));

        while (IsAlive(session))
        {
            if (roundState.RoundNumber != round)
            {
                round = roundState.RoundNumber;
                beat = new BeatCounters { FrameIndex = FirstBeatFrameIndex };
                generation = BeatGenerationFor(context);
                logger.LogDebug(
                    "UDP {LocalPort}: round changed to {RoundNumber}; the beat restarts its frame index "
                    + "and moves to identifier set operations 0x{Operations:x4}",
                    context.LocalPort,
                    round,
                    generation.Operations);
            }

            // The clock is read here, per frame, and not captured with the counters:
            // it is the round's remaining time, so a beat that spans a round gets
            // the new round's own clock rather than the one the old round opened
            // on, which is what a replay of a fixed value would write. The vitals
            // onset is read the same way, off the round's own record rather than
            // the frame count the capture places it at — and it is gated on the
            // round being open, because this beat begins on the run a briefing's
            // first report asks for, and no capture writes a player's health while
            // its client is still being briefed
            // (docs/analysis/join-stall-after-mode2.md §14).
            var plan = new BeatFramePlan(
                beat.FrameIndex,
                generation,
                roundState.RemainingUnits,
                roundState.IsOpen,
                roundState.VitalsArrived,
                beat.BlobsWritten,
                beat.ItemBoxSequence,
                beat.ItemBoxStateSequence);

            // The insertions' fourth bytes are taken inside the plan, from the
            // stream of the identifier each record travels under; what advances
            // here is the blob alternation, once per state blob, and the item-box
            // counters, once per record the group wrote.
            await SendAsync(context, plan.Records(sequences), plan.Compressed);

            if (plan.CarriesStateBlob)
            {
                beat.BlobsWritten++;
            }

            beat.ItemBoxSequence = plan.ItemBoxSequenceAfter;
            beat.ItemBoxStateSequence = plan.ItemBoxStateSequenceAfter;

            beat.FrameIndex++;
            await period.WaitForNextTickAsync();
        }

        logger.LogDebug(
            "UDP {LocalPort}: steady match records stopped for {RemoteAddress} because the peer is idle",
            context.LocalPort,
            session.RemoteAddress);
    }

    /// <summary>
    /// The identifier set this peer's beat writes under: the round's own, with the
    /// vitals record's identifier read from the peer's slot in the room's
    /// 24-entry player table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The vitals identifier is the player's own slot and not a constant of the
    /// beat.</b> <see cref="PlayerVitalsFramesUtils"/> holds the measurement: the
    /// host writes the player's vitals under <c>0x0844 + 0x0a × slot</c>, so
    /// `mgo2-game1`'s peer in slot <c>0x14</c> draws <c>0x090c</c> — the value the
    /// recorded set carries — while a room whose players hold other slots draws
    /// theirs, and the round's own sibling bit decides the form of that value.
    /// A host that replayed the constant wrote every player as slot 20, which is
    /// the one slot no live room here has ever put a player in.
    /// </para>
    /// <para>
    /// It is read here and not inside the frame builder because the slot is the
    /// peer's own: the beat is written per peer and the room is shared, so the one
    /// piece of the set that differs from peer to peer is the vitals record. The
    /// read is one lookup on the roster this host already holds, once per round,
    /// and a peer whose member is not registrable yet keeps the round's own
    /// value — the same fallback every other writer here uses for a peer without
    /// a slot.
    /// </para>
    /// </remarks>
    /// <param name="context">Context of the peer the beat is written to.</param>
    /// <returns>The set to write this peer's beat under.</returns>
    private MatchRecordGenerationUtils BeatGenerationFor(PeerContext context)
    {
        var generation = roundState.Generation;

        // Both endpoints are looked up, for the reason the other writers give: a
        // peer is filed under the endpoint its profile arrived from, while the
        // session answers at the one the peer advertised.
        var member = roster.Find(context.Remote.ToString())
            ?? roster.Find(context.Session.DialBack.ToString());
        if (member is null)
        {
            logger.LogDebug(
                "UDP {LocalPort}: no roster entry names the peer at {RemoteAddress}, so its beat keeps the "
                + "round's own vitals identifier 0x{Vitals:x4}",
                context.LocalPort,
                context.Remote,
                generation.Vitals);
            return generation;
        }

        var vitals = PlayerVitalsFramesUtils.IdentifierFor(generation, member.SessionSlot);
        if (vitals == generation.Vitals)
        {
            return generation;
        }

        logger.LogDebug(
            "UDP {LocalPort}: the peer at {RemoteAddress} holds slot {Slot}, so its beat writes the vitals "
            + "record under 0x{Vitals:x4} rather than the recorded room's 0x{Recorded:x4}",
            context.LocalPort,
            context.Remote,
            member.SessionSlot,
            vitals,
            generation.Vitals);
        return generation with { Vitals = vitals };
    }
}
