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
/// Handles a player-profile record. The joiner sends its own profile once the
/// session is keyed and then re-sends it until the host answers with one of its
/// own, so this handler replies with the host's profile.
/// </summary>
/// <param name="hostIdentity">Identity this host presents to its peers.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class PlayerProfileHandler(
    HostIdentityService hostIdentity,
    ILogger<PlayerProfileHandler> logger) : IPeerCommandHandler
{
    /// <summary>
    /// Position this host takes in the room roster. Zero is the host's own slot:
    /// the joining client fills the slots after it, so the host is first.
    /// </summary>
    private const byte HostRosterIndex = 0;

    /// <summary>
    /// The per-player value the record carries at offset 8. Its meaning is
    /// unresolved, and every recorded value differs per player, so it is sent as
    /// zero and logged rather than guessed at.
    /// </summary>
    private const ushort UnresolvedPerPlayerValue = 0;

    /// <summary>Team flag; zero in every recorded record that carries no team.</summary>
    private const byte HostTeamFlag = 0;

    /// <inheritdoc />
    public async Task HandleAsync(PeerContext context)
    {
        var record = PlayerProfileRecordUtility.Parse(context.Message.Body);
        logger.LogInformation(
            "UDP {LocalPort}: profile from {RemoteAddress}: character {CharacterIdentifier} name {Name}",
            context.LocalPort,
            context.Remote,
            record?.CharacterIdentifier,
            record?.Name is { Length: > 0 } name ? name : "(none)");

        // The host answers with its own profile. Sending it unconditionally is
        // deliberate: the joiner re-sends its profile until it sees one, so a
        // reply to a repeat is what breaks the exchange, not a bug.
        var body = PlayerProfileRecordUtility.Build(
            characterIdentifier: hostIdentity.ProfileCharacterIdentifier,
            rosterIndex: HostRosterIndex,
            perPlayerValue: UnresolvedPerPlayerValue,
            teamFlag: HostTeamFlag,
            name: hostIdentity.AccountName,
            clanName: hostIdentity.ClanName);

        await context.Send(UdpCommandConstants.PlayerProfile, body);
    }
}
