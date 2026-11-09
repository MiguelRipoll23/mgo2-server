using System.Net;
using Mgo2Server.GameplayServer.Bots;
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
public sealed partial class RoomRosterService(
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
    /// The lobby-slot sequence each peer's own exchange is on, by endpoint
    /// (<see cref="NotePairSequence"/>). It belongs to the peer and not to the
    /// room, so it is dropped with the member (<see cref="Remove"/>).
    /// </summary>
    private readonly Dictionary<string, byte> pairSequences = [];

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
    /// <param name="clanIdentifier">
    /// Clan id the entry names, read from the character record behind the peer's
    /// character id; zero when it is in no clan. It is what the client fetches the
    /// clan emblem with, so a zero draws no emblem on the row.
    /// </param>
    /// <returns>The roster entry the peer is now filed under.</returns>
    public RosterMember Register(
        IPEndPoint remote,
        PlayerProfileRecord? profile,
        uint characterId,
        byte[] peerAddressData,
        int clanIdentifier = 0)
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
                existing.ClanIdentifier = clanIdentifier;
                if (profile is { AnnouncedAppearance.Length: > 0 })
                {
                    existing.AnnouncedAppearance = profile.AnnouncedAppearance;
                }

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

            var member = new RosterMember(key, RosterSlotUtils.NextFreeRosterIndex(members, FirstJoinerRosterIndex))
            {
                SessionSlot = RosterSlotUtils.NextFreeSessionSlot(members, FirstJoinerSessionSlot, SessionSlotCount),
                CharacterId = (int)characterId,
                Name = profile?.Name ?? string.Empty,
                ClanName = profile?.ClanName ?? string.Empty,
                ClanIdentifier = clanIdentifier,
                AnnouncedAppearance = profile?.AnnouncedAppearance ?? [],
                AddressData = peerAddressData.ToArray(),
            };
            members.Add(member);

            // The run this join is answered with consumes the counter's values
            // from zero: the head and the host's own entry read zero, the joining
            // players one upwards, the close members.Count + 1, the room's count
            // members.Count + 2 and the answered peer's own settings payload
            // members.Count + 3. What follows the run — the burst, the round's
            // lobby-slot pair, the scoreboard — therefore starts here, and the seed
            // is left alone where the room's exchange has already counted past it.
            SeedRunOrdinal((byte)(members.Count + 4));

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
    /// Adds a bot this host plays to the roster, returning the member it was filed
    /// under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A bot is a roster member, filed under a key no endpoint can hold.</b> The
    /// recorded dedicated hosts put their own bots in the roster run — the
    /// briefing's ready set names them in the same slots as the players — so a bot
    /// here is registered the same way, with a slot and a roster index, and its
    /// entry is the short layout at <c>0x10</c> zero
    /// (<see cref="RosterMember.BuildRecord"/>). Nothing at its key ever answers,
    /// which is why the reaper never takes it off: only
    /// <see cref="Bots.BotPopulationService"/> adds and removes it.
    /// </para>
    /// <para>
    /// It takes no ordinal from the roster exchange. The exchange's counter numbers
    /// the records sent to a peer, and a bot has no peer to send to; spending values
    /// on it would move numbers a joining client is about to be handed.
    /// </para>
    /// </remarks>
    /// <param name="name">The bot's character name.</param>
    /// <returns>The member the bot is now filed under.</returns>
    public RosterMember RegisterBot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (gate)
        {
            var key = BotRosterKeyUtils.KeyFor(name);
            var existing = members.Find(member => member.RemoteAddress == key);
            if (existing is not null)
            {
                return existing;
            }

            var member = new RosterMember(key, RosterSlotUtils.NextFreeRosterIndex(members, FirstJoinerRosterIndex))
            {
                SessionSlot = RosterSlotUtils.NextFreeSessionSlot(members, FirstJoinerSessionSlot, SessionSlotCount),

                // Zero, and the record is what carries the slot: the settings
                // burst reads this id for the character's own appearance, so a
                // bot given its slot here would be announced with a player's
                // face. The entry writes the slot as its character id instead
                // (<see cref="RosterMember.BuildRecord"/>).
                CharacterId = 0,
                Name = name,
                ClanName = string.Empty,
                IsBot = true,
            };
            members.Add(member);

            logger.LogDebug(
                "Added bot member {Name}: slot {SessionSlot}, roster index {RosterIndex}",
                name,
                member.SessionSlot,
                member.RosterIndex);
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

    /// <summary>
    /// Notes the lobby-slot sequence a peer's own exchange named.
    /// </summary>
    /// <remarks>
    /// <b>The sequence is the peer's and not the room's.</b> A client answers the
    /// arrays under a sequence of its own — <c>82 NN</c>/<c>84 NN</c>, the same
    /// byte its <c>83 NN</c> request names — and every reference host hands that
    /// byte back in the arrays it publishes: `started-briefing-then-solo-game`
    /// answers <c>82 8c</c> and its run carries <c>85 8c</c>/<c>86 8c</c>, while
    /// `mgo2-game1`'s carries <c>85 f3</c> for a client that answered
    /// <c>f3</c>. A host that publishes one captured client's byte to every
    /// client names a pair in an exchange the new client never opened, which is
    /// what leaves it unplaced in the room.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the peer is filed under.</param>
    /// <param name="sequence">Sequence byte the peer's own records named.</param>
    public void NotePairSequence(string remoteAddress, byte sequence)
    {
        lock (gate)
        {
            pairSequences[remoteAddress] = sequence;
        }
    }

    /// <summary>
    /// The lobby-slot sequence a peer's own exchange is on, or the captured room's
    /// sequence for a peer that has not named one yet.
    /// </summary>
    /// <remarks>
    /// The fallback is what a peer is published before its first answer arrives —
    /// the run answers its entry, which can precede the answer to the pair — and
    /// it is the value <see cref="TeamArrayFramesUtils.OpeningSequence"/> already
    /// carries.
    /// </remarks>
    /// <param name="remoteAddress">Endpoint the peer is filed under.</param>
    /// <returns>The sequence the peer's arrays are published under.</returns>
    public byte PairSequenceOf(string remoteAddress)
    {
        lock (gate)
        {
            return pairSequences.TryGetValue(remoteAddress, out var sequence)
                ? sequence
                : TeamArrayFramesUtils.OpeningSequence;
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
            pairSequences.Remove(remoteAddress);
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
    /// <see cref="UdpCommandConstants.PlayerProfile"/>, the close record, the
    /// room's count and the answered peer's own settings payload last.
    /// </summary>
    /// <param name="answeringLoadout">
    /// The answered peer's settings payload, or <c>null</c> to close the run on
    /// the room's count.
    /// </param>
    /// <returns>The records in send order, each with the type it travels as.</returns>
    public List<RosterRecord> BuildRosterRun(
        byte[]? answeringLoadout = null,
        bool includeEndpointPairs = true) =>
        RoomRosterFramesUtils.BuildRun(Snapshot(), hostIdentity, answeringLoadout, includeEndpointPairs);
}
