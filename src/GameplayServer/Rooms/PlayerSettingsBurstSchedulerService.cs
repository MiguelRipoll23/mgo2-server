using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Sends the settings burst the recorded host writes once, a measured pause
/// after it has answered a joiner with the roster.
/// </summary>
/// <remarks>
/// <para>
/// The capture holds exactly one burst in the whole round — t+4.9929, outbound
/// counter 5, six records, 2.52 s after the roster answer — and the peers that
/// join at t+154, t+385 and t+493 do not each get one. So it is scheduled on
/// the first roster answer and never again
/// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4).
/// </para>
/// <para>
/// The send is not awaited. The pause is longer than the accept path should hold
/// the dispatch loop open, and a host that blocked on it would stop reading every
/// other peer for two and a half seconds. The counter is still the session's:
/// the burst goes out on the sequence after the roster trailer because nothing
/// else is written in between, which is what the capture shows.
/// </para>
/// <para>
/// <b>The room's rotation and loadouts are resolved here, not replayed.</b> The
/// burst's rotation record is the only place a joiner is told which rounds the
/// room plays, and each loadout record is the only place it is told what the
/// room's players look like, so both are taken from this host and handed to the
/// builder: the rotation from this instance's own settings
/// (<see cref="MatchSettingsService"/>) and the loadouts from the room's rows
/// (<see cref="LobbyLoadoutService"/>). What is resolved here is resolved 2.52 s
/// before the burst goes out, which is where the capture's pause is, so the one
/// read costs the join nothing.
/// </para>
/// </remarks>
/// <param name="burst">Builder of the settings burst and the control record that follows it.</param>
/// <param name="matchSettings">Resolver of the room's own rotation.</param>
/// <param name="loadoutReader">Reader of the room's own players and their loadouts.</param>
/// <param name="roster">
/// The room's roster, whose slot for the peer the burst answers is the fourth byte
/// of its closing state blob.
/// </param>
/// <param name="logger">Logger of this scheduler.</param>
public sealed class PlayerSettingsBurstSchedulerService(
    PlayerSettingsBurstService burst,
    MatchSettingsService matchSettings,
    LobbyLoadoutService loadoutReader,
    RoomRosterService roster,
    ILogger<PlayerSettingsBurstSchedulerService> logger)
{
    /// <summary>
    /// Pause the recorded host leaves between the roster answer and the burst,
    /// measured at 2.52 s on outbound counter 5.
    /// </summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(2520);

    /// <summary>Set once the burst has been scheduled, so it leaves one time.</summary>
    private int scheduled;

    /// <summary>
    /// Schedules the burst for the peer the roster was just sent to, once per
    /// host.
    /// </summary>
    /// <param name="context">Context the roster was answered through.</param>
    public void Schedule(PeerContext context)
    {
        if (Interlocked.Exchange(ref scheduled, 1) != 0)
        {
            logger.LogDebug(
                "UDP {LocalPort}: settings burst already scheduled; not scheduling another for {RemoteAddress}",
                context.LocalPort,
                context.Remote);
            return;
        }

        logger.LogDebug(
            "UDP {LocalPort}: scheduled settings burst for {RemoteAddress} after {Delay}",
            context.LocalPort,
            context.Remote,
            Delay);
        _ = SendAsync(context);
    }

    private async Task SendAsync(PeerContext context)
    {
        await Task.Delay(Delay);

        var rotation = ReadRotation(context);

        // The room's own players are read beside its rotation, in the same pause:
        // each loadout record names one of them, and the read is what makes the
        // burst answer a joiner with the room it joined rather than with the
        // capture's characters.
        var loadouts = await loadoutReader.ReadAsync();

        logger.LogDebug(
            "UDP {LocalPort}: sending the settings burst to {RemoteAddress} after the roster pause: "
            + "{LoadoutCount} player loadout(s) from the room's own rows",
            context.LocalPort,
            context.Remote,
            loadouts.Count);

        // The burst answers one peer, and every capture says so in its closing
        // state blob: the fourth byte is that peer's slot byte, `key + 1`, which
        // is the record's own announcement of the player rather than of the room
        // (PlayerSettingsBurstService.BuildSettingsBurst).
        var answeringSlot = roster.Find(context.Session.RemoteAddress) is { } member
            ? (byte)(member.SessionSlot + 1)
            : (byte)0;

        await RosterRunSendUtils.SendAsync(
            context,
            burst.BuildSettingsBurst(rotation, loadouts, answeringSlot),
            compressed: true);
        logger.LogDebug(
            "UDP {LocalPort}: settings burst sent to {RemoteAddress}; sending its closing control",
            context.LocalPort,
            context.Remote);

        var closing = PlayerSettingsBurstService.BuildClosingControl();
        await context.Send(closing.Type, closing.Body, 0);
        logger.LogDebug(
            "UDP {LocalPort}: closing control 0x{MessageType:x4} sent to {RemoteAddress}",
            context.LocalPort,
            closing.Type,
            context.Remote);
    }

    /// <summary>
    /// The rotation the burst carries: this host's own, or <c>null</c> when its
    /// room is not published yet, which leaves the recorded room's body in the
    /// record.
    /// </summary>
    /// <remarks>
    /// The fallback is loud on purpose. A joiner that cannot be told its room's
    /// rounds is a joiner that will read another room's, so the log line names the
    /// two cases rather than letting the burst look the same either way.
    /// </remarks>
    /// <param name="context">Context the burst will be sent through.</param>
    /// <returns>The room's rotation, or <c>null</c>.</returns>
    private IReadOnlyList<MatchRotationEntry>? ReadRotation(PeerContext context)
    {
        var settings = matchSettings.FindPublished();
        if (settings is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: this host's room is not published, so the settings burst to {RemoteAddress} "
                + "carries the recorded host's rotation rather than this room's",
                context.LocalPort,
                context.Remote);
            return null;
        }

        logger.LogInformation(
            "UDP {LocalPort}: writing match {GameIdentifier} ({GameName})'s own rotation of {RoundCount} "
            + "round(s) into the settings burst to {RemoteAddress}",
            context.LocalPort,
            settings.GameIdentifier,
            settings.Name,
            settings.Rotation.Count,
            context.Remote);
        return settings.Rotation;
    }
}
