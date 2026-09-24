using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Domain.Events;

/// <summary>
/// Maps characters and teams to the sessions this lobby is serving. It exists so
/// the event services look sessions up in one place rather than reaching into
/// the session registry themselves, which keeps the lookup rule — routing
/// context only, never authoritative membership — in a single spot.
/// </summary>
/// <param name="activeGameSessions">Sessions this lobby is serving.</param>
public sealed class EventSessionDirectoryService(ActiveGameSessionsService activeGameSessions)
{
    /// <summary>Returns the session of one character, when it is connected here.</summary>
    /// <param name="characterIdentifier">Character to look for.</param>
    public TcpSession? FindByCharacter(int characterIdentifier) =>
        activeGameSessions.List()
            .FirstOrDefault(session => session.CharacterIdentifier == characterIdentifier);

    /// <summary>Returns the sessions attached to one team, optionally excluding one.</summary>
    /// <param name="teamIdentifier">Team to look for.</param>
    /// <param name="excludedSession">Session to leave out, when there is one.</param>
    public List<TcpSession> TeamSessions(int teamIdentifier, TcpSession? excludedSession) =>
        [.. activeGameSessions.List().Where(session =>
            session.EventTeamIdentifier == teamIdentifier
            && !ReferenceEquals(session, excludedSession))];
}
