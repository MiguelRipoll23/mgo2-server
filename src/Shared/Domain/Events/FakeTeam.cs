namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// One player that exists only in the lobby's memory.
/// </summary>
/// <param name="CharacterIdentifier">Identifier the client protocol carries.</param>
/// <param name="Name">Name the client shows.</param>
/// <param name="TeamIdentifier">Team the player is in, or zero when it is in none.</param>
public readonly record struct FakePlayer(int CharacterIdentifier, string Name, int TeamIdentifier);

/// <summary>
/// One team that exists only in the lobby's memory.
/// <para>
/// A fake team is the memory twin of an <c>event_teams</c> row, with the same
/// shape the team screens read: a name, a lifecycle state, a leader and a
/// roster of slots led by slot zero. Nothing about it is persisted, so a lobby
/// restart forgets it the way it forgets a fake player — a team that outlived
/// the process that made it would look exactly like a real one.
/// </para>
/// <para>
/// It is projected into the same <see cref="EventSnapshot"/> the real teams are,
/// so the list and detail replies do not have to know where a team came from.
/// </para>
/// </summary>
public sealed class FakeTeam
{
    /// <summary>Experience fake players are shown with, roughly a mid-level rank.</summary>
    public const int DefaultExperience = 18_000;

    private readonly List<FakePlayer> members = [];

    /// <summary>Identifier the client caches, from the fake range.</summary>
    public required int Identifier { get; init; }

    /// <summary>Lobby the team belongs to.</summary>
    public required int LobbyIdentifier { get; init; }

    /// <summary>Event the team is filed under.</summary>
    public required int EventIdentifier { get; init; }

    /// <summary>Match type of the team, which is the lobby selector.</summary>
    public required int Mode { get; init; }

    /// <summary>Display name of the team.</summary>
    public required string Name { get; init; }

    /// <summary>Lifecycle state the client reads.</summary>
    public int State { get; private set; } = EventConstants.TeamJoinableState;

    /// <summary>
    /// Member state forced on the whole roster, or null when each member follows
    /// the team's own state. It exists so a moderator can exercise the client's
    /// per-member decision byte without a second player to press the button.
    /// </summary>
    public int? ParticipantStateOverride { get; private set; }

    /// <summary>Sequence the client echoes back on roster mutations.</summary>
    public int Sequence { get; private set; } = 1;

    /// <summary>Character leading the team, which is slot zero.</summary>
    public int OwnerCharacterIdentifier { get; private set; }

    /// <summary>Slots of the roster, leader first.</summary>
    public IReadOnlyList<FakePlayer> Members => members;

    /// <summary>Adds a player to the first free slot, when one is free.</summary>
    /// <param name="playerIdentifier">Identifier the player is given.</param>
    /// <param name="name">Name the player is shown with.</param>
    /// <returns>Whether the roster had room.</returns>
    public bool TryAddMember(int playerIdentifier, string name)
    {
        if (members.Count >= EventConstants.TeamMemberLimit)
        {
            return false;
        }

        members.Add(new FakePlayer(playerIdentifier, name, Identifier));
        if (members.Count == 1)
        {
            OwnerCharacterIdentifier = playerIdentifier;
        }

        Sequence++;
        return true;
    }

    /// <summary>Moves the team to a new lifecycle state.</summary>
    /// <param name="state">State to store, as the client's own phase byte.</param>
    public void SetState(int state)
    {
        State = state;
        Sequence++;
    }

    /// <summary>Forces a state on the whole roster, or lets it follow the team.</summary>
    /// <param name="participantState">State to force, or null to follow the team state.</param>
    public void SetParticipantState(int? participantState)
    {
        ParticipantStateOverride = participantState;
        Sequence++;
    }

    /// <summary>Projects the team into the active-game snapshot the client reads.</summary>
    public EventSnapshot BuildSnapshot()
    {
        var snapshot = new EventSnapshot
        {
            SnapshotIdentifier = Identifier,
            Sequence = Sequence,
            State = State,
            Name = Name,
            LobbyIdentifier = LobbyIdentifier,
            MatchType = Mode,
            EventIdentifier = EventIdentifier,
            HostIdentifier = OwnerCharacterIdentifier,
        };

        // The member state is the one the client accepts for the team's own
        // state: a roster addition to an open team is carrying a member that has
        // not decided yet, and the client drops the notification otherwise. A
        // moderator may override it to exercise each value directly.
        var memberState = ParticipantStateOverride
            ?? EventTeamRegistrationUtils.ParticipantStateFor(State);
        for (var slot = 0; slot < members.Count && slot < snapshot.Participants.Length; slot++)
        {
            var participant = snapshot.Participants[slot];
            participant.CharacterIdentifier = members[slot].CharacterIdentifier;
            participant.Name = members[slot].Name;
            participant.State = memberState;
            participant.Experience = DefaultExperience;
            snapshot.ParticipantStates[slot] = (byte)memberState;

            if (slot == 0)
            {
                snapshot.HostName = members[slot].Name;
            }
        }

        return snapshot;
    }
}
