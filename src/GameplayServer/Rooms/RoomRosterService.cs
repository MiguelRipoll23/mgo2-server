using System.Net;
using Mgo2Server.GameplayServer.Identity;
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
/// (<c>tools/replays/replay_360827_5.dat</c>, records at file offsets
/// <c>0x52</c> to <c>0x484</c>). Until the client has seen that sequence it
/// has no slot of its own and no way to place the players around it, which is
/// why it keeps retrying.
/// </para>
/// <para>
/// The host's own entry sits at <see cref="PlayerProfileRecordUtility.HostRosterIndex"/>;
/// the joining players fill 0, 1, 2 and so on, in the order they arrive. Slots
/// are handed out per remote endpoint, so a client that re-sends its profile
/// keeps the slot it was first given instead of taking a new one each retry.
/// </para>
/// </remarks>
/// <param name="hostIdentity">Identity this host presents to its peers.</param>
public sealed class RoomRosterService(HostIdentityService hostIdentity)
{
    /// <summary>Roster slot of the first joining player.</summary>
    public const sbyte FirstJoinerRosterIndex = 0;

    /// <summary>
    /// The value written at offset 8 of every record. It differs per player in
    /// every recorded room and its meaning is unresolved, so it is sent as zero
    /// rather than guessed at.
    /// </summary>
    private const ushort UnresolvedPerPlayerValue = 0;

    /// <summary>Team flag; zero in every recorded record that carries no team.</summary>
    private const byte TeamFlag = 0;

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
                existing.CharacterIdentifier = profile?.CharacterIdentifier ?? existing.CharacterIdentifier;
                existing.Name = profile is { Name.Length: > 0 } ? profile.Name : existing.Name;
                existing.ClanName = profile?.ClanName ?? existing.ClanName;
                return existing;
            }

            var member = new RosterMember(key, NextFreeIndex())
            {
                CharacterIdentifier = profile?.CharacterIdentifier ?? 0,
                Name = profile?.Name ?? string.Empty,
                ClanName = profile?.ClanName ?? string.Empty,
            };
            members.Add(member);
            return member;
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
                hostIdentity.ProfileCharacterIdentifier,
                PlayerProfileRecordUtility.HostRosterIndex,
                UnresolvedPerPlayerValue,
                TeamFlag,
                hostIdentity.AccountName,
                hostIdentity.ClanName),
        };
        records.AddRange(ordered.Select(member => member.BuildRecord(UnresolvedPerPlayerValue, TeamFlag)));
        return records;
    }

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

/// <summary>One joining player on the room roster.</summary>
/// <param name="RemoteAddress">Endpoint the player is reached at, formatted as "address:port".</param>
/// <param name="RosterIndex">Slot the player fills, counted from zero.</param>
public sealed record RosterMember(string RemoteAddress, sbyte RosterIndex)
{
    /// <summary>Character identifier the player announced.</summary>
    public byte CharacterIdentifier { get; set; }

    /// <summary>Account name the player announced.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Clan name the player announced; empty when it announced none.</summary>
    public string ClanName { get; set; } = string.Empty;

    /// <summary>Builds the player-profile record that puts this player on a peer's roster.</summary>
    /// <param name="perPlayerValue">Value written at offset 8, whose meaning is unresolved.</param>
    /// <param name="teamFlag">Team flag written at offset 10.</param>
    /// <returns>The record body.</returns>
    public byte[] BuildRecord(ushort perPlayerValue, byte teamFlag) =>
        PlayerProfileRecordUtility.Build(
            CharacterIdentifier,
            RosterIndex,
            perPlayerValue,
            teamFlag,
            Name,
            ClanName);
}
