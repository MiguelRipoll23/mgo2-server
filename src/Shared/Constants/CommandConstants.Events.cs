namespace Mgo2Server.Shared.Constants;

/// <summary>
/// Command identifiers of the event subsystem — Survival (lobby subtype 4),
/// Tournament (3) and Tournament registration (10). They are split from the
/// rest of the gameplay lobby because the family is served by its own domain.
/// <para>
/// The block is post-launch content: the client is wired to parse every reply
/// here, but the server answered none of it before this domain existed. Sized
/// layouts live with the codecs; this file is only the index.
/// </para>
/// </summary>
public static partial class CommandConstants
{
    // Event information and team-create framing -----------------------------

    /// <summary>Returns the event information for the connected lobby's selector.</summary>
    public const ushort GetEventInformation = 0x4908;

    /// <summary>Carries the 905-byte event information record.</summary>
    public const ushort GetEventInformationResult = 0x4909;

    /// <summary>Returns the event information for one cached event identifier.</summary>
    public const ushort GetEventInformationById = 0x4904;

    /// <summary>Carries the 905-byte event information record for one identifier.</summary>
    public const ushort GetEventInformationByIdResult = 0x4905;

    /// <summary>Asks for a saved team-creation preset.</summary>
    public const ushort GetTeamCreateInformation = 0x4348;

    /// <summary>Carries the saved team-creation preset, or its absence.</summary>
    public const ushort GetTeamCreateInformationResult = 0x4349;

    // Survival battle list --------------------------------------------------

    /// <summary>Requests the Survival battle list.</summary>
    public const ushort GetSurvivalBattleList = 0x4E00;

    /// <summary>Opens the Survival battle-list reply stream.</summary>
    public const ushort GetSurvivalBattleListStart = 0x4E10;

    /// <summary>Carries one team row of the Survival battle list.</summary>
    public const ushort GetSurvivalBattleListPage = 0x4E11;

    /// <summary>Closes the Survival battle-list reply stream.</summary>
    public const ushort GetSurvivalBattleListEnd = 0x4E12;

    /// <summary>Requests the detail of one listed battle team.</summary>
    public const ushort GetBattleTeamInformation = 0x49B0;

    /// <summary>Carries one listed battle team's detail.</summary>
    public const ushort GetBattleTeamInformationResult = 0x49B1;

    // Teams -----------------------------------------------------------------

    /// <summary>Creates an event team.</summary>
    public const ushort CreateEventTeam = 0x4910;

    /// <summary>Carries the created team's active-game snapshot.</summary>
    public const ushort CreateEventTeamResult = 0x4911;

    /// <summary>Joins an event team.</summary>
    public const ushort JoinEventTeam = 0x4912;

    /// <summary>Carries the joined team's active-game snapshot.</summary>
    public const ushort JoinEventTeamResult = 0x4913;

    /// <summary>Disbands a team, or leaves one.</summary>
    public const ushort LeaveEventTeam = 0x4914;

    /// <summary>Carries the result of a disband or leave.</summary>
    public const ushort LeaveEventTeamResult = 0x4915;

    /// <summary>Records a team member's entry decision.</summary>
    public const ushort SetEventEntryDecision = 0x4930;

    /// <summary>Carries the result of an entry decision.</summary>
    public const ushort SetEventEntryDecisionResult = 0x4931;

    /// <summary>Lists the joinable teams of the lobby.</summary>
    public const ushort GetEventTeamList = 0x4980;

    /// <summary>Opens the joinable team-list reply stream.</summary>
    public const ushort GetEventTeamListStart = 0x4981;

    /// <summary>Carries one joinable team row.</summary>
    public const ushort GetEventTeamListPage = 0x4982;

    /// <summary>Closes the joinable team-list reply stream.</summary>
    public const ushort GetEventTeamListEnd = 0x4983;

    /// <summary>Requests the detail of one joinable team.</summary>
    public const ushort GetEventTeamDetails = 0x4984;

