using System.Net;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Holds the roster of the room this host is playing and turns it into the
/// player-profile records a joining client is answered with.
/// </summary>
/// <remarks>
/// <para>
/// A joining client sends its own profile and then re-sends it, byte for byte,
/// until the host answers. The answer is not one record but the room: the
/// recorded host wrote its own entry first and then one entry per joining
/// player, in roster order, back to back in the same message stream
/// (<c>docs/mgo2-game.pcapng</c>; see
/// <c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4). Until the client has seen
/// that sequence it
/// has no slot of its own and no way to place the players around it, which is
/// why it keeps retrying.
/// </para>
/// <para>
/// The host's own entry sits at <see cref="PlayerProfileRecordUtility.HostRosterIndex"/>;
/// the joining players fill 0, 1, 2 and so on, in the order they arrive. Slots
/// are handed out per remote endpoint, so a client that re-sends its profile
/// keeps the slot it was first given instead of taking a new one each retry.
/// </para>
/// <para>
/// Each entry repeats what the player announced about itself: its character id
/// at offset 8 and its appearance block at <c>0x13</c>–<c>0x1e</c>. Both are
/// per character rather than per slot — the capture shows the same character
/// carrying the same two on entries written at different roster indices — so a
/// peer is answered with the appearance the player arrived with rather than one
/// this host made up.
/// </para>
/// </remarks>
/// <param name="hostIdentity">Identity this host presents to its peers.</param>
public sealed class RoomRosterService(HostIdentityService hostIdentity)
{
    /// <summary>Roster slot of the first joining player.</summary>
    public const sbyte FirstJoinerRosterIndex = 0;

    /// <summary>
    /// Type the host's own roster entry is sent under, which is not the type the
    /// joining players' entries travel as.
    /// </summary>
    /// <remarks>
    /// The recorded host opens its answer with its own entry under
    /// <see cref="UdpCommandConstants.JoinRequest"/> and follows it with the
    /// joining players under <see cref="UdpCommandConstants.PlayerProfile"/> —
    /// the same tag the joiner itself opened the exchange with, which the host
    /// then uses for its own record. Both roster frames of the live session do
    /// this (docs/protocol/UDP_GAME_CAPTURE.md §4), so the head of the run is
    /// not merely a <c>0x1001</c> record like the rest of it.
    /// </remarks>
    public const ushort HostEntryType = UdpCommandConstants.JoinRequest;

    private readonly Lock gate = new();
    private readonly List<RosterMember> members = [];

    /// <summary>
    /// Adds a peer to the roster, or refreshes the one already there, and
    /// returns the slot it ended up in.
    /// </summary>
    /// <param name="remote">Endpoint the peer is reached at.</param>
    /// <param name="profile">
    /// Profile the peer announced, or <c>null</c> when its body could not be
    /// read. A peer without a readable profile still takes a slot, because the
    /// slot is what the room is keyed on.
    /// </param>
    /// <returns>The roster entry the peer is now filed under.</returns>
    public RosterMember Register(IPEndPoint remote, PlayerProfileRecord? profile)
    {
        var key = remote.ToString();

        lock (gate)
        {
            var existing = members.Find(member => member.RemoteAddress == key);
            if (existing is not null)
            {
                // A re-sent profile is the client still waiting for its answer,
                // not a second player, so the slot is kept and only the details
                // the client restated are refreshed.
                existing.CharacterId = profile?.CharacterId ?? existing.CharacterId;
                existing.Name = profile is { Name.Length: > 0 } ? profile.Name : existing.Name;
                existing.ClanName = profile?.ClanName ?? existing.ClanName;

                // The appearance block belongs to the character rather than to
                // the slot, so a re-sent profile restates it and nothing else
                // moves; it is only filled in the first time one arrives.
                if (profile is { Appearance.Length: > 0 })
                {
                    existing.Appearance = profile.Appearance;
                }

                return existing;
            }

            var member = new RosterMember(key, NextFreeIndex())
            {
                CharacterId = profile?.CharacterId ?? 0,
                Name = profile?.Name ?? string.Empty,
                ClanName = profile?.ClanName ?? string.Empty,
                Appearance = profile?.Appearance ?? [],
            };
            members.Add(member);
            return member;
        }
    }

    /// <summary>
    /// Number of joining players on the roster, the host itself not counted.
    /// </summary>
    public int JoinerCount
    {
        get
        {
            lock (gate)
            {
                return members.Count;
            }
        }
    }

    /// <summary>Removes a peer from the roster and frees its slot.</summary>
    /// <param name="remote">Endpoint the peer was reached at.</param>
    /// <returns>True when the peer was on the roster.</returns>
    public bool Remove(IPEndPoint remote)
    {
        var key = remote.ToString();

        lock (gate)
        {
            return members.RemoveAll(member => member.RemoteAddress == key) > 0;
        }
    }

