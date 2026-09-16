using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.Shared.Tcp;

/// <summary>
/// Ends the connection a client asked to close.
/// <para>
/// Registered like any other command so the registry stays the inventory of what
/// this server answers: the two session commands used to be special-cased in
/// <see cref="TcpServerBase"/>, which answered both without them appearing
/// anywhere a reader could count them.
/// </para>
/// </summary>
/// <param name="logger">Logger of this handler.</param>
public sealed class DisconnectHandler(ILogger<DisconnectHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        logger.LogDebug("[{LogPrefix}] 0x0003 disconnect requested", session.LogPrefix);
        session.RequestDisconnect();
        return Task.CompletedTask;
    }
}

/// <summary>Answers a keep-alive with a keep-alive carrying no payload.</summary>
/// <param name="sessionHelper">Helper used to write the reply.</param>
public sealed class KeepAliveHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendPacketAsync(session, CommandConstants.KeepAlive, null, cancellationToken);
}
