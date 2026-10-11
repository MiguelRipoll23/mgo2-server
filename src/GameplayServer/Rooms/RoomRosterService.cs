using System.Net;
using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Rooms;

/// <summary>
/// Holds the room roster and turns it into the player-profile records a joining
/// client is answered with. The recorded host wrote its own entry first then one
/// per joining player in roster order (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4).
/// </summary>
/// <param name="hostIdentity">Identity this host presents to its peers.</param>
public sealed class RoomRosterService(
    HostIdentityService hostIdentity,
    ILogger<RoomRosterService> logger)
{
    /// <summary>Roster slot of the first joining player.</summary>
    public const sbyte FirstJoinerRosterIndex = 0;

    /// <summary>
    /// Slot the host's own entry carries in the room's player table. The client
    /// binary numbers that table from <c>0</c>; the recorded host's own entry
    /// carries zero in the one captured round it is legible in
    /// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §8.4). It is the one key a
    /// joining player's entry must never carry, and 
    /// <see cref="FirstJoinerSessionSlot"/> is why it does not.
    /// </summary>
    public const byte HostSessionSlot = PlayerProfileRecordUtility.HostSessionSlot;

    /// <summary>First slot handed to a joining player; the host holds slot zero.</summary>
    public const byte FirstJoinerSessionSlot = 1;

    /// <summary>
    /// Number of slots in the room's player table, so slots run <c>0</c> to
    /// <c>23</c>. It is the table the client binary's lookup <c>0x26d5a8</c>
    /// walks: 24 records of <c>0x74</c> bytes, key in the first byte and
    /// <c>0xff</c> for an empty one. Every handle the captured rosters carry
    /// lies in this range.
    /// </summary>
    public const int SessionSlotCount = 24;

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
    /// The fourth byte the next record of the roster exchange takes.
    /// </summary>
    /// <remarks>
    /// The run numbers its positions from zero: the head and the host's own entry
    /// both read zero, the joining players read one upwards, and the close reads
    /// <see cref="JoinerCount"/> + 1. What follows the run keeps counting rather
    /// than restarting — the burst the recorded host sends 2.52 s later reads 3,
    /// 4, 5 and 6 under a one-player roster, which is this value and the three
    /// after it (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4) — so a sender that
    /// restarts the count writes numbers the peer has already seen.
    /// </remarks>
    public byte NextRunOrdinal
    {
        get
        {
            lock (gate)
            {
                return (byte)(members.Count + 2);
            }
        }
    }

    /// <summary>
    /// Adds or refreshes a peer on the roster, returning the slot it ended up in.
    /// A peer without a readable profile still takes a slot — the slot is what the
    /// room is keyed on.
    /// </summary>
    /// <param name="remote">Endpoint the peer is reached at.</param>
    /// <param name="profile">Profile the peer announced, or <c>null</c> when unreadable.</param>
    /// <param name="characterId">
    /// The peer's character id, taken from its handshake: the captured hosts
    /// write that handshake value straight into offset <c>0x08</c> of the
    /// peer's roster entry (game1: joiner handshake <c>0x0001232f</c> appears
    /// as <c>74543</c> there, the host's own <c>0x0000a001</c> as <c>40961</c>).
    /// </param>
    /// <param name="peerAddressData">Endpoint pairs from the handshake, packed for the roster record.</param>
    /// <returns>The roster entry the peer is now filed under.</returns>
    public RosterMember Register(
        IPEndPoint remote,
        PlayerProfileRecord? profile,
        uint characterId,
        byte[] peerAddressData)
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
                existing.CharacterId = (int)characterId;
                existing.Name = profile is { Name.Length: > 0 } ? profile.Name : existing.Name;
                existing.ClanName = profile?.ClanName ?? existing.ClanName;

                // The character's endpoint pairs arrive in its handshake, not
                // in the profile body. Keep that announced data across profile
                // retries; a malformed endpoint set must not overwrite it.
                if (peerAddressData.Length == PlayerProfileRecordUtility.AddressDataLength)
                {
                    existing.AddressData = peerAddressData.ToArray();
                }

                logger.LogDebug(
                    "Refreshed roster member {RemoteAddress}: character {CharacterId}, slot {RosterIndex}",
                    key,
                    existing.CharacterId,
                    existing.RosterIndex);
                return existing;
            }

            var member = new RosterMember(key, NextFreeIndex())
            {
                SessionSlot = NextFreeSessionSlot(),
                CharacterId = (int)characterId,
                Name = profile?.Name ?? string.Empty,
                ClanName = profile?.ClanName ?? string.Empty,
                AddressData = peerAddressData.ToArray(),
            };
            members.Add(member);
            logger.LogDebug(
                "Added roster member {RemoteAddress}: character {CharacterId}, slot {RosterIndex}; {JoinerCount} joiners",
                key,
                member.CharacterId,
                member.RosterIndex,
                members.Count);
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

    /// <summary>Removes a peer from the roster. Called by the session reaper.</summary>
    /// <param name="remoteAddress">Endpoint the peer was filed under.</param>
    /// <returns>True when the peer was on the roster.</returns>
    public bool Remove(string remoteAddress)
    {
        lock (gate)
        {
            var removed = members.RemoveAll(member => member.RemoteAddress == remoteAddress) > 0;
            logger.LogDebug(
                "Roster removal for {RemoteAddress}: removed={Removed}, remaining joiners={JoinerCount}",
                remoteAddress,
                removed,
                members.Count);
            return removed;
        }
    }

    /// <summary>
    /// Builds the roster as profile records: the host's own entry first, then every
    /// joining player in slot order.
    /// </summary>
    /// <returns>One record body per entry, in send order.</returns>
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
                isHostEntry: true,
                PlayerProfileRecordUtility.HostRosterIndex,
                (int)hostIdentity.PeerIdentifier,
                hostIdentity.CharacterName,
                hostIdentity.ClanName,
                HostSessionSlot,
                addressData: hostIdentity.AddressData,
                unresolvedByte: PlayerProfileRecordUtility.HostEntryUnresolvedByte),
        };
        records.AddRange(ordered.Select(member => member.BuildRecord()));
        return records;
    }



    /// <summary>
    /// Builds the roster run as it goes on the wire: empty head, the host's own
    /// entry under <see cref="HostEntryType"/>, every joining player under
    /// <see cref="UdpCommandConstants.PlayerProfile"/>, and the close record last.
    /// </summary>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildRosterRun()
    {
        var entries = BuildRecords();
        var run = new List<RosterRecord>(entries.Count + 2)
        {
            new(UdpCommandConstants.RosterHead, [], 0),
            new(HostEntryType, entries[0], 0),
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

    /// <summary>
    /// Hands out the lowest free slot in the room's player table, host zero
    /// excepted. Distinct per live player; released when they leave.
    /// </summary>
    private byte NextFreeSessionSlot()
    {
        var taken = members.Select(member => member.SessionSlot).ToHashSet();

        for (var value = FirstJoinerSessionSlot; value < SessionSlotCount; value++)
        {
            if (!taken.Contains((byte)value))
            {
                return (byte)value;
            }
        }

        throw new InvalidOperationException("The room's player table is full; no key is left for another player.");
    }
}

/// <summary>One record of the roster run, with the type it travels as.</summary>
/// <param name="Type">Message type the record is sent under.</param>
/// <param name="Body">Record body.</param>
/// <param name="Ordinal">
/// Fourth byte: the record's position in the run. The recorded host fills it
/// for every record (head and host = 0, first player = 1, close = player count);
/// the repeat drops the head and keeps the rest. Later frames continue the count
/// rather than restart it.
/// </param>
public sealed record RosterRecord(ushort Type, byte[] Body, byte Ordinal = 0);

/// <summary>One joining player on the room roster.</summary>
/// <param name="RemoteAddress">Endpoint the player is reached at ("address:port").</param>
/// <param name="RosterIndex">Slot the player fills, counted from zero.</param>
public sealed record RosterMember(string RemoteAddress, sbyte RosterIndex)
{
    /// <summary>
    /// The player's slot in the room's 24-entry network player table (offsets 0x05
    /// and 0x07 of its roster entry), the byte the replay console's <c>rpdt.js</c>
    /// calls <c>slot</c>. The host allocates it; the captured host does not echo
    /// the value its client announced (game1's client sent <c>0x05</c> and the
    /// host wrote <c>0x14</c>), and the values it hands out are distinct per live
    /// player and released when they leave. How the host picks it is unresolved,
    /// so this server takes the lowest free one.
    /// </summary>
    public byte SessionSlot { get; init; }

    /// <summary>
    /// Character identifier of the player, written at offset <c>0x08</c> of its
    /// roster entry. It is the identifier from the player's handshake, which is
    /// the character id; the captures confirm both sides (game1: the joiner's
    /// handshake <c>0x0001232f</c> is written as <c>74543</c>, the host's own as
    /// <c>40961</c>).
    /// </summary>
    public int CharacterId { get; set; }

    /// <summary>Character name the player announced.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Clan name the player announced; empty when it announced none.</summary>
    public string ClanName { get; set; } = string.Empty;

    /// <summary>
    /// The peer's public/private endpoint pairs, copied from its handshake and
    /// written into the roster's twelve-byte endpoint field.
    /// </summary>
    public byte[] AddressData { get; set; } = [];

    /// <summary>Builds the player-profile record that puts this player on a peer's roster.</summary>
    /// <returns>The record body.</returns>
    public byte[] BuildRecord() =>
        PlayerProfileRecordUtility.Build(
            isHostEntry: false,
            RosterIndex,
            CharacterId,
            Name,
            ClanName,
            SessionSlot,
            addressData: AddressData,
            unresolvedByte: PlayerProfileRecordUtility.PlayerRecordUnresolvedByte);
}
