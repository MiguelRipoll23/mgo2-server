using System.Collections.Concurrent;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Tcp;

/// <summary>
/// Tracks the sessions a gameplay lobby currently serves. The set is the
/// process's own truth about who is connected, which is what keeps a search or a
/// player count from outliving the socket behind it.
/// </summary>
public sealed class ActiveGameSessionsService
{
    private readonly ConcurrentDictionary<string, TcpSession> sessions = [];

    /// <summary>Records a session.</summary>
    /// <param name="session">Session to record.</param>
    public void Add(TcpSession session) => sessions[session.RemoteAddress] = session;

    /// <summary>Forgets a session.</summary>
    /// <param name="session">Session to forget.</param>
    public void Remove(TcpSession session) => sessions.TryRemove(session.RemoteAddress, out _);

    /// <summary>Lists the recorded sessions.</summary>
    public List<TcpSession> List() => [.. sessions.Values];
}
