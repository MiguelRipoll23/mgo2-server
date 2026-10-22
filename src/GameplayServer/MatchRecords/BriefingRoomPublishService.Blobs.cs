using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The state-blob half of the briefing's publication: one record per player of the
/// room, carrying this host's own measurement of that player and nothing it has not
/// measured.
/// </summary>
/// <remarks>
/// Split out of <c>BriefingRoomPublishService.cs</c> so neither file exceeds the
/// repository's size limit; the frame it rides on, and the room's other records, are
/// there.
/// </remarks>
public sealed partial class BriefingRoomPublishService
{
    /// <summary>Slot key the host's own player holds in the room's player table.</summary>
    private const byte HostOwnSlot = 0;

    /// <summary>
    /// Writes one state blob per player of the room on its own ten-second tick,
    /// once the sparse phase has written its last one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is one record per player and not one record for the room.</b> The
    /// identifier never travels alone in a room of more than one: every captured
    /// <c>0x0002</c> carries the slot byte the loadout records carry — the player's
    /// key plus one — and a capture's records for the room's players repeat on
    /// that tick. `mgo2-game1` writes seven of them, for classes `01`, `05`, `07`,
    /// `0c`, `10` and `15`, and its five joiners hold keys `04`, `06`, `0b`, `0f`
    /// and `14`; `mgo2-game-started-on-briefing` writes one for each of the six
    /// players it briefs. The class is therefore the player's own and the value is
    /// that player's own, which is what this writes.
    /// </para>
    /// <para>
    /// <b>The record is a one-field state transfer, and the class is its state.</b>
    /// The class byte is a slot state's index — one of the session's 24 slot states
    /// — and the body behind it is a 21-byte presence map plus the one field bit 150
    /// names, a single byte (`MatchStateBodiesUtils.StateBlobBody`);
    /// `docs/analysis/join-stall-after-mode2.md` §28.7 carries the parse and the
    /// client's handler that reads it.
    /// </para>
    /// <para>
    /// <b>This host writes its own measurement of the peer, which is the round trip
    /// it times from the records the peer answers.</b>
    /// The field is the host's figure for that player: 50 before it has one
    /// (`mgo2-game1`'s class `0c` reads 50, 55, 73, 83, 77, 78, 83, 78, 73, 85, and
    /// class `10` 50, 91, 90, 94, 98, 99), 100 for the host's own player in every
    /// capture that holds it, and 0 once the player has gone. The figure reads as the
    /// ladder's top less the round trip in milliseconds
    /// (<see cref="MatchStateBodiesUtils.StateBlobValueFor"/>), so a peer whose
    /// answer took 12 ms is written 88 and one that has answered nothing yet is
    /// written 50.
    /// <para>
    /// The measurement is <see cref="Shared.Types.PeerSession.RoundTripMilliseconds"/>:
    /// every record written is stamped with its instant and a client answers one
    /// record per answer, so the pairing is the wire's own. It is an upper bound —
    /// the peer's frame handling is inside it — and it is smoothed over four samples.
    /// </para>
    /// </remarks>
    /// <param name="context">Context the records are written through.</param>
    /// <param name="started">When this publication started, beside the sparse phase.</param>
    /// <param name="frames">Passes this publication has made.</param>
    private async Task PublishStateBlobAsync(
        PeerContext context,
        DateTimeOffset started,
        int frames)
    {
        if (frames % StateBlobFrameInterval != 0)
        {
            return;
        }

        // The sparse phase writes the recording's own blobs, so nothing here goes
        // out until it has closed: a blob beside a recorded one would be a second
        // record the capture does not carry.
        if (DateTimeOffset.UtcNow - started
            < TimeSpan.FromMilliseconds(SparsePhaseFramesUtils.SparsePhaseMilliseconds))
        {
            return;
        }

        var present = new HashSet<byte>();
        foreach (var slot in await lobbySlots.ReadAsync())
        {
            var key = slot.Slot >= byte.MaxValue ? byte.MaxValue : (byte)(slot.Slot + 1);
            present.Add(key);

            // The host's own player is 100 by definition — it is the reference's value
            // in every capture that holds it — and a peer is that less the round trip
            // this session has measured, or the unmeasured 50 until it has one.
            var value = slot.Slot == HostOwnSlot
                ? MatchStateBodiesUtils.MeasuredStateBlobValue
                : MatchStateBodiesUtils.StateBlobValueFor(context.Session.RoundTripMilliseconds);
            await context.Send(
                UdpCommandConstants.GameStateBlob,
                MatchStateBodiesUtils.StateBlobBody(value),
                key);
        }

        // A slot whose player has gone keeps its record and empties it, which is what
        // the captures do rather than dropping the slot: `mgo2-game1`'s slot 15 reads
        // 50 at t+6.7 and 0 on every tick from t+78.3, and the solo capture's slot 04
        // reads 50 at t+6.0 and 0 from t+92.9
        // (`MatchStateBodiesUtils.EmptyStateBlobValue`).
        List<byte> gone;
        lock (announcedGate)
        {
            announcedSlots.UnionWith(present);
            gone = [.. announcedSlots.Where(slot => !present.Contains(slot))];
        }

        foreach (var slot in gone)
        {
            await context.Send(
                UdpCommandConstants.GameStateBlob,
                MatchStateBodiesUtils.StateBlobBody(MatchStateBodiesUtils.EmptyStateBlobValue),
                slot);
        }
    }

    /// <summary>
    /// Slots this host has announced, so a slot whose player has gone can be written
    /// as empty rather than left at the last value its client holds.
    /// </summary>
    /// <remarks>
    /// It is the room's and not a peer's: every peer is told the same room, and a slot
    /// that empties is empty for all of them.
    /// </remarks>
    private readonly HashSet<byte> announcedSlots = [];

    private readonly Lock announcedGate = new();
}
