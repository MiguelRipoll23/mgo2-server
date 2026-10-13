using Mgo2Server.GameplayServer.MatchRecords;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Runs the lobby-slot array handshake: it confirms the sequence a client
/// answered and publishes the pair again under the next one.
/// </summary>
/// <remarks>
/// <para>
/// The loop is the client's, and the confirmation is the only thing that moves
/// it. A host that writes the pair and never confirms gets one answer and then
/// silence — the client only advances once the sequence it answered has been
/// confirmed — and a host that confirms and does not republish leaves the client
/// with nothing to answer next. Both halves are written here, in one pass, as
/// the captures show them.
/// </para>
/// <para>
/// <b>The pair is per peer.</b> Each client answers under its own sequence and
/// is confirmed under it, so the pair is written back to the peer that answered
/// and not to the room. The captures hold one peer's side of the exchange and so
/// cannot show a room, but the sequence the answer names is the peer's own, and a
/// pair fanned out to the room would carry one peer's sequence to all of them.
/// </para>
/// <para>
/// The pair is the seventh and eighth records of the round's opening run
/// (<see cref="MatchStartRunFramesUtils"/>), which is where its first sequence
/// comes from, so the two stay in step: the run publishes <c>85 NN</c> and
/// <c>86 NN</c>, the client answers <c>82 NN</c> and <c>84 NN</c>, and this
/// confirms <c>81 NN</c> and publishes <c>85 NN+1</c>.
/// </para>
/// <para>
/// <b>What the pair says is the room's and is read again here.</b> The republish
/// names the room's own players and sides, and it is read per confirmation rather
/// than carried over from the run: a client that answers a pair twice over a
/// briefing in which someone changed sides is answered with the room as it now
/// is. The read is one query against the room's rows
/// (<see cref="LobbySlotService"/>) behind a client that has just answered a
/// record, so it adds no wait the client can see.
/// </para>
/// </remarks>
/// <param name="lobbySlots">Reader of the room's own players and their sides.</param>
/// <param name="logger">Logger of this service.</param>
public sealed class TeamArrayService(
    LobbySlotService lobbySlots,
    ILogger<TeamArrayService> logger)
{
    /// <summary>
    /// Confirms the sequence a client answered and publishes the pair under the
    /// next one.
    /// </summary>
    /// <param name="context">Context of the answer being confirmed.</param>
    /// <param name="answered">Sequence the client answered.</param>
    public async Task ConfirmAsync(PeerContext context, byte answered)
    {
        var confirmation = TeamArrayFramesUtils.BuildConfirmation(answered);
        logger.LogDebug(
            "UDP {LocalPort}: confirming the lobby-slot arrays at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            answered,
            context.Remote);

        await context.Send(
            UdpCommandConstants.PlayerProfile,
            confirmation,
            TeamArrayFramesUtils.ConfirmationClass);

        var next = TeamArrayFramesUtils.NextSequence(answered);
        var (teams, occupancy) = TeamArrayFramesUtils.BuildPair(next, await lobbySlots.ReadAsync());
        var pair = new List<RosterRecord>
        {
            new(RoomRosterService.HostEntryType, teams, 0),
            new(UdpCommandConstants.PlayerProfile, occupancy, 1),
        };

        logger.LogDebug(
            "UDP {LocalPort}: publishing the lobby-slot arrays again at sequence {Sequence:x2} to {RemoteAddress}",
            context.LocalPort,
            next,
            context.Remote);

        await RosterRunSendUtils.SendAsync(context, pair, compressed: true);
    }
}
