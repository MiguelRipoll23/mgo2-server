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
        Register(serverType, commandId, typeof(THandler));

    /// <summary>
    /// Registers the handler type of a command.
    /// <para>
    /// A command has exactly one handler, so registering a second one is refused
    /// rather than resolved: overwriting silently leaves whichever handler was
    /// declared last in charge, and the loser is dead code that still reads as
    /// maintained. Both names are reported so the pair is visible in the exception.
    /// </para>
    /// </summary>
    /// <param name="serverType">Role of the server the command belongs to.</param>
    /// <param name="commandId">Command identifier.</param>
    /// <param name="handlerType">Handler type to register.</param>
    /// <exception cref="InvalidOperationException">Thrown when another handler already serves the command.</exception>
    public void Register(ServerType serverType, ushort commandId, Type handlerType)
    {
        if (handlers.TryGetValue((serverType, commandId), out var existing) && existing != handlerType)
        {
            throw new InvalidOperationException(
                $"Command 0x{commandId:x4} on {serverType} is registered twice: " +
                $"{existing.Name} and {handlerType.Name}. One of the two is unreachable.");
        }

        handlers[(serverType, commandId)] = handlerType;
    }

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
