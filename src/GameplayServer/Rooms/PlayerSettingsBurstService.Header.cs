using Mgo2Server.GameplayServer.Match;
using Mgo2Server.Shared.Constants;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// The host's own settings header and the joining player's round-state transfer,
/// written in the burst beside the room's rotation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every reference join of a room that has not played carries it.</b>
/// `mgo2-game-started-on-briefing` t+8.125 and t+10.379,
/// `mgo2-game2` t+10.236 and t+12.361, and
/// `mgo2-game-failed-memoryviolation` t+11.085 and t+13.206 write the 25-byte
/// form of the header for slot <c>01</c> — the host's own player — between the
/// rotation and the loadouts, under the burst's consecutive ordinals (in
/// `game2`: rotation <c>5</c>, header <c>6</c>, loadout <c>7</c>).
/// </para>
/// <para>
/// <b>This host wrote none.</b> Its only header was the 24-byte answer form, and
/// only once a peer asked for the room (<c>TeamArrayService.RoomEntry</c>) — 20 s
/// or more after that peer had finished loading. The reference client's screen
/// is decided from what it holds at the join (it reaches its map about two
/// seconds after its lobby-slot echo without the host writing anything in
/// between), so a record written only in answer to a request cannot reach that
/// decision (<c>docs/analysis/join-stall-after-mode2.md</c> §33).
/// </para>
/// <para>
/// What would confirm it is the same join with this record on the wire: the
/// client's own `1d` rows within two seconds of its echo, as the three captures
/// above show. What would falsify it is the same silence with the header there,
/// which leaves the ten-byte control and the second field's value as the
/// remaining candidates. The loadout is not one: its length follows the
/// player's equipped skills, and this host already writes the reference's own
/// 50-byte no-skill body byte for byte.
/// </para>
/// </remarks>
public sealed partial class PlayerSettingsBurstService
{
    /// <summary>
    /// The burst's opening records, after the control byte: the round-state
    /// transfer, the room's rotation and the host's own settings header in the
    /// join-time form.
    /// </summary>
    /// <param name="rotation">
    /// The room's rotation, or <c>null</c> to write the recorded room's own body.
    /// </param>
    /// <returns>The records, in the order a reference host sends them.</returns>
    private List<RosterRecord> JoinPreamble(IReadOnlyList<MatchRotationEntry>? rotation)
    {
        // The state transfer comes first where the reference writes it as the
        // burst's first data record (`started-on-briefing` t+93.147, `game2`
        // t+120.811, the red-team capture t+148.371), and the rotation and header
        // keep the order the burst already had. Its value is the room's, so it is
        // written whether or not the roster can name the peer that asked.
        List<RosterRecord> preamble =
        [
            new(
                UdpCommandConstants.PlayerProfile,
                RoundStateBodiesUtils.JoinPlayerCountBody((byte)(roster.JoinerCount + 1)),
                roster.TakeRunOrdinal()),
        ];

        preamble.Add(new(
            RoomRosterService.HostEntryType,
            rotation is null ? RecordedRotationBody() : RoomRotationBody(rotation),
            roster.TakeRunOrdinal()));
        preamble.Add(new(
            UdpCommandConstants.PlayerProfile,
            SettingsHeaderBodiesUtils.BuildJoin((byte)(RoomRosterService.HostSessionSlot + 1)),
            roster.TakeRunOrdinal()));
        return preamble;
    }
}
