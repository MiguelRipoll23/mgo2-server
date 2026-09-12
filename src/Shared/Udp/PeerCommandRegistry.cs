using Mgo2Server.Shared.Interfaces;

namespace Mgo2Server.Shared.Udp;

/// <summary>
/// Maps a peer-to-peer message type to the handler that serves it. Handlers are
/// resolved from the container, so each one receives the services of its own
/// domain through its constructor.
/// </summary>
public sealed class PeerCommandRegistry
{
    private readonly Dictionary<ushort, Type> handlers = [];

    /// <summary>Registers the handler type of a message.</summary>
    /// <param name="messageType">Message type the handler serves.</param>
    /// <typeparam name="THandler">Handler type to register.</typeparam>
    public void Register<THandler>(ushort messageType)
        where THandler : class, IPeerCommandHandler =>
        handlers[messageType] = typeof(THandler);

    /// <summary>Returns whether a message has a handler.</summary>
    /// <param name="messageType">Message type to look for.</param>
    public bool Has(ushort messageType) => handlers.ContainsKey(messageType);

    /// <summary>Returns the registered handler type of a message.</summary>
    /// <param name="messageType">Message type to look for.</param>
    public Type? ResolveHandlerType(ushort messageType) =>
        handlers.GetValueOrDefault(messageType);
}
