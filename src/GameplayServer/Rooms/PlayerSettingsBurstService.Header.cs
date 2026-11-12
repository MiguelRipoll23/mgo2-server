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
    /// The burst's opening records, after the control byte: the room's rotation
    /// and the host's own settings header in the join-time form.
    /// </summary>
    /// <remarks>
    /// <b>Both travel under the room's identifier.</b> The rotation is the room's
    /// own statement, so a host writes it under <c>0x1001</c> like the rest of its
    /// records; `briefing-then-solo-game` t+4.712 and
    /// `mgo2-game-started-on-briefing` t+8.125 both write `1001`/161 bytes, and
    /// neither capture holds a host-written `0x9001` at all. The joiner's tag is
    /// what a client opens the exchange with, and a room's rotation answered under
    /// it reads as a record the peer itself sent.
    /// </remarks>
    /// <param name="rotation">
    /// The room's rotation body, already built from the room and its current state.
    /// </param>
    /// <returns>The records, in the order a reference host sends them.</returns>
    private List<RosterRecord> JoinPreamble(byte[] rotationBody)
    {
        List<RosterRecord> preamble =
        [
            new(
                UdpCommandConstants.PlayerProfile,
                rotationBody,
                roster.TakeRunOrdinal()),
            new(
                UdpCommandConstants.PlayerProfile,
                SettingsHeaderBodiesUtils.BuildJoin((byte)(RoomRosterService.HostSessionSlot + 1)),
                roster.TakeRunOrdinal()),
        ];

        // **The burst carries one header, the host's own, and no seat's besides it.**
        //
        // The other seats' headers are written by `BuildSeatRows` in the run behind
        // this one, which is where they belong: they make a slot a player — a client
        // holding none draws the body, shoots at it and reports no hit, and a kill
        // feed's killer byte for that slot resolves no name and is drawn on the
        // host's own row — but they are not part of the frame that seats the room.
        //
        // A twenty-five byte header per seat was written here once, on the reading
        // that `actions.pcapng` holds 194 of them across 17 slots. The count is right
        // and the frame is wrong: the burst at t+3.55 holds exactly one, for slot
        // `01`, and the other 193 do not begin until t+15.44 — a different exchange,
        // twelve seconds behind the join. Writing seventeen of them here made this
        // burst fifty-two records against the reference's twenty-two, and the join
        // answered it with fifty-two nulls and stopped (`myserver.pcapng`, 2026-11-12,
        // t+14.77). So the header stays where the reference writes it: one seat, in
        // the burst, and every other seat's left to whatever exchange writes it later.
        return preamble;
    }

    /// <summary>
    /// The rows that name every other seat of the room, written as their own run
    /// behind the burst.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These are the records that make a slot a player, and they are not the
    /// burst's.</b> A client holding no settings row for a slot draws the body, shoots
    /// at it and reports no hit; a kill feed's killer byte for that slot resolves no
    /// name and is drawn on the host's own row. So each seat gets its twenty-five
    /// byte header, and each player its seventy-byte gear body, which is the record
    /// the appearance and the level come from.
    /// </para>
    /// <para>
    /// <b>Behind the burst and not inside it.</b> `actions.pcapng` t+3.55 holds one
    /// twenty-five byte header and nothing of this run; the other 193 begin at
    /// t+15.44, eleven seconds after the burst and after its client has reported. A
    /// burst carrying all seventeen came to fifty-two records against the reference's
    /// twenty-two and the join answered it with fifty-two nulls and stopped, so this
    /// run travels as the frame behind the burst, where the reference's own seats are
    /// named (`myserver.pcapng`, 2026-11-12).
    /// </para>
    /// </remarks>
    /// <param name="loadouts">
    /// The room's players, each with its own appearance and level, or <c>null</c> for a
    /// room whose rows could not be read, in which case only the headers are written.
    /// </param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildSeatRows(IReadOnlyList<LobbyLoadout>? loadouts)
    {
        List<RosterRecord> rows = [];

        foreach (var member in roster.Snapshot().OrderBy(member => member.RosterIndex))
        {
            // The host's own seat is the burst's header; two for one slot is a
            // sequence the reference never writes.
            if (member.SessionSlot == RoomRosterService.HostSessionSlot)
            {
                continue;
            }

            rows.Add(new(
                UdpCommandConstants.PlayerProfile,
                SettingsHeaderBodiesUtils.BuildJoin((byte)(member.SessionSlot + 1)),
                roster.TakeRunOrdinal()));
        }

        if (loadouts is { Count: > 0 })
        {
            foreach (var loadout in loadouts.OrderBy(player => player.Slot))
            {
                rows.Add(new(
                    UdpCommandConstants.PlayerProfile,
                    PlayerGearBodiesUtils.Build(
                        (byte)(loadout.Slot + 1),
                        loadout.Appearance,
                        PlayerGearBodiesUtils.PlayerStats.None,
                        loadout.Level),
                    roster.TakeRunOrdinal()));
            }
        }

        return rows;
    }
}