    /// <summary>
    /// Builds the roster as the sequence of profile records the host sends to a
    /// peer: its own entry first, then every joining player in slot order.
    /// </summary>
    /// <returns>One record body per roster entry, in the order they are sent.</returns>
    public List<byte[]> BuildRecords()
    {
        List<RosterMember> ordered;

        lock (gate)
        {
            ordered = [.. members.OrderBy(member => member.RosterIndex)];
        }

        var records = new List<byte[]>
        {
            PlayerProfileRecordUtility.Build(
                PlayerProfileRecordUtility.RoomRecordSubType,
                PlayerProfileRecordUtility.HostRosterIndex,
                (int)hostIdentity.PeerIdentifier,
                hostIdentity.CharacterName,
                hostIdentity.ClanName),
        };
        records.AddRange(ordered.Select(member => member.BuildRecord()));
        return records;
    }

    /// <summary>
    /// Builds the whole roster run as it goes on the wire: the empty record the
    /// recorded host opens with, its own entry under <see cref="HostEntryType"/>,
    /// every joining player's entry under
    /// <see cref="UdpCommandConstants.PlayerProfile"/>, and the record that closes
    /// the run last.
    /// </summary>
    /// <returns>
    /// The records in the order they are sent, each with the type it travels as.
    /// </returns>
    public List<RosterRecord> BuildRosterRun()
    {
        var entries = BuildRecords();
        var run = new List<RosterRecord>(entries.Count + 2)
        {
            new(UdpCommandConstants.RosterHead, []),
            new(HostEntryType, entries[0]),
        };

        for (var index = 1; index < entries.Count; index++)
        {
            run.Add(new(UdpCommandConstants.PlayerProfile, entries[index]));
        }

        run.Add(new(UdpCommandConstants.PlayerProfile, PlayerProfileRecordUtility.BuildRosterClose()));
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
    /// <returns>The records of the run without its head, ready to be sent.</returns>
    public List<RosterRecord> BuildRosterRunRepeat() =>
    [
        .. BuildRosterRun().Where(record => record.Type != UdpCommandConstants.RosterHead),
    ];

    /// <summary>
    /// Builds the bare head record the recorded host sends after the run and its
    /// second copy: one empty <c>0x5001</c> and nothing else.
    /// </summary>
    /// <remarks>
    /// The live host closes a roster exchange with a third frame carrying the
    /// same empty <c>0x5001</c> that opens the run, 158 µs after the repeat in
    /// the capture (t+2.4758, outbound counter 4). It repeats the type that
    /// opened the run rather than closing it, and what it says is **[U]** — the
    /// type is the one §3 of <c>UDP_GAME_CAPTURE.md</c> shows is not the
    /// acknowledgement despite the resemblance.
    ///
    /// It is sent because the host sends it, on the same grounds as the rest of
    /// the run: a record a peer wrote and a host answers with is not a record
    /// this server should decline to write. Nothing waits on it.
    /// </remarks>
    /// <returns>The trailing record, ready to be sent.</returns>
    public static RosterRecord BuildRosterTrailer() => new(UdpCommandConstants.RosterHead, []);

    private sbyte NextFreeIndex()
    {
        var taken = members.Select(member => member.RosterIndex).ToHashSet();

        for (var index = FirstJoinerRosterIndex; index <= sbyte.MaxValue; index++)
        {
            if (!taken.Contains((sbyte)index))
            {
                return (sbyte)index;
            }
        }

        throw new InvalidOperationException("The room roster is full; no slot is left for another player.");
    }
}

/// <summary>One record of the roster run, with the type it travels as.</summary>
/// <param name="Type">Message type the record is sent under.</param>
/// <param name="Body">Record body.</param>
public sealed record RosterRecord(ushort Type, byte[] Body);

/// <summary>One joining player on the room roster.</summary>
/// <param name="RemoteAddress">Endpoint the player is reached at, formatted as "address:port".</param>
/// <param name="RosterIndex">Slot the player fills, counted from zero.</param>
public sealed record RosterMember(string RemoteAddress, sbyte RosterIndex)
{
    /// <summary>
    /// Character identifier the player announced, read off offset 8 of its
    /// profile.
    /// </summary>
    public int CharacterId { get; set; }

    /// <summary>Character name the player announced.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Clan name the player announced; empty when it announced none.</summary>
    public string ClanName { get; set; } = string.Empty;

    /// <summary>
    /// The player's appearance block, as it was announced. Empty when the
    /// profile carried none, which leaves the record's constants in place.
    /// </summary>
    public byte[] Appearance { get; set; } = [];

    /// <summary>Builds the player-profile record that puts this player on a peer's roster.</summary>
    /// <returns>The record body.</returns>
    public byte[] BuildRecord() =>
        PlayerProfileRecordUtility.Build(
            PlayerProfileRecordUtility.PlayerEntrySubType,
            RosterIndex,
            CharacterId,
            Name,
            ClanName,
            Appearance);
}
