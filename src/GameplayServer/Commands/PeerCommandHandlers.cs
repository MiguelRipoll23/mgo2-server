using Mgo2Server.GameplayServer.Identity;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameplayServer.Commands;

/// <summary>
/// Accepts a joiner's handshake, then sends the keep-alive that establishes the
/// session key.
/// </summary>
/// <param name="hostIdentity">Identity this host presents to its peers.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class AcceptHandshakeHandler(
    HostIdentityService hostIdentity,
    ILogger<AcceptHandshakeHandler> logger) : IPeerCommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var handshake = FrameBuilderUtility.ParseHandshakeBody(context.Message.Body);
        if (handshake is null)
        {
            logger.LogWarning(
                "UDP {LocalPort}: a handshake from {RemoteAddress} failed validation",
                context.LocalPort,
                context.Remote);
            return;
        }

        // The provisioning pass ran before the body was parsed, so the identity
        // it carried is written back here together with the session key.
        context.Session.PeerIdentifier = handshake.PeerIdentifier;
        context.Session.CounterBase = handshake.CounterBase;
        context.Session.SessionKey = handshake.CounterBase ^ hostIdentity.CounterBase;
        context.Session.DialBack = context.Remote;

        logger.LogInformation(
            "UDP {LocalPort}: handshake from {RemoteAddress} peer=0x{PeerIdentifier:x8} base=0x{CounterBase:x8}",
            context.LocalPort,
            context.Remote,
            handshake.PeerIdentifier,
            handshake.CounterBase);

        // 1. The handshake reply, still pre-keyed because the joiner has not
        //    reached its keyed state yet.
        var reply = FrameBuilderUtility.BuildHandshakeBody(
            hostIdentity.PeerIdentifier,
            hostIdentity.CounterBase,
            context.Remote.Address.ToString(),
            context.LocalPort);
        await context.Send(UdpCommandConstants.Handshake, reply);

        // 2. The key-establishing keep-alive.
        context.Session.Established = true;
        await context.Send(UdpCommandConstants.KeepAlive, []);
        logger.LogInformation(
            "UDP {LocalPort}: session with peer=0x{PeerIdentifier:x8} established",
            context.LocalPort,
            handshake.PeerIdentifier);
    }
}

/// <summary>Mirrors a peer's keep-alive straight back.</summary>
public sealed class AcknowledgeKeepAliveHandler : IPeerCommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(PeerContext context) =>
        context.Send(UdpCommandConstants.KeepAlive, context.Message.Body);
}

/// <summary>
/// Records the joiner's player-profile record. The frame carrying it is
/// acknowledged by the transport, so no reply is sent.
/// </summary>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(ILogger<PlayerProfileHandler> logger) : IPeerCommandHandler
{
    /// <summary>Offset of the account name within the record body.</summary>
    private const int NameOffset = 0x51 - 4;

    /// <inheritdoc />
    public Task HandleAsync(PeerContext context)
    {
        var body = context.Message.Body;
        if (body.Length < 2)
        {
            return Task.CompletedTask;
        }

        var characterIdentifier = BinaryUtility.ReadUInt16LittleEndian(body, 0);
        var name = ReadNullTerminatedString(body, NameOffset);

        logger.LogInformation(
            "UDP {LocalPort}: character {CharacterIdentifier}{Name} attempting to join",
            context.LocalPort,
            characterIdentifier,
            name is null ? string.Empty : $" ({name})");

        return Task.CompletedTask;
    }

    private static string? ReadNullTerminatedString(byte[] buffer, int offset)
    {
        var end = offset;
        while (end < buffer.Length && buffer[end] != 0x00)
        {
            end++;
        }

        if (end == offset || end >= buffer.Length)
        {
            return null;
        }

        var characters = new char[end - offset];
        var printable = true;
        for (var index = offset; index < end; index++)
        {
            var value = buffer[index];
            if (value is < 0x20 or > 0x7e)
            {
                printable = false;
            }

            characters[index - offset] = (char)value;
        }

        return printable ? new string(characters) : null;
    }
}
