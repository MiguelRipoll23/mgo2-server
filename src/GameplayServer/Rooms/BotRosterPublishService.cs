using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Writes the records that put a fill's bots into a room a client is already
/// briefed in: each bot's roster entry, the close, the room's count and each bot's
/// own loadout.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is an append and not a second roster run.</b> The recorded host adds its
/// bots to a live room as single entries numbered from the room's own exchange
/// counter: `pcapng1` writes `BOT-11` at ordinal <c>81</c>, the close at <c>82</c>,
/// the room's count at <c>83</c> and that bot's loadout at <c>84</c>, and its
/// second briefing appends ten bots the same way (ordinals 21…30, the close at
/// 31, the loadout of each behind it). This host used to answer the report with a
/// whole roster run whose numbering restarted at zero — a run the client had
/// already read, and the reason its briefing stayed a room of one.
/// </para>
/// <para>
/// <b>The loadout is where a bot's face reaches the client.</b> The appearance
/// block in a <c>0b</c> body is the only place the game reads a player's look from
/// (<c>docs/protocol/UDP_SETTINGS_PAYLOAD.md</c>), and the burst that carries it
/// went out before the fill existed, so each added bot's own body rides behind the
/// close here, exactly where the reference writes it.
/// </para>
/// <para>
/// Only the bots that were just seated are named. A second player who reports does
/// not re-announce the room, because the room's other entries are already in the
/// client's table and repeating them spends ordinals on records it has read.
/// </para>
/// </remarks>
/// <param name="roster">The room's roster, which owns the members and the exchange's counter.</param>
/// <param name="loadouts">Reader of the room's own players, which owns each bot's appearance and skills.</param>
/// <param name="burst">Builder of a single loadout body, so the shape is one writer's.</param>
public sealed class BotRosterPublishService(
    RoomRosterService roster,
    LobbyLoadoutService loadouts,
    PlayerSettingsBurstService burst)
{
    /// <summary>
    /// Builds the records that announce newly seated bots, in the reference's order:
    /// one entry per bot, the close, the room's count, then one loadout per bot.
    /// </summary>
    /// <param name="added">The members the fill just seated, as the roster holds them.</param>
    /// <param name="cancellationToken">Token that cancels the loadout read.</param>
    /// <returns>The records in send order, each with the ordinal it takes.</returns>
    public async Task<List<RosterRecord>> BuildAsync(
        IReadOnlyList<RosterMember> added,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(added);

        var records = new List<RosterRecord>((added.Count * 2) + 2);
        foreach (var member in added.OrderBy(member => member.RosterIndex))
        {
            // The short layout and no endpoint field: a bot has no endpoint to
            // announce, which is the shape every captured bot entry carries.
            records.Add(new RosterRecord(
                UdpCommandConstants.PlayerProfile,
                member.BuildRecord(includeEndpointPairs: false),
                roster.TakeRunOrdinal()));
        }

        records.Add(new RosterRecord(
            UdpCommandConstants.PlayerProfile,
            PlayerProfileRecordUtility.BuildRosterClose(),
            roster.TakeRunOrdinal()));

        records.Add(new RosterRecord(
            UdpCommandConstants.PlayerProfile,
            RoundStateBodiesUtils.JoinPlayerCountBody((byte)(roster.JoinerCount + 1)),
            roster.TakeRunOrdinal()));

        var seats = added.Select(member => member.SessionSlot).ToHashSet();
        var room = await loadouts.ReadAsync(cancellationToken);
        foreach (var loadout in room.Where(loadout => seats.Contains(loadout.Slot)).OrderBy(loadout => loadout.Slot))
        {
            records.Add(new RosterRecord(
                UdpCommandConstants.PlayerProfile,
                burst.BuildLoadoutBody(loadout),
                roster.TakeRunOrdinal()));

            // The gear body behind the loadout, as the burst writes it for a player
            // seated at the join: a bot's loadout names its face, and this one
            // carries the gear behind the same key (`PlayerGearBodiesUtils`).
            records.Add(new RosterRecord(
                UdpCommandConstants.PlayerProfile,
                PlayerGearBodiesUtils.Build(
                    (byte)(loadout.Slot + 1),
                    loadout.Appearance,
                    PlayerGearBodiesUtils.PlayerStats.None,
                    loadout.Level),
                roster.TakeRunOrdinal()));

            // The per-slot settings header, which is the record that gives a client
            // a player at a slot: `0b <slot> 02 00 … 10 00 fe ff`, the twenty-five
            // byte join form (`SettingsHeaderBodiesUtils.BuildJoin`).
            // `.pcapngs/actions/actions.pcapng` writes **194 of them across 17
            // slots** — one per seat, repeated — where this host wrote four, because
            // only the host's own seat and a joining player's ever got one. A bot
            // without one is known to the client only through its position records:
            // it is drawn, and it takes no damage, because the row a hit is applied
            // to and the row a kill feed resolves are both this record's.
            records.Add(new RosterRecord(
                UdpCommandConstants.PlayerProfile,
                SettingsHeaderBodiesUtils.BuildJoin((byte)(loadout.Slot + 1)),
                roster.TakeRunOrdinal()));
        }

        return records;
    }
}
