using System.ComponentModel.DataAnnotations;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.InternalGrpc.Contracts;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// Fields of a request to create a team that exists only in a lobby's memory.
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

    /// <summary>Name the players are shown with, or blank to let the lobby compose one.</summary>
    [StringLength(64)]
    public string PlayerPrefix { get; set; } = string.Empty;
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

    /// <summary>Name the players are shown with, or blank to let the lobby compose one.</summary>
    [StringLength(64)]
    public string PlayerPrefix { get; set; } = string.Empty;
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

    /// <summary>Name of the in-memory team whose state is changed. It is required.</summary>
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
/// Fields of a request to make one in-memory team pairable by writing it out
/// as a row.
/// <para>
/// It is the only request here that persists anything, and the reason is
/// downstream of it rather than in it: a pairing is a durable row naming two
/// teams, and every service that reads one re-reads both sides as rows. The
/// request itself is as narrow as the others — a team and a mode.
/// </para>
/// <para>
/// It is called <c>FakeTeamPromotionRequest</c> rather than
/// <c>FakeTeamPairingRequest</c> because the coordination protocol already has
/// a message of that name, and a contract that shadowed it would force every
/// file holding both to disambiguate a name it did not choose.
/// </para>
/// </summary>
public sealed class FakeTeamPromotionRequest
{
    /// <summary>
    /// Mode of the lobby the team is in. Only 4, Survival, is served: a
    /// Tournament entrant is seeded and frozen, which a memory-only team cannot
    /// be.
    /// </summary>
    [Range(1, 255)]
    public int Mode { get; set; } = EventConstants.SurvivalSelector;

    /// <summary>Name of the in-memory team to write out and queue. It is required.</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public required string TeamName { get; set; }
}

/// <summary>
/// Fields of a request to change the entry-decision byte on the fake players of
/// a team that has a row.
/// <para>
/// It is a request of its own rather than a mode of the in-memory state one,
/// because a stored team has a state the entry pipeline owns: a testing device
/// may move a fake player's decision, which nobody else can, and may not move
/// the team's own state or a real member's.
/// </para>
/// <para>
/// It is called <c>FakeTeamMemberStateChangeRequest</c> rather than
/// <c>FakeTeamMemberStateRequest</c> for the reason
/// <see cref="FakeTeamPromotionRequest"/> gives: the coordination protocol
/// already has a message of the shorter name, and a contract that shadowed it
/// would force every file holding both to disambiguate a name it did not
/// choose.
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
    /// host-eligibility rule requires the host to be in the room.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int HostCharacterIdentifier { get; set; }
}

/// <summary>What a fake-event request did, as the caller is told it.</summary>
/// <param name="Message">Sentence describing the outcome.</param>
public sealed record FakeEventResult(string Message);

/// <summary>
/// One in-memory team as a caller is told about it. It carries the name rather
/// than the identifier, because the name is what the other requests take.
/// </summary>
/// <param name="Name">Display name of the team.</param>
/// <param name="State">Lifecycle state the client reads.</param>
/// <param name="Members">How many players the roster holds, leader included.</param>
public sealed record FakeTeamEntry(string Name, int State, int Members)
{
    /// <summary>Projects the answer a lobby gave into what a caller reads.</summary>
    /// <param name="summary">Team the lobby reported.</param>
    public static FakeTeamEntry Of(FakeTeamSummary summary) =>
        new(summary.TeamName, summary.State, summary.MemberCount);
}

/// <summary>
/// The in-memory teams one lobby is holding, as a caller is told about them.
/// </summary>
/// <param name="Mode">Lobby mode that was asked about.</param>
/// <param name="Teams">Teams the lobby reported, in the order it holds them.</param>
public sealed record FakeTeamListingResult(int Mode, IReadOnlyList<FakeTeamEntry> Teams);
