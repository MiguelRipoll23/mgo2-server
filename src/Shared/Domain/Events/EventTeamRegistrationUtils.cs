using Mgo2Server.Shared.Persistence.Entities;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The rules that decide what state a team is marked with when it enters or
/// leaves the matchmaking queue.
/// <para>
/// They are here rather than inline in the queue because they are the part a
/// client reads back: the joinable list is served from this state by another
/// process, so "waiting" and "joinable" have to mean exactly one thing each.
/// </para>
/// </summary>
public static class EventTeamRegistrationUtils
{
    /// <summary>
    /// The state a team holds once it is entered and waiting for an opponent. A
    /// team that has decided to play is no longer up for grabs, so it leaves the
    /// joinable list the client offers to join from.
    /// </summary>
    public static int EnteredState => EventConstants.TeamEnteredState;

    /// <summary>
    /// The state a team returns to when it leaves the field, or <c>null</c> when
    /// it is not marked as entered and so has nothing to be released from.
    /// </summary>
    /// <param name="currentState">State the team is recorded in.</param>
    public static int? ReleasedState(int currentState) =>
        currentState == EventConstants.TeamEnteredState
            ? EventConstants.TeamJoinableState
            : null;

    /// <summary>
    /// Whether every occupied slot of a roster has decided to play.
    /// </summary>
    /// <param name="members">Roster of the team.</param>
    /// <returns>Whether the roster holds at least one member and all are ready.</returns>
    public static bool IsReady(IEnumerable<EventTeamMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var occupied = 0;
        foreach (var member in members)
        {
            if (member.CharacterIdentifier == 0)
            {
                continue;
            }

            occupied++;
            if (member.State != EventConstants.ParticipantReadyState)
            {
                return false;
            }
        }

        return occupied > 0;
    }

    /// <summary>
    /// The member state a team notification may carry for a team in the given
    /// state.
    /// <para>
    /// The client refuses a member whose state byte is not the one the team's own
    /// state expects — <c>1</c> on any team, and <c>2</c> only on one that was
    /// formed automatically — so a roster addition that arrives with any other
    /// value is dropped with no error and no visible change. Deriving the byte
    /// here keeps every writer of a roster notification in step with that rule,
    /// and it is why the auto-formation state is the only one that may turn a
    /// row's byte into a decision: an entered team's members arrive pending and
    /// become ready through <c>0x4932</c>, not through the row that adds them.
    /// </para>
    /// </summary>
    /// <param name="teamState">State the team is recorded in.</param>
    /// <returns>The member state the client accepts for that team state.</returns>
    public static int ParticipantStateFor(int teamState) =>
        teamState == EventConstants.TeamAutoFormationState
            ? EventConstants.ParticipantReadyState
            : EventConstants.ParticipantPendingState;

    /// <summary>
    /// Whether a queued team may be handed to an opponent that has just entered
    /// the queue. Readiness alone is not enough: a team that was released, or
    /// that this queue never marked, is joinable again and must not be paired
    /// with a team that queued after it.
    /// </summary>
    /// <param name="currentState">State the candidate team is recorded in.</param>
    /// <param name="isReady">Whether every occupied slot has decided to play.</param>
    /// <param name="lobbyIdentifier">Lobby the candidate team belongs to.</param>
    /// <param name="expectedLobbyIdentifier">Lobby the incoming team belongs to.</param>
    public static bool IsPairableCandidate(
        int currentState,
        bool isReady,
        int lobbyIdentifier,
        int expectedLobbyIdentifier) =>
        currentState == EventConstants.TeamEnteredState
        && isReady
        && lobbyIdentifier == expectedLobbyIdentifier;
}
