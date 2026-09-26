using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// One half of a pairing, as the event testing tools are told about it.
/// </summary>
/// <param name="Identifier">Row identifier of the team, which a player never sees.</param>
/// <param name="Name">Display name of the team.</param>
/// <param name="Members">How many players the roster holds.</param>
/// <param name="IsFake">Whether every member is one the testing tools made.</param>
public sealed record EventMatchSideEntry(
    int Identifier,
    string Name,
    int Members,
    bool IsFake)
{
    /// <summary>Projects the half a pairing was read as.</summary>
    /// <param name="side">Half to project.</param>
    public static EventMatchSideEntry Of(EventMatchSide side) =>
        new(side.Identifier, side.Name, side.MemberCount, side.IsFake);
}

/// <summary>
/// One pairing of a lobby, as the event testing tools are told about it.
/// <para>
/// It is here because a pairing is otherwise invisible: the match-found packet is
/// only sent once a room has been leased, so two teams that have paired against
/// each other and a pair still waiting for a host look identical from the
/// outside. The state and the room name are what tell them apart.
/// </para>
/// </summary>
/// <param name="Identifier">Row identifier of the pairing.</param>
/// <param name="State">Lifecycle state of the pairing, 1 waiting for a host or 2 assigned.</param>
/// <param name="First">First of the two paired teams.</param>
/// <param name="Second">Second of the two paired teams.</param>
/// <param name="Room">Room the match is leased to, or empty while it waits for one.</param>
public sealed record EventMatchEntry(
    int Identifier,
    int State,
    EventMatchSideEntry First,
    EventMatchSideEntry Second,
    string Room)
{
    /// <summary>Projects the pairing a lobby's rows were read into.</summary>
    /// <param name="summary">Pairing to project.</param>
    public static EventMatchEntry Of(EventMatchSummary summary) =>
        new(
            summary.Identifier,
            summary.State,
            EventMatchSideEntry.Of(summary.First),
            EventMatchSideEntry.Of(summary.Second),
            summary.RoomName);
}

/// <summary>
/// The live pairings of one lobby, as the event testing tools are told about them.
/// </summary>
/// <param name="Mode">Lobby mode that was asked about.</param>
/// <param name="LobbyIdentifier">Lobby the pairings were read from.</param>
/// <param name="Matches">Pairings of that lobby, oldest first.</param>
public sealed record EventMatchListingResult(
    int Mode,
    int LobbyIdentifier,
    IReadOnlyList<EventMatchEntry> Matches);
