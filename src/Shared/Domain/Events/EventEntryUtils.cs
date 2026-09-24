namespace Mgo2Server.Shared.Domain.Events;

/// <summary>What a shared entry request asks for, given where it arrived.</summary>
public enum EventEntryRoute
{
    /// <summary>Submit the caller's team into a Tournament event.</summary>
    SubmitTournamentTeam,

    /// <summary>
    /// Enter the caller itself into an event: the individual entry the client
    /// makes from an event's detail screen in a Tournament or Survival lobby.
    /// </summary>
    EnterEventSolo,

    /// <summary>Withdraw the caller's Survival team from the waiting field.</summary>
    CancelSurvivalEntry,

    /// <summary>An entry this server does not serve.</summary>
    Unsupported,
}

/// <summary>
/// The rule that reads one request as four different operations, because the
/// client sends the same empty command for all of them and states which it means
/// by the screen it is on.
/// <para>
/// It is separated from the handler because getting it wrong is silent: the
/// wrong branch cancels a team whose player asked to enter, or submits a team
/// into an event the player was only reading about.
/// </para>
/// </summary>
public static class EventEntryUtils
{
    /// <summary>Decides what an entry request means.</summary>
    /// <param name="lobbySubtype">Game type of the lobby the request arrived in.</param>
    /// <param name="hasTeam">Whether the connection is attached to a team.</param>
    /// <param name="hasSelectedEvent">Whether the connection has an event's detail open.</param>
    public static EventEntryRoute Resolve(
        int lobbySubtype,
        bool hasTeam,
        bool hasSelectedEvent)
    {
        if (lobbySubtype == EventConstants.TournamentRegistrationSelector)
        {
            // The registration lobby has no individual reading at all: its
            // request is always a team submission, and the caller's team and
            // event are what it is submitted with.
            return hasTeam && hasSelectedEvent
                ? EventEntryRoute.SubmitTournamentTeam
                : EventEntryRoute.Unsupported;
        }

        if (lobbySubtype == EventConstants.TournamentSelector)
        {
            // The Tournament lobby's own entry is the individual one, and only
            // when an event's detail is open: without one there is no event the
            // player could have asked to enter.
            return hasSelectedEvent
                ? EventEntryRoute.EnterEventSolo
                : EventEntryRoute.Unsupported;
        }

        if (lobbySubtype != EventConstants.SurvivalSelector)
        {
            return EventEntryRoute.Unsupported;
        }

        // A selected event is newer evidence of intent than a team waiting: the
        // player opened an event's detail and asked to enter it. Withdrawing the
        // team here would answer the opposite of what was asked, so the
        // individual entry is chosen even when a team is attached.
        if (hasSelectedEvent)
        {
            return EventEntryRoute.EnterEventSolo;
        }

        // Without a team and without an event there is no waiting state to
        // withdraw and no event to enter.
        return hasTeam ? EventEntryRoute.CancelSurvivalEntry : EventEntryRoute.Unsupported;
    }

    /// <summary>
    /// Whether an event's window is open. The end is exclusive, because the
    /// window is a daily interval and an inclusive end would leave the first
    /// moment of the next day inside two events.
    /// </summary>
    /// <param name="nowSeconds">Moment being tested.</param>
    /// <param name="startSeconds">Start of the window.</param>
    /// <param name="endSeconds">End of the window.</param>
    public static bool IsWindowOpen(long nowSeconds, int startSeconds, int endSeconds) =>
        nowSeconds >= startSeconds && nowSeconds < endSeconds;

    /// <summary>
    /// Whether a team the character already owns is the one a repeat entry
    /// belongs to. Reusing it is what makes a repeated entry idempotent: a second
    /// request must not create a second entrant for one character, and it must
    /// not enter a roster the character formed with other players either.
    /// </summary>
    /// <param name="occupiedMemberCount">Members the owned team has.</param>
    /// <param name="teamEventIdentifier">Event the owned team belongs to.</param>
    /// <param name="eventIdentifier">Event being entered.</param>
    public static bool IsReusableEntrantTeam(
        int occupiedMemberCount,
        int teamEventIdentifier,
        int eventIdentifier) =>
        occupiedMemberCount == 1 && teamEventIdentifier == eventIdentifier;
}
