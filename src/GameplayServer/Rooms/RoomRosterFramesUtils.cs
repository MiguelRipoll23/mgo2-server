using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Turns the room's roster into the records a peer is answered with: the
/// profile entries, the run that carries them, and the copies the recorded host
/// sends behind it.
/// </summary>
/// <remarks>
/// <b>It is split from <see cref="RoomRosterService"/> because the two answer
/// different questions.</b> The service owns who is in the room and hands out its
/// slots; this file owns what those members look like on the wire, and it reads a
/// snapshot rather than the live list so a peer cannot join halfway down a run.
/// The shapes and the copies are measured in
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4.
/// </remarks>
public static class RoomRosterFramesUtils
{
    /// <summary>
    /// Builds the roster as profile records: the host's own entry first, then every
    /// joining player in slot order.
    /// </summary>
    /// <param name="members">The room's joining players, as a snapshot.</param>
    /// <param name="hostIdentity">Identity this host presents to its peers.</param>
    /// <returns>One record body per entry, in send order.</returns>
    /// <param name="omitSlot">
    /// Slot whose entry the run leaves out, or <c>null</c> for a room that leaves
    /// none out. The reference never sends a peer its own entry: its join reads
    /// sixteen entries for seventeen actors, and the actor that is missing is the
    /// `0x10` its kill feeds name — `actions.pcapng` at t+43.29 writes
    /// `09 10 04` and holds no profile entry for slot sixteen at all, while the
    /// client draws that player's own name in its corner regardless.
    /// </param>
    public static List<byte[]> BuildRecords(
        IReadOnlyList<RosterMember> members,
        HostIdentityService hostIdentity,
        bool includeEndpointPairs = true,
        byte? omitSlot = null)
    {
        var records = new List<byte[]>
        {
            PlayerProfileRecordUtility.Build(
                isHostEntry: true,
                PlayerProfileRecordUtility.HostRosterIndex,
                (int)hostIdentity.PeerIdentifier,
                // The host's entry names no character. `.pcapngs/actions/actions.pcapng`
                // writes it 57 bytes long where this host wrote 68, and the whole of
                // that difference is `server-9999` — the eleven characters of the
                // dedicated host's character name. The seat is the room's and not a
                // player of the round: the host is not in the gameplay session, so a
                // client that resolves slot zero as a character holds a player object
                // no avatar ever fills (`system.dll` `FUN_1032fec0`: an empty handle
                // pair resolves to null, and its callers read through it).
                string.Empty,
                hostIdentity.ClanName,
                RoomRosterService.HostSessionSlot,
                clanIdentifier: hostIdentity.ClanIdentifier,
                addressData: hostIdentity.AddressData,
                unresolvedByte: PlayerProfileRecordUtility.HostEntryUnresolvedByte,
                includeEndpointPairs: includeEndpointPairs),
        };
        records.AddRange(
            members
                .Where(member => member.SessionSlot != omitSlot)
                .OrderBy(member => member.RosterIndex)
                .Select(member => member.BuildRecord(includeEndpointPairs)));
        return records;
    }

    /// <summary>
    /// Builds the roster run as it goes on the wire: empty head, the host's own
    /// entry, every joining player, the close record and the room's count, all
    /// under <see cref="UdpCommandConstants.PlayerProfile"/>.
    /// </summary>
    /// <remarks>
    /// <b>The host writes its own entry under the room's identifier, not under the
    /// joiner's tag.</b> <c>0x9001</c> is `JoinRequest` — the tag a client opens the
    /// exchange with — and a host that answers with it under its own entry is
    /// writing a record the peer reads as one of its own. The two newest captures
    /// write the host's entry as <c>0x1001</c> beside the players':
    /// `briefing-then-solo-game` t+1.662 carries `1001` for the host, the joiner
    /// and the close, and `mgo2-game-started-on-briefing` t+5.996 the same (its
    /// whole twenty minutes hold no host-written `0x9001` at all). What remains
    /// under `JoinRequest` is the pair a round's open writes, which is measured in
    /// `mgo2-game1` (<see cref="TeamArrayService"/>) and is a different record.
    /// </remarks>
    /// <param name="members">The room's joining players, as a snapshot.</param>
    /// <param name="hostIdentity">Identity this host presents to its peers.</param>
    /// <param name="answeringLoadout">
    /// The answered peer's own settings payload, or <c>null</c> for a room that
    /// could not name it. It closes the run, which is where the reference writes
    /// it (<see cref="BuildRun"/>).
    /// </param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public static List<RosterRecord> BuildRun(
        IReadOnlyList<RosterMember> members,
        HostIdentityService hostIdentity,
        byte[]? answeringLoadout = null,
        bool includeEndpointPairs = true,
        byte? omitSlot = null)
    {
        var entries = BuildRecords(members, hostIdentity, includeEndpointPairs, omitSlot);
        var run = new List<RosterRecord>(entries.Count + 3)
        {
            new(UdpCommandConstants.RosterHead, [], 0),
            new(UdpCommandConstants.PlayerProfile, entries[0], 0),
        };

        // Each joining player carries its slot: the first reads one, so the
        // count runs from the host's own entry rather than from the head.
        for (var index = 1; index < entries.Count; index++)
        {
            run.Add(new(UdpCommandConstants.PlayerProfile, entries[index], (byte)index));
        }

        run.Add(new(
            UdpCommandConstants.PlayerProfile,
            PlayerProfileRecordUtility.BuildRosterClose(),
            (byte)entries.Count));

        // The room's count closes the run, one state transfer carrying the
        // population: `mgo2-game-started-on-briefing` t+5.996 writes the host's
        // entry, the joiner's, the close at ordinal 2 and then `1001`/10 bytes
        // `0b 00 00 04 00 00 00 00 00 02` at ordinal 3 — the host counted in, the
        // joiner counted in. Behind the close rather than inside the settings
        // burst, which is where this host used to write it. The port client's own
        // join puts its own loadout at that value instead
        // (`briefing-then-solo-game` t+1.662); the value is the peer's either way,
        // and the loadout follows it in the burst 3.0 s later.
        run.Add(new(
            UdpCommandConstants.PlayerProfile,
            RoundStateBodiesUtils.JoinPlayerCountBody((byte)(members.Count + 1)),
            (byte)(entries.Count + 1)));

        // The answered peer's own settings payload closes the run, and it is not
        // the burst's job: `mgo2-game-started-on-briefing` t+6.004 writes five
        // records under `1001` — the host's entry, the joiner's, the close at 2,
        // the count at 3 and a fifty-four-byte `0b 02` body at 4 — and writes the
        // same body again in the burst 2.1 s later. This host wrote it only in the
        // burst, so the join's answer named the room's population and never the
        // peer itself, and the burst's ordinals started one value early.
        if (answeringLoadout is not null)
        {
            run.Add(new(
                UdpCommandConstants.PlayerProfile,
                answeringLoadout,
                (byte)(entries.Count + 2)));
        }

        // **The per-seat settings headers do not belong here.** They were written in
        // this run once, one per seated member, and the join stopped working: the
        // answer became one datagram of nineteen records where the reference writes
        // five, and the capture after it holds thirty frames instead of four
        // thousand — a client that never finished the join. The reference's 194
        // twenty-five-byte headers across 17 slots are spread through the settings
        // burst, not carried by the answer; `PlayerSettingsBurstService` is where
        // they are written.
        return run;
    }

}
