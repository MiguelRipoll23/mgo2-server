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
    /// Appends the item-box filler group to a fresh briefing's run and writes the
    /// two that follow it in datagrams of their own.
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
    /// </remarks>
    /// <param name="context">Context the groups are written through.</param>
    /// <param name="generation">Identifier set of the briefing.</param>
    /// <param name="run">The run the first group is appended to.</param>
    private async Task AppendItemBoxGroupsAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        List<MatchRecordFramesUtils.RecordMessage> run)
    {
        await WriteItemBoxGroupsAsync(context, generation, run);
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
        await WriteItemBoxGroupsAsync(context, generation, attachTo: null);
    }

    /// <summary>
    /// Writes the fresh briefing's item-box groups, optionally riding the first in a
    /// frame the caller is about to send.
    /// </summary>
    /// <remarks>
    /// Three groups, each two records per identifier, numbered from the streams the
    /// identifiers own. The run's own group rides in its frame and the two behind it
    /// go out as datagrams of their own, which is the captured shape; the repeat
    /// attaches none and sends all three.
    /// </remarks>
    /// <param name="context">Context the groups are written through.</param>
    /// <param name="generation">Identifier set of the briefing.</param>
    /// <param name="attachTo">
    /// The frame the first group is appended to, or <c>null</c> to send every group
    /// in a datagram of its own.
    /// </param>
    private async Task WriteItemBoxGroupsAsync(
        PeerContext context,
        MatchRecordGenerationUtils generation,
        List<MatchRecordFramesUtils.RecordMessage>? attachTo)
    {
        for (var group = 0; group < ItemBoxFramesUtils.BriefingGroupCount; group++)
        {
            // Two values per identifier per group, because the group writes two
            // records under each and the second takes the value behind the first:
            // the run's groups go out on 0/1, 2/3 and 4/5, as the capture numbers
            // them (`ItemBoxFramesUtils.BriefingGroupCount`).
            var itemBoxOrdinals = sequences.Take(
                generation.ItemBox, ItemBoxFramesUtils.ItemBoxRecords);
            var stateOrdinals = sequences.Take(
                generation.ItemBoxState, ItemBoxFramesUtils.ItemBoxStateRecords);

            var records = ItemBoxFramesUtils.ItemBoxGroup(
                generation,
                ItemBoxFramesUtils.OpeningEventGroups,
                itemBoxOrdinals[0],
                stateOrdinals[0]);

            if (group == 0 && attachTo is not null)
            {
                attachTo.AddRange(records);
                continue;
            }

            await MatchRecordSendUtils.SendAsync(context, records);
        }
    }

}