    /// <summary>Carries one joinable team's detail.</summary>
    public const ushort GetEventTeamDetailsResult = 0x4985;

    // Invitations -----------------------------------------------------------

    /// <summary>Invites characters to the sender's team.</summary>
    public const ushort InviteEventTeamMembers = 0x49C0;

    /// <summary>Delivers an invitation notification to a client.</summary>
    public const ushort InviteEventTeamMembersResult = 0x49C1;

    /// <summary>Answers an invitation.</summary>
    public const ushort AnswerEventTeamInvitation = 0x49C2;

    /// <summary>Carries the result of answering an invitation.</summary>
    public const ushort AnswerEventTeamInvitationResult = 0x49C3;

    /// <summary>Reports the invitation state the client is displaying.</summary>
    public const ushort ReportEventInvitationStatus = 0x49C7;

    /// <summary>Carries the result of an invitation status report.</summary>
    public const ushort ReportEventInvitationStatusResult = 0x49C8;

    // Entry, cancellation and assignment ------------------------------------

    /// <summary>Enters an event, or cancels a Survival entry.</summary>
    public const ushort EnterEvent = 0x4A25;

    /// <summary>Carries the result of an entry or cancellation.</summary>
    public const ushort EnterEventResult = 0x4A26;

    /// <summary>Requests the active-game snapshot of the sender's team.</summary>
    public const ushort GetActiveGameSnapshot = 0x4986;

    /// <summary>Carries the active-game snapshot.</summary>
    public const ushort GetActiveGameSnapshotResult = 0x4987;

    /// <summary>Confirms an active-game assignment.</summary>
    public const ushort ConfirmActiveGameAssignment = 0x491B;

    /// <summary>Carries the result of an assignment confirmation.</summary>
    public const ushort ConfirmActiveGameAssignmentResult = 0x491C;

    /// <summary>Carries the submitted active-team record.</summary>
    public const ushort ActiveTeamSubmitted = 0x491D;

    /// <summary>Removes an event game entry or reservation.</summary>
    public const ushort RemoveEventGameEntry = 0x4992;

    /// <summary>Carries the result of removing a game entry.</summary>
    public const ushort RemoveEventGameEntryResult = 0x4993;

    /// <summary>Requests the detail of an assigned or reserved game.</summary>
    public const ushort GetAssignedGameDetail = 0x4F08;

    /// <summary>Carries an assigned or reserved game's detail.</summary>
    public const ushort GetAssignedGameDetailResult = 0x4F09;

    /// <summary>Requests the member information of an assigned game.</summary>
    public const ushort GetAssignedMemberInformation = 0x4F17;

    /// <summary>Carries the member information of an assigned game.</summary>
    public const ushort GetAssignedMemberInformationResult = 0x4F18;

    /// <summary>Reserves a Tournament registration place.</summary>
    public const ushort ReserveTournamentEntry = 0x4F00;

    /// <summary>Carries the result of a Tournament reservation.</summary>
    public const ushort ReserveTournamentEntryResult = 0x4F01;

    // Event list and detail -------------------------------------------------

    /// <summary>Lists the events of the connected lobby.</summary>
    public const ushort GetEventList = 0x4A40;

    /// <summary>Opens the event-list reply stream.</summary>
    public const ushort GetEventListStart = 0x4A10;

    /// <summary>Carries one event-list row.</summary>
    public const ushort GetEventListPage = 0x4A11;

    /// <summary>Closes the event-list reply stream.</summary>
    public const ushort GetEventListEnd = 0x4A12;

    /// <summary>Requests the detail of one event.</summary>
    public const ushort GetEventDetail = 0x4A30;

    /// <summary>Carries the Survival event detail.</summary>
    public const ushort GetEventDetailResult = 0x4A31;

    /// <summary>Syncs the event view state the client is showing.</summary>
    public const ushort SyncEventViewState = 0x49D0;

