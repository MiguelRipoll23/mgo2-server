using System.ComponentModel.DataAnnotations;
using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// Fields of a request to create a testing team whose players come from the
/// test-character pool.
/// <para>
/// The mode names the lobby rather than the identifier, because the client that
/// shows the team chooses the lobby it is in; the HTTP API resolves the running
/// lobby of that mode and hands the request down its stream.
/// </para>
/// </summary>
public sealed class FakeTeamCreateRequest
{
    /// <summary>Lobby mode the team is created in: 4 Survival, 3 Tournament or 10 registration.</summary>
    [Range(1, 255)]
    public int Mode { get; set; } = EventConstants.SurvivalSelector;

    /// <summary>How many players the team holds, leader included.</summary>
    [Range(1, EventConstants.TeamMemberLimit)]
    public int Count { get; set; } = EventConstants.TeamMemberLimit;

    /// <summary>Display name the team is given, or blank for a composed one.</summary>
    [StringLength(64)]
    public string TeamName { get; set; } = string.Empty;

}

/// <summary>Fields of a request to add fake players to a team that already exists.</summary>
public sealed class FakePlayerAddRequest
{
    /// <summary>Lobby mode the team is in: 4 Survival, 3 Tournament or 10 registration.</summary>
    [Range(1, 255)]
    public int Mode { get; set; } = EventConstants.SurvivalSelector;

    /// <summary>How many players to add.</summary>
    [Range(1, EventConstants.TeamMemberLimit)]
    public int Count { get; set; } = 1;

    /// <summary>Name of the existing team the players join. It is required.</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string TeamName { get; set; }

}

/// <summary>
/// Fields of a request to change the state of a team that exists only in a
/// lobby's memory, and optionally of its whole roster.
/// </summary>
public sealed class FakeTeamStateChangeRequest
{
    /// <summary>Lobby mode the team is in: 4 Survival, 3 Tournament or 10 registration.</summary>
    [Range(1, 255)]
    public int Mode { get; set; } = EventConstants.SurvivalSelector;

    /// <summary>Name of the team whose state is changed. It is required.</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string TeamName { get; set; }

    /// <summary>Team state to store, as the client's own phase byte.</summary>
    [Range(0, 255)]
    public int State { get; set; }

    /// <summary>Member state to force on the roster, or zero to let each member follow the team state.</summary>
    [Range(0, 255)]
    public int MemberState { get; set; }
}

/// <summary>
/// Fields of a request to change the entry-decision byte on the fake players of
/// a team that has a row.
/// <para>
/// It is a request of its own rather than a mode of the state one, because a
/// team has a state the entry pipeline owns: a testing device
/// may move a fake player's decision, which nobody else can, and may not move
/// the team's own state or a real member's.
/// </para>
/// <para>
/// It is called <c>FakeTeamMemberStateChangeRequest</c> rather than
/// <c>FakeTeamMemberStateRequest</c> because the coordination protocol already
/// has a message of the shorter name, and a contract that shadowed it would
/// force every file holding both to disambiguate a name it did not choose.
/// </para>
/// </summary>
public sealed class FakeTeamMemberStateChangeRequest
{
    /// <summary>Lobby mode the team is in: 4 Survival, 3 Tournament or 10 registration.</summary>
    [Range(1, 255)]
    public int Mode { get; set; } = EventConstants.SurvivalSelector;

    /// <summary>Name of the team whose fake players change. It is required.</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string TeamName { get; set; }

    /// <summary>
    /// Member state to store on each fake player: 1 pending, which the client
    /// paints NG, or 2 ready, which it paints OK.
    /// </summary>
    [Range(0, 255)]
    public int MemberState { get; set; } = EventConstants.ParticipantReadyState;
}

/// <summary>
/// Fields of a request to create a dedicated event host room in a lobby.
/// <para>
/// A pairing is written when two teams are queued but is not announced until a
/// room has been leased for it, so this is what carries a testing pairing from
/// "paired" to the point where the client is actually told about it.
/// </para>
/// </summary>
public sealed class FakeHostRoomCreateRequest
{
    /// <summary>Mode of the lobby the room belongs to: 4 Survival or 3 Tournament.</summary>
    [Range(1, 255)]
    public int Mode { get; set; } = EventConstants.SurvivalSelector;

    /// <summary>
    /// Character to host the room, or zero to let the lobby pick one. The room's
    /// host is a real character, because the column is a foreign key and the
    /// host-eligibility rule requires the host to be in the room. Character zero
    /// cannot host a room in either sense, so asking for it by name picks
    /// instead rather than furnishing a room nothing would ever lease.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int HostCharacterIdentifier { get; set; }
}

/// <summary>What a fake-event request did, as the caller is told it.</summary>
/// <param name="Message">Sentence describing the outcome.</param>
public sealed record FakeEventResult(string Message);

/// <summary>Fields of a request to add characters to the test-character pool.</summary>
public sealed class TestCharacterAddRequest
{
    /// <summary>How many test characters to add.</summary>
    [Range(1, TestPlayerNameUtils.MaximumPerAdd)]
    public int Count { get; set; } = 1;
}

/// <summary>One test character as a caller is told about it.</summary>
/// <param name="Identifier">Row identifier of the character.</param>
/// <param name="Name">Name the character is shown with.</param>
/// <param name="InTeam">Whether a team's roster currently holds it.</param>
public sealed record TestCharacterEntry(int Identifier, string Name, bool InTeam)
{
    /// <summary>Projects one pool entry into what a caller reads.</summary>
    /// <param name="entry">Entry the pool reported.</param>
    public static TestCharacterEntry Of(TestCharacterPoolEntry entry) =>
        new(entry.Identifier, entry.Name, entry.InTeam);
}

/// <summary>The test characters the server is holding.</summary>
/// <param name="Characters">The characters, oldest first.</param>
public sealed record TestCharacterPoolResult(IReadOnlyList<TestCharacterEntry> Characters);
