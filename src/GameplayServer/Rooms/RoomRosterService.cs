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
    MatchRecords.MatchRecordSequenceService sequences,
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

    /// <summary>
    /// Identifier the roster exchange is numbered on. Every record it writes — the
    /// head, the host's own entry, the joining players' profiles — is this
    /// identifier's low twelve bits, and the counter itself lives in
    /// <see cref="MatchRecords.MatchRecordSequenceService"/> so that the controls
    /// the match-record writers insert take the same sequence.
    /// </summary>
    public const ushort ExchangeIdentifier = UdpCommandConstants.PlayerProfile;

    private readonly Lock gate = new();
    private readonly List<RosterMember> members = [];

    /// <summary>
    /// Takes the fourth byte the next record of the roster exchange carries, and
    /// advances the exchange's counter past it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counter is one per identifier and every record under it consumes a
    /// value, which is why there is a take and no way to read it without taking
    /// one: a caller that pinned a value would write one a record before it has
    /// already taken it. The counter is the room's and not this file's — the same
    /// sequence numbers the <c>0xd001</c> controls the sparse phase and the beat
    /// insert, which the captures write between the exchange's own records
    /// (<see cref="MatchRecords.MatchRecordSequenceService"/>). Measured on
    /// `mgo2-game-joined-when-red-team-then-briefing`, whose live rounds carry
    /// every record of the roster identifier (<c>0x1001</c>) in one ascending
    /// sequence — the lobby-slot arrays take two consecutive values (23 and 24,
    /// then 28 and 29) and the scoreboard deltas take the values between them (25,
    /// 26 and 27).
    /// </para>
    /// <para>
    /// The run numbers its positions from zero: the head and the host's own entry
    /// both read zero, the joining players read one upwards, and the close reads
    /// <see cref="JoinerCount"/> + 1. What follows the run keeps counting rather
    /// than restarting — the burst the recorded host sends 2.52 s later reads 3,
    /// 4, 5 and 6 under a one-player roster, which is why the counter is seeded at
    /// <see cref="JoinerCount"/> + 2 when a member registers
    /// (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §4) — so a sender that restarts
    /// the count writes numbers the peer has already seen.
    /// </para>
    /// <para>
    /// A record that carries an index byte takes one; a record that does not — an
    /// opening control byte, the head — takes none, which is why this is called
    /// per record rather than once per frame.
    /// </para>
    /// </remarks>
    /// <returns>The fourth byte the record takes.</returns>
    public byte TakeRunOrdinal() => sequences.Take(ExchangeIdentifier);

    /// <summary>
    /// Opens the exchange's counter at a value, for a room whose join has just
    /// taken the values it carries. A stream already counting is left alone
    /// (<see cref="MatchRecords.MatchRecordSequenceService.Seed"/>).
    /// </summary>
    /// <param name="value">Value the exchange's next record should take.</param>
    public void SeedRunOrdinal(byte value) => sequences.Seed(ExchangeIdentifier, value);

    /// <summary>
    /// Takes the next values of the roster exchange's counter, in order, for a
    /// caller that writes several records under it.
    /// </summary>
    /// <remarks>
    /// The lobby-slot pair is written this way: the replay parser tells a pair by
    /// its two records being consecutive, and consecutive values are exactly what
    /// the counter hands out (<c>docs/protocol/UDP_GAME_CAPTURE.md</c> §3.2).
    /// </remarks>
    /// <param name="count">How many records the caller is about to write.</param>
    /// <returns>The values, in the order the records take them.</returns>
    public byte[] TakeRunOrdinals(int count) => sequences.Take(ExchangeIdentifier, count);

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

            // The run this join is answered with consumes the counter's values
            // from zero: the head and the host's own entry read zero, the joining
            // players one upwards, and the close members.Count + 1. What follows
            // the run — the burst, the round's lobby-slot pair, the scoreboard —
            // therefore starts here, and the seed is left alone where the room's
            // exchange has already counted past it.
            SeedRunOrdinal((byte)(members.Count + 2));

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

    /// <summary>
    /// The roster's joining players, in the order they joined. The host's own
    /// entry is not one of them, so a room's population is this list's count.
    /// </summary>
    /// <returns>A snapshot of the members, safe to walk outside the lock.</returns>
    public IReadOnlyList<RosterMember> Snapshot()
    {
        lock (gate)
        {
            return [.. members];
        }
    }

    /// <summary>
    /// The member filed under an endpoint, when the roster has one.
    /// </summary>
    /// <remarks>
    /// The lookup is by the endpoint the peer's profile arrived from, which is
    /// the key <see cref="Register"/> files it under; an endpoint with no entry
    /// is a peer that has not sent a profile yet, or one the reaper has already
    /// taken off the roster.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    /// <returns>The member, or <c>null</c>.</returns>
    public RosterMember? Find(string remoteAddress)
    {
        lock (gate)
        {
            return members.Find(member => member.RemoteAddress == remoteAddress);
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
    /// <remarks>
    /// The shapes are <see cref="RoomRosterFramesUtils"/>'; this reads the room's
    /// own members and hands them over as a snapshot, so a peer that joins while a
    /// run is being built is in the next run and not in the middle of this one.
    /// </remarks>
    /// <returns>One record body per entry, in send order.</returns>
    public List<byte[]> BuildRecords() =>
        RoomRosterFramesUtils.BuildRecords(Snapshot(), hostIdentity);

    /// <summary>
    /// Builds the roster run as it goes on the wire: empty head, the host's own
    /// entry under <see cref="HostEntryType"/>, every joining player under
    /// <see cref="UdpCommandConstants.PlayerProfile"/>, and the close record last.
    /// </summary>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildRosterRun() =>
        RoomRosterFramesUtils.BuildRun(Snapshot(), hostIdentity);

    /// <summary>
    /// Builds the copy of a roster run that the recorded host sends immediately
    /// after the run itself: the same records in the same order, with the empty
    /// head record left off.
    /// </summary>
    /// <returns>The records of the run without its head, ready to be sent.</returns>
    public List<RosterRecord> BuildRosterRunRepeat() =>
        RoomRosterFramesUtils.BuildRunRepeat(Snapshot(), hostIdentity);

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

