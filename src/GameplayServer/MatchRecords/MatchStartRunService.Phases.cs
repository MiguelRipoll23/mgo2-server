using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.MatchRecords;

/// <summary>
/// The room's own phase record: state <c>0x00</c>'s field <c>0</c>, written at
/// the round's open and at its close.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the field a joining client waits on.</b> The client's join machine
/// reads field 0 and treats <c>0</c> and <c>7</c> as "wait and ask again"; any
/// other value makes it write its lobby-slot echo and move on
/// (<see cref="Rooms.RoundStateBodiesUtils.RoundPhaseBody"/>). A host that never
/// writes it leaves the peer holding whatever its last room put there.
/// </para>
/// <para>
/// <b>It is written where the reference writes it and not on a timer.</b>
/// <c>mgo2-game2</c> writes <c>00</c> in the same datagram as its round's opening
/// operations record (t+102.663) and again at the close (t+218.629), and the
/// round's own number in between; this host writes the two ends. What it does not
/// do is the reference's periodic re-sends inside a round, which the beat's
/// frames already carry the round's other state on.
/// </para>
/// <para>
/// <b>A failed send ends nothing.</b> The record is a courtesy to a peer's join
/// machine: one that cannot be delivered says the peer is gone, which the
/// watcher's next wait already handles, so it is logged and the watcher goes on.
/// </para>
/// </remarks>
public sealed partial class MatchStartRunService
{
    /// <summary>
    /// Writes one peer the room's phase record on the round's stream.
    /// </summary>
    /// <param name="context">Context the record is written through.</param>
    /// <param name="round">
    /// The round the match is on, or <c>0</c> for the moment a round has closed
    /// and the room is back in its briefing.
    /// </param>
    private async Task WriteRoundPhaseAsync(PeerContext context, byte round)
    {
        try
        {
            await context.Send(
                UdpCommandConstants.PlayerProfile,
                Rooms.RoundStateBodiesUtils.RoundPhaseBody(round),
                roster.TakeRunOrdinal());
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "UDP {LocalPort}: the room's phase record to {RemoteAddress} was not sent",
                context.LocalPort,
                context.Remote);
        }
    }
}
