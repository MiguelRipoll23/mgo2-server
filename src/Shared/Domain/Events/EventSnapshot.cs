namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One roster slot of an active-game snapshot. It is a projection of a team
/// member, not the member itself, so it can be built for a send without holding
/// a database row open.
/// </summary>
public sealed class EventSnapshotParticipant
{
    /// <summary>Character in the slot, or zero when the slot is empty.</summary>
    public int CharacterIdentifier { get; set; }

    /// <summary>Name shown by the team screens.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Participant state.</summary>
    public int State { get; set; }

    /// <summary>Experience used to derive the level the list screens show.</summary>
    public int Experience { get; set; }
}

/// <summary>
/// The active-game snapshot the client caches from <c>0x4911</c>, <c>0x4913</c>,
/// <c>0x4985</c> and <c>0x4987</c>. Fields whose meaning is not established keep
/// neutral names, because inventing a meaning for an unread field is how a wire
/// model starts lying.
/// <para>
/// Two shapes are written from it: the compact form the team replies use, and
/// the full form that also carries the host environment. The byte layout lives
/// in <see cref="EventSnapshotUtils"/>.
/// </para>
/// </summary>
public sealed class EventSnapshot
{
    /// <summary>Result word the reply begins with.</summary>
    public int Result { get; set; }

    /// <summary>Team correlation identifier the client caches.</summary>
    public int SnapshotIdentifier { get; set; }

    /// <summary>Sequence the client echoes back on roster mutations.</summary>
    public int Sequence { get; set; }

    /// <summary>Lifecycle state of the team.</summary>
    public int State { get; set; }

    /// <summary>Display name of the team.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Free-form comment of the team.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Option bits of the team.</summary>
    public int FlagBits { get; set; }

    /// <summary>Join password; validated on join and never serialized.</summary>
    public string JoinPassword { get; set; } = string.Empty;

    /// <summary>Host environment the full form carries.</summary>
    public EventHostEnvironment HostEnvironment { get; set; } = EventHostEnvironment.CreateDefault();

    /// <summary>Roster slots, leader first.</summary>
    public EventSnapshotParticipant[] Participants { get; } = CreateParticipants();

    /// <summary>Lobby the team belongs to.</summary>
    public int LobbyIdentifier { get; set; }

    /// <summary>Match type of the team.</summary>
    public int MatchType { get; set; }

    /// <summary>Primary equipment preset selector.</summary>
    public int PrimaryEquipmentType { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x296.</summary>
    public int Field296 { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x298.</summary>
    public int Field298 { get; set; }

    /// <summary>Character hosting the team.</summary>
    public int HostIdentifier { get; set; }

    /// <summary>Name of the hosting character.</summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>Unestablished field at snapshot offset 0x2b4.</summary>
    public int Field2B4 { get; set; }

    /// <summary>Secondary name the snapshot carries.</summary>
    public string SecondaryName { get; set; } = string.Empty;

    /// <summary>Unestablished field at snapshot offset 0x2ca.</summary>
    public int Field2CA { get; set; }

    /// <summary>Event the team entered.</summary>
    public int EventIdentifier { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x2d4.</summary>
    public int Field2D4 { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x2d8.</summary>
    public int Field2D8 { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x0a8.</summary>
    public int Field0A8 { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x0a9.</summary>
    public int Field0A9 { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x0ac.</summary>
    public int Field0AC { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x2e0.</summary>
    public int Field2E0 { get; set; }

    /// <summary>Unestablished field at snapshot offset 0x2e8.</summary>
    public int Field2E8 { get; set; }

    /// <summary>Per-participant state bytes, mirrored into the array.</summary>
    public byte[] ParticipantStates { get; } = new byte[EventConstants.SnapshotParticipantCount];

    /// <summary>Unestablished field at snapshot offset 0x2ea.</summary>
    public int Field2EA { get; set; }

    /// <summary>Consecutive wins the team carries; server-side, for the battle list.</summary>
    public int ConsecutiveWins { get; set; }

    /// <summary>Reward accumulated by the team; server-side, for the battle list.</summary>
    public int PaidReward { get; set; }

    /// <summary>Counts the occupied roster slots.</summary>
    public int OccupiedParticipantCount()
    {
        var count = 0;
        foreach (var participant in Participants)
        {
            if (participant.CharacterIdentifier != 0)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Returns the index of a character's slot, or minus one.</summary>
    /// <param name="characterIdentifier">Character to look for.</param>
    public int IndexOfParticipant(int characterIdentifier)
    {
        if (characterIdentifier == 0)
        {
            return -1;
        }

        for (var index = 0; index < Participants.Length; index++)
        {
            if (Participants[index].CharacterIdentifier == characterIdentifier)
            {
                return index;
            }
        }

        return -1;
    }

    private static EventSnapshotParticipant[] CreateParticipants()
    {
        var participants = new EventSnapshotParticipant[EventConstants.SnapshotParticipantCount];
        for (var index = 0; index < participants.Length; index++)
        {
            participants[index] = new EventSnapshotParticipant();
        }

        return participants;
    }
}
