using Mgo2Server.Shared.Constants;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// The event a newly formed team is placed in.
/// <para>
/// The create request names no event: the client zeroes the lobby id and the
/// rule before sending and stops at the last field of the record, so the event
/// has to be decided by the lobby the team was formed in. Survival is the easy
/// case — it has one standing information record and every team in the lobby
/// joins that one. The Tournament registration lobby is different, because its
/// events are scheduled and a team belongs to the specific one the player was
/// looking at when they formed it.
/// </para>
/// </summary>
public static class EventTeamCreationUtils
{
    /// <summary>
    /// Size of the create request, in bytes. The record is the first 168 bytes
    /// of the shared team record and stops there.
    /// </summary>
    public const int CreateRequestWireSize = 168;

    /// <summary>
    /// Whether an arrival is the create record.
    /// <para>
    /// The size is exact, and it is the whole request. The client zeroes the
    /// lobby id and the rule before sending and appends nothing after the last
    /// field, so a longer arrival is not this command's record — and a refusal
    /// is silent, so accepting a wrong shape would file a team from whatever
    /// the length happened to contain.
    /// </para>
    /// </summary>
    /// <param name="payloadLength">Length of the request payload.</param>
    /// <returns><c>true</c> when the arrival is the create record.</returns>
    public static bool IsExpectedCreateRequestShape(int payloadLength) =>
        payloadLength == CreateRequestWireSize;

    /// <summary>Decides which event a team formed in a lobby belongs to.</summary>
    /// <param name="lobbySubtype">Subtype of the lobby the team forms in.</param>
    /// <param name="selectedEventIdentifier">Event whose screen the connection last opened, if any.</param>
    /// <param name="eventIdentifier">The event the team belongs to, when the request is answerable.</param>
    /// <returns>
    /// <c>true</c> when the team can be placed; <c>false</c> when the lobby holds
    /// specific events and the connection has not selected one.
    /// </returns>
    public static bool TryResolveEventIdentifier(
        int lobbySubtype,
        int? selectedEventIdentifier,
        out int eventIdentifier)
    {
        // Whether a team joins one standing event or a scheduled one is a
        // property of the lobby, not something the client gets to ask for.
        if (lobbySubtype != LobbySubtypeConstants.TournamentRegistration)
        {
            eventIdentifier = EventConstants.TransientEventIdentifier;
            return true;
        }

        // Nothing is selected, so there is no event this team could be said to
        // have entered. The player has to open an event's screen first, and
        // this is refused rather than defaulted into the transient record: a
        // team silently filed under the wrong event would be invisible to the
        // bracket it was meant to enter.
        if (selectedEventIdentifier is not > 0)
        {
            eventIdentifier = 0;
            return false;
        }

        eventIdentifier = selectedEventIdentifier.Value;
        return true;
    }
}
