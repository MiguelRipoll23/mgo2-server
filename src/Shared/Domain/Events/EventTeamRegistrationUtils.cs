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
    /// The state a team holds while it waits for an opponent. A team that has
    /// decided to play is no longer up for grabs, so it leaves the joinable list
    /// the client offers to join from.
    /// </summary>
    public static int QueuedState => EventConstants.TeamRegisteredState;

    /// <summary>
    /// The state a team returns to when it leaves the queue, or <c>null</c> when
    /// it is not marked as waiting and so has nothing to be released from.
    /// </summary>
    /// <param name="currentState">State the team is recorded in.</param>
    public static int? ReleasedState(int currentState) =>
        currentState == EventConstants.TeamRegisteredState
            ? EventConstants.TeamJoinableState
            : null;

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
        currentState == EventConstants.TeamRegisteredState
        && isReady
        && lobbyIdentifier == expectedLobbyIdentifier;
}
