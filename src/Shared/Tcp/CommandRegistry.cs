using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;

namespace Mgo2Server.Shared.Tcp;

/// <summary>
/// Maps a server role and command identifier to the handler that serves it.
/// Handlers are resolved from the container, so a handler receives the services
/// of its own domain through its constructor.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<(ServerType ServerType, ushort CommandId), Type> handlers = [];

    /// <summary>Registers the handler type of a command.</summary>
    /// <param name="serverType">Role of the server the command belongs to.</param>
    /// <param name="commandId">Command identifier.</param>
    /// <typeparam name="THandler">Handler type to register.</typeparam>
    public void Register<THandler>(ServerType serverType, ushort commandId)
        where THandler : class, ICommandHandler =>
        handlers[(serverType, commandId)] = typeof(THandler);

    /// <summary>Registers the handler type of a command.</summary>
    /// <param name="serverType">Role of the server the command belongs to.</param>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="handlerType">Handler type to register.</param>
    public void Register(ServerType serverType, ushort commandId, Type handlerType) =>
        handlers[(serverType, commandId)] = handlerType;

    /// <summary>Returns whether a command has a handler.</summary>
    /// <param name="serverType">Role of the server the command belongs to.</param>
    /// <param name="commandId">Command identifier.</param>
    public bool Has(ServerType serverType, ushort commandId) =>
        handlers.ContainsKey((serverType, commandId));

    /// <summary>Returns the registered handler type of a command.</summary>
    /// <param name="serverType">Role of the server the command belongs to.</param>
    /// <param name="commandId">Command identifier.</param>
    public Type? ResolveHandlerType(ServerType serverType, ushort commandId) =>
        handlers.GetValueOrDefault((serverType, commandId));
}
