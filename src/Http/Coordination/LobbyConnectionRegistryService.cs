using System.Collections.Concurrent;
using Mgo2Server.Shared.InternalGrpc.Contracts;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Http.Coordination;

/// <summary>
/// The gameplay lobbies whose coordination stream is currently open. It is what
/// makes the HTTP API the central point of cross-lobby communication: a server
/// that wants to reach every lobby hands the message to this service instead of
/// knowing anything about the lobbies themselves.
/// </summary>
/// <param name="logger">Logger of this service.</param>
public sealed class LobbyConnectionRegistryService(ILogger<LobbyConnectionRegistryService> logger)
{
    private readonly ConcurrentDictionary<int, LobbyConnection> connections = [];

    /// <summary>Number of lobbies whose stream is open.</summary>
    public int Count => connections.Count;

    /// <summary>
    /// Records a stream as the one of its lobby. A lobby that reconnects
    /// replaces the connection it had, so an announcement is never written to a
    /// stream the lobby abandoned.
    /// </summary>
    /// <param name="lobbyIdentifier">Identifier the lobby registered with.</param>
    /// <param name="lobbyName">Name the lobby registered with.</param>
    /// <returns>The connection the stream owns; it must be handed back on close.</returns>
    public LobbyConnection Open(int lobbyIdentifier, string lobbyName)
    {
        var connection = new LobbyConnection(lobbyIdentifier, lobbyName);

        if (connections.TryGetValue(lobbyIdentifier, out var previous))
        {
            logger.LogInformation(
                "Lobby {LobbyIdentifier} ({LobbyName}) reconnected; replacing the stream it had",
                lobbyIdentifier,
                lobbyName);
            previous.Complete();
        }

        connections[lobbyIdentifier] = connection;
        return connection;
    }

    /// <summary>
    /// Forgets the stream of a lobby, but only when it is still the stream the
    /// registry holds: the cleanup of a replaced connection must not unregister
    /// the connection that replaced it.
    /// </summary>
    /// <param name="connection">Connection the stream owns.</param>
    /// <returns>Whether the stream was the current one of its lobby.</returns>
    public bool Close(LobbyConnection connection)
    {
        connection.Complete();

        if (!connections.TryGetValue(connection.LobbyIdentifier, out var current) ||
            !ReferenceEquals(current, connection))
        {
            return false;
        }

        return connections.TryRemove(
            new KeyValuePair<int, LobbyConnection>(connection.LobbyIdentifier, connection));
    }

    /// <summary>
    /// Queues a message for every connected lobby and reports how many of them
    /// it reached.
    /// </summary>
    /// <param name="message">Message to write to every stream.</param>
    public int Broadcast(HttpEvent message)
    {
        var recipients = 0;

        foreach (var connection in connections.Values)
        {
            if (connection.TryEnqueue(message))
            {
                recipients++;
            }
        }

        return recipients;
    }

    /// <summary>
    /// Queues a message for one lobby and reports whether its stream took it.
    /// <para>
    /// A message that is about one lobby's own contents is not broadcast: a
    /// flash news belongs to every client, but a set of players belongs to the
    /// one lobby that will show them, and sending it to the others would have
    /// each of them answer for a lobby it is not.
    /// </para>
    /// </summary>
    /// <param name="lobbyIdentifier">Lobby the message is for.</param>
    /// <param name="message">Message to write to that stream.</param>
    /// <returns>Whether a connected lobby took the message.</returns>
    public bool SendTo(int lobbyIdentifier, HttpEvent message) =>
        connections.TryGetValue(lobbyIdentifier, out var connection)
        && connection.TryEnqueue(message);
}
