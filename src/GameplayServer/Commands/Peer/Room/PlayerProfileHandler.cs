using Mgo2Server.GameplayServer.Rooms;
using Mgo2Server.GameplayServer.Stream;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameplayServer.Commands.Peer.Room;

/// <summary>
/// Handles a player-profile record. The joiner sends its own profile once the
/// session is keyed and then re-sends it, byte for byte, until the host answers;
/// this handler answers with the whole room roster and announces the new player
/// to peers already in the room.
/// </summary>
/// <remarks>
/// The type is shared. A joining client also sends one-byte <c>0x1001</c>
/// messages, which are not profiles and are not answered with a roster.
/// </remarks>
/// <param name="roster">Roster of the room this host is playing.</param>
/// <param name="burstScheduler">Scheduler of the one-shot burst after roster exchange.</param>
/// <param name="hostStream">Stream the recorded host keeps writing after the burst.</param>
/// <param name="options">Gameplay server options.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(
    RoomRosterService roster,
    PostJoinBurstSchedulerService burstScheduler,
    HostStreamService hostStream,
    IOptions<ServerOptions> options,
    ILogger<PlayerProfileHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var profile = PlayerProfileRecordParseUtils.Parse(context.Message.Body);
        if (profile is not { Name.Length: > 0 })
        {
            logger.LogDebug(
                "UDP {LocalPort}: {MessageType:x4} of {BodyLength} bytes from {RemoteAddress} carries no profile; not answered",
                context.LocalPort,
                context.Message.Type,
                context.Message.Body.Length,
                context.Remote);
            return;
        }

        var member = roster.Register(
            context.Remote,
            profile,
            context.Session.PeerIdentifier,
            context.Session.PeerAddressData);
        logger.LogDebug(
            "UDP {LocalPort}: registered roster entry for {RemoteAddress}: character {CharacterId}, " +
            "slot {RosterIndex}, current joiner count {JoinerCount}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
            member.RosterIndex,
            roster.JoinerCount);

        if (context.Session.PeerAddressData.Length != PlayerProfileRecordUtility.AddressDataLength)
        {
            logger.LogWarning(
                "UDP {LocalPort}: profile from {RemoteAddress} has no valid pair of handshake endpoints; " +
                "its roster address fields cannot be echoed faithfully",
                context.LocalPort,
                context.Remote);
        }

        logger.LogInformation(
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterId} name {Name}, roster slot {RosterIndex}",
            context.LocalPort,
            context.Remote,
            member.CharacterId,
            profile.Name,
            member.RosterIndex);

        var run = roster.BuildRosterRun();
        logger.LogDebug(
            "UDP {LocalPort}: built roster response for {RemoteAddress}: {RecordCount} records, " +
            "{JoinerCount} joiners, {FrameCount} outbound roster frames including repeat and trailer",
            context.LocalPort,
            context.Remote,
            run.Count,
            roster.JoinerCount,
            3);
        foreach (var record in run.Where(record => record.Type == RoomRosterService.HostEntryType))
        {
            logger.LogInformation(
                "UDP {LocalPort}: sending this host's own roster entry to {RemoteAddress} under " +
                "0x{MsgType:x4}, the tag a joiner opens the exchange with; the rest of the roster " +
                "travels as 0x{RosterType:x4}",
                context.LocalPort,
                context.Remote,
                record.Type,
                UdpCommandConstants.PlayerProfile);
        }

        var repeat = roster.BuildRosterRunRepeat();
        logger.LogDebug(
            "UDP {LocalPort}: sending roster response ({RecordCount} records, compressed)",
            context.LocalPort,
            run.Count);
        await RosterRunSendUtils.SendAsync(context, run, compressed: true);
        logger.LogDebug(
            "UDP {LocalPort}: sending roster repeat ({RecordCount} records, compressed)",
            context.LocalPort,
            repeat.Count);
        await RosterRunSendUtils.SendAsync(context, repeat, compressed: true);

        var trailer = RoomRosterService.BuildRosterTrailer();
        logger.LogDebug(
            "UDP {LocalPort}: sending roster trailer 0x{MessageType:x4} to {RemoteAddress}",
            context.LocalPort,
            trailer.Type,
            context.Remote);
        await context.Send(trailer.Type, trailer.Body, trailer.Ordinal);

        if (options.Value.GameplayServerPostJoinTraffic)
        {
            logger.LogDebug(
                "UDP {LocalPort}: scheduling post-join burst and host stream for {RemoteAddress}",
                context.LocalPort,
                context.Remote);
            burstScheduler.Schedule(context);
            hostStream.Start(context);
        }
        else
        {
            logger.LogInformation(
                "UDP {LocalPort}: post-join traffic is off; sending nothing after the roster to {RemoteAddress}",
                context.LocalPort,
                context.Remote);
        }

        logger.LogDebug(
            "UDP {LocalPort}: announcing {RecordCount} roster records from {RemoteAddress} to other peers",
            context.LocalPort,
            run.Count,
            context.Remote);
        foreach (var record in run)
        {
            await context.Broadcast(record.Type, record.Body, record.Ordinal);
        }
    }
}
