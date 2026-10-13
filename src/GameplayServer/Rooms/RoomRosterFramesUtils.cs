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
    public static List<byte[]> BuildRecords(
        IReadOnlyList<RosterMember> members,
        HostIdentityService hostIdentity)
    {
        var records = new List<byte[]>
        {
            PlayerProfileRecordUtility.Build(
                isHostEntry: true,
                PlayerProfileRecordUtility.HostRosterIndex,
                (int)hostIdentity.PeerIdentifier,
                hostIdentity.CharacterName,
                hostIdentity.ClanName,
                RoomRosterService.HostSessionSlot,
                addressData: hostIdentity.AddressData,
                unresolvedByte: PlayerProfileRecordUtility.HostEntryUnresolvedByte),
        };
        records.AddRange(
            members
                .OrderBy(member => member.RosterIndex)
                .Select(member => member.BuildRecord()));
        return records;
    }

    /// <summary>
    /// Builds the roster run as it goes on the wire: empty head, the host's own
    /// entry under <see cref="RoomRosterService.HostEntryType"/>, every joining
    /// player under <see cref="UdpCommandConstants.PlayerProfile"/>, and the close
    /// record last.
    /// </summary>
    /// <param name="members">The room's joining players, as a snapshot.</param>
    /// <param name="hostIdentity">Identity this host presents to its peers.</param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public static List<RosterRecord> BuildRun(
        IReadOnlyList<RosterMember> members,
        HostIdentityService hostIdentity)
    {
        var entries = BuildRecords(members, hostIdentity);
        var run = new List<RosterRecord>(entries.Count + 2)
        {
            new(UdpCommandConstants.RosterHead, [], 0),
            new(RoomRosterService.HostEntryType, entries[0], 0),
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
        return run;
    }

    /// <summary>
    /// Builds the copy of a roster run that the recorded host sends immediately
    /// after the run itself: the same records in the same order, with the empty
    /// head record left off.
    /// </summary>
    /// <remarks>
    /// The live host sends every roster run twice. The second copy opens on the
    /// host's own entry rather than on <see cref="UdpCommandConstants.RosterHead"/>,
    /// and reaches the peer 0.7 to 0.9 ms after the first — 117 µs after it at the
    /// join itself.
    ///
    /// What the capture settles is the shape and the fact of the second copy, and
    /// not what schedules it. It is not a retransmission of a lost frame: the two
    /// go out on consecutive outbound sequences 56 to 68 frames apart, with the
    /// tick stream filling the gap, so the second is a new datagram rather than
    /// the same frame sent again. It is not acknowledgement-driven either — the
    /// joiner acknowledged sequence 1 twenty-seven times over the round and
    /// sequence 2, which is what the run travels as, never once — and the first
    /// acknowledgement of any kind arrives some 66 ms after the run this method
    /// repeats.
    ///
    /// So it is sent unconditionally, and nothing here waits for a peer to
    /// confirm it. What ends the repetition is **[U]**: no inbound record marks
    /// the end of a run's second copy in the capture, and the host's own
    /// acknowledgements never cover these sequences.
    /// </remarks>
    /// <param name="members">The room's joining players, as a snapshot.</param>
    /// <param name="hostIdentity">Identity this host presents to its peers.</param>
    /// <returns>The records of the run without its head, ready to be sent.</returns>
    public static List<RosterRecord> BuildRunRepeat(
        IReadOnlyList<RosterMember> members,
        HostIdentityService hostIdentity) =>
    [
        .. BuildRun(members, hostIdentity).Where(record => record.Type != UdpCommandConstants.RosterHead),
    ];
}
