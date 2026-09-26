using Mgo2Server.Shared.Domain.Events;

namespace Mgo2Server.Http.Contracts;

/// <summary>
/// One formed team of a lobby, as the event testing tools are told about it.
/// <para>
/// It is separate from <see cref="FakeTeamEntry"/> because the two are different
/// things: this one has a row, survives a lobby restart, and is what the entry
/// pipeline actually queues. A tool that showed them as one list without saying
/// which was which would invite a moderator to wait for a fake team to be
/// paired.
/// </para>
/// </summary>
/// <param name="Identifier">Row identifier, which a player never sees.</param>
/// <param name="Name">Display name of the team, which the fill request takes.</param>
/// <param name="State">Lifecycle state the entry pipeline is holding it in.</param>
/// <param name="LeaderName">Name of the character in slot zero.</param>
/// <param name="Members">How many players the roster holds, leader included.</param>
public sealed record EventTeamEntry(
    int Identifier,
    string Name,
    int State,
    string LeaderName,
    int Members)
{
    /// <summary>Projects the summary a lobby's rows were read into.</summary>
    /// <param name="summary">Team the rows were read as.</param>
    public static EventTeamEntry Of(EventTeamSummary summary) =>
        new(summary.Identifier, summary.Name, summary.State, summary.LeaderName, summary.MemberCount);
}

/// <summary>
/// The formed teams of one lobby, as the event testing tools are told about them.
/// </summary>
/// <param name="Mode">Lobby mode that was asked about.</param>
/// <param name="LobbyIdentifier">Lobby the teams were read from.</param>
/// <param name="Teams">Teams of that lobby, oldest first.</param>
public sealed record EventTeamListingResult(
    int Mode,
    int LobbyIdentifier,
    IReadOnlyList<EventTeamEntry> Teams);
