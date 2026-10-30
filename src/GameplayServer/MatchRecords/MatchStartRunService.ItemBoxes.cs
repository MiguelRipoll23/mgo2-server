using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The item-box half of the round opener: the filler groups a fresh briefing's run
/// writes behind itself, which the beat takes over once a round opens.
/// </summary>
/// <remarks>
/// It lives apart from the run's own writer because the run is a round's record and
/// this is a briefing's, and because the two together outgrow the file limit the run
/// alone does not.
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// The item-box filler groups a fresh briefing writes: the first riding the
    /// run's own frame and the two behind it in datagrams of their own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and the third group is the run's own.</b>
    /// <c>mgo2-game-started-on-briefing</c> opens its fresh-room run at t+58.255
    /// with one filler group of the item-box pair at the end of the run's frame —
    /// <c>0x1250</c> under <c>0</c> and <c>1</c>, <c>0x125a</c> under <c>0</c> and
    /// <c>1</c>, bodies <c>00 00 00 00 00 00</c> and <c>01 00 00 00 00 00</c> — and
    /// writes two more groups in the two frames immediately behind it, under
    /// <c>2</c>/<c>3</c> and <c>4</c>/<c>5</c>, each in a datagram of its own with no
    /// other record in it (the capture stamps all three at the same instant). Its
    /// repeat two seconds later continues the same counters from six.
    /// </para>
    /// <para>
    /// <b>The bodies are the filler and not a box's event.</b> The group that opens
    /// a round with boxes carries four records per box with real bodies
    /// (<c>mgo2-started-briefing-then-solo-game</c> t+59.993 writes
    /// <c>00 74 01 00 00 00</c>, <c>01 8c 00 01 00 00</c> and the two longer ones),
    /// and a room that has spawned nothing writes the six-byte pair, which is what
    /// this does: the briefed round's boxes are the round's to create, and a host
    /// that invented their events would be telling the client about boxes it never
    /// spawned (the same reason <see cref="ItemBoxFramesUtils.ItemBoxRecordsFor"/>
    /// turns the beat's group over only once the round is open).
    /// </para>
    /// <para>
    /// A room whose round has played keeps the full run and none of this: the
    /// captured briefings that follow a round write no item-box group before their
    /// round opens.
    /// </para>
    /// <para>
    /// <b>They are built here and written behind the run, and the separation is
    /// the point.</b> The capture writes the run's frame with the first group at the
    /// end of it and only then the two others, each in a datagram of its own. A host
    /// that writes them first hands the client world-object records for a round
    /// whose run it has not received yet, which is the reverse of every capture.
    /// </para>
    /// </remarks>
    /// <param name="generation">Identifier set of the briefing.</param>
    /// <returns>The groups in send order, the first of which the run carries.</returns>
    private List<List<MatchRecordFramesUtils.RecordMessage>> BuildItemBoxGroups(
        MatchRecordGenerationUtils generation)
    {
        var groups = new List<List<MatchRecordFramesUtils.RecordMessage>>(
            ItemBoxFramesUtils.BriefingGroupCount);

        for (var group = 0; group < ItemBoxFramesUtils.BriefingGroupCount; group++)
        {
            // Two values per identifier per group, because the group writes two
            // records under each and the second takes the value behind the first:
            // the groups go out on 0/1, 2/3 and 4/5, as the capture numbers them
            // (`ItemBoxFramesUtils.BriefingGroupCount`).
            var itemBoxOrdinals = sequences.Take(
                generation.ItemBox, ItemBoxFramesUtils.ItemBoxRecords);
            var stateOrdinals = sequences.Take(
                generation.ItemBoxState, ItemBoxFramesUtils.ItemBoxStateRecords);

            groups.Add(ItemBoxFramesUtils.ItemBoxGroup(
                generation,
                ItemBoxFramesUtils.OpeningEventGroups,
                itemBoxOrdinals[0],
                stateOrdinals[0]));
        }

        return groups;
    }

    /// <summary>
    /// Writes built groups from <paramref name="firstGroup"/> on, each in a datagram
    /// of its own.
    /// </summary>
    /// <param name="context">Context the groups are written through.</param>
    /// <param name="groups">The groups, in send order.</param>
    /// <param name="firstGroup">Group to start writing at, from zero.</param>
    private static async Task SendItemBoxGroupsAsync(
        PeerContext context,
        IReadOnlyList<List<MatchRecordFramesUtils.RecordMessage>> groups,
        int firstGroup)
    {
        for (var group = firstGroup; group < groups.Count; group++)
        {
            await MatchRecordSendUtils.SendAsync(context, groups[group]);
        }
    }

    /// <summary>
    /// Writes the groups a repeated fresh-briefing run carries, on the counters the
    /// run's own groups left behind.
    /// </summary>
    /// <remarks>
    /// <b>The repeat continues the numbering and does not repeat it.</b> The
    /// captured repeat two seconds behind the run writes ordinals <c>6</c>/<c>7</c>
    /// and on where the run left off at <c>4</c>/<c>5</c>, which is why the run's
    /// own group is stripped from the repeated records
    /// (<see cref="MatchStartRunFramesUtils.RepeatOf"/>): the same bytes twice would
    /// be a duplicate sequence number, and the item-box family is keyed on it.
    /// </remarks>
    /// <param name="context">Context the groups are written through.</param>
    /// <param name="generation">Identifier set of the briefing.</param>
    private async Task RepeatItemBoxGroupsAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation)
    {
        // The repeat carries none in its frame, so all three go out on their own.
        await SendItemBoxGroupsAsync(
            context,
            BuildItemBoxGroups(generation),
            firstGroup: 0);
    }

    /// <summary>
    /// Keeps the item-box pair going after a fresh briefing's run and repeat, at
    /// the group's measured spacing, until the window the captures show or the
    /// round opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pair outlives the repeat, and a client answers every record of it.</b>
    /// The run's three groups and the repeat's three are the first 1.6 s of a
    /// stretch the reference keeps for eight: `mgo2-started-briefing-then-solo-game`
    /// writes thirty groups between its run and t+67.37, and
    /// `mgo2-game-started-on-briefing`'s client acknowledges 164 records of each
    /// identifier where this host's twelve left its own client acknowledging and
    /// then silent for the rest of the briefing
    /// (<see cref="ItemBoxFramesUtils.BriefingContinuationGroups"/>).
    /// </para>
    /// <para>
    /// <b>It ends where the round begins.</b> Once the round opens the group is the
    /// round's and the beat writes it (<see cref="BeatFramePlan"/>), so this stops
    /// on the open rather than running beside it, and it stops at the window's own
    /// end rather than at the briefing's: a room still briefed an hour later is a
    /// room the reference has written no item-box record for since its run.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the groups are written through.</param>
    /// <param name="generation">Identifier set of the briefing.</param>
    private async Task ContinueItemBoxGroupsAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation)
    {
        var session = context.Session;

        try
        {
            using var period = new PeriodicTimer(
                TimeSpan.FromMilliseconds(ItemBoxFramesUtils.BriefingContinuationPeriodMilliseconds));

            for (var group = 0; group < ItemBoxFramesUtils.BriefingContinuationGroups; group++)
            {
                if (!MatchRecordPeerUtils.IsAlive(session) || roundState.IsOpen)
                {
                    return;
                }

                // The counters are taken per group, as the run and the repeat take
                // them: a group is two records under each identifier and the next
                // group goes on from the value behind them.
                var itemBoxOrdinals = sequences.Take(
                    generation.ItemBox, ItemBoxFramesUtils.ItemBoxRecords);
                var stateOrdinals = sequences.Take(
                    generation.ItemBoxState, ItemBoxFramesUtils.ItemBoxStateRecords);

                await MatchRecordSendUtils.SendAsync(
                    context,
                    ItemBoxFramesUtils.ItemBoxGroup(
                        generation,
                        ItemBoxFramesUtils.OpeningEventGroups,
                        itemBoxOrdinals[0],
                        stateOrdinals[0]));

                await period.WaitForNextTickAsync();
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the item-box groups behind the run written to {RemoteAddress} stopped",
                context.LocalPort,
                context.Remote);
        }
    }
}