    /// <summary>Carries the result of syncing the event view state.</summary>
    public const ushort SyncEventViewStateResult = 0x49D1;

    // Server-pushed notifications -------------------------------------------

    /// <summary>Pushes an added team participant to every member.</summary>
    public const ushort EventParticipantAdded = 0x4918;

    /// <summary>Pushes a removed team participant to every member.</summary>
    public const ushort EventParticipantRemoved = 0x4919;

    /// <summary>Pushes a changed participant decision to every member.</summary>
    public const ushort EventParticipantDecision = 0x4932;

    /// <summary>Clears the active team on every remaining member.</summary>
    public const ushort EventTeamCleared = 0x491A;

    /// <summary>Pushes a formed Survival match to both teams.</summary>
    public const ushort EventMatchFound = 0x4E20;

    /// <summary>Returns the losing Survival team to the waiting state.</summary>
    public const ushort EventMatchLoserReturn = 0x4E21;

    /// <summary>Advances the winning Survival team to the next round.</summary>
    public const ushort EventMatchWinnerContinue = 0x4E22;

    /// <summary>Pushes a Survival state update to one team.</summary>
    public const ushort EventStateUpdate = 0x4E23;

    /// <summary>Starts the assigned active event in the client.</summary>
    public const ushort ActiveEventStart = 0x4A00;

    /// <summary>Pushes one round of the bracket and the entrant status column.</summary>
    public const ushort EventRoundResult = 0x4A20;

    /// <summary>Pushes the whole bracket, one row per round.</summary>
    public const ushort EventBracketState = 0x4A21;

    /// <summary>Pushes the final standings and the reward a team was paid.</summary>
    public const ushort EventFinalStandings = 0x4A28;

    /// <summary>Initialises the assigned event game in the client.</summary>
    public const ushort EventGameInitialize = 0x43F0;

    /// <summary>Initialises the assigned event game for its host.</summary>
    public const ushort EventGameHostInitialize = 0x43F1;

    // Screens whose success body is unknown --------------------------------

    /// <summary>A directly reachable Survival screen whose body is unrecovered.</summary>
    public const ushort GetSurvivalAdjacentList = 0x49E0;

    /// <summary>Carries the result of <see cref="GetSurvivalAdjacentList"/>.</summary>
    public const ushort GetSurvivalAdjacentListResult = 0x49E1;

    /// <summary>A directly reachable Survival screen whose body is unrecovered.</summary>
    public const ushort GetTournamentAdjacentList = 0x4F13;

    /// <summary>Carries the result of <see cref="GetTournamentAdjacentList"/>.</summary>
    public const ushort GetTournamentAdjacentListResult = 0x4F14;

    /// <summary>A directly reachable Survival screen whose body is unrecovered.</summary>
    public const ushort GetEventAdjacentDetail = 0x4920;

    /// <summary>Carries the result of <see cref="GetEventAdjacentDetail"/>.</summary>
    public const ushort GetEventAdjacentDetailResult = 0x4921;

    /// <summary>A directly reachable Survival screen whose body is unrecovered.</summary>
    public const ushort GetEventAdjacentState = 0x4923;

    /// <summary>Carries the result of <see cref="GetEventAdjacentState"/>.</summary>
    public const ushort GetEventAdjacentStateResult = 0x4924;

    /// <summary>A directly reachable Survival screen whose body is unrecovered.</summary>
    public const ushort GetEventAdjacentEntry = 0x4F02;

    /// <summary>Carries the result of <see cref="GetEventAdjacentEntry"/>.</summary>
    public const ushort GetEventAdjacentEntryResult = 0x4F03;

    /// <summary>A directly reachable Survival screen whose body is unrecovered.</summary>
    public const ushort GetEventAdjacentTeam = 0x49A0;

    /// <summary>Carries the result of <see cref="GetEventAdjacentTeam"/>.</summary>
    public const ushort GetEventAdjacentTeamResult = 0x49A1;
}
