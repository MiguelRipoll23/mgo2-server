using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Authentication;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game;

/// <summary>
/// Validates the session field a client presents when it enters a gameplay
/// lobby and parks the character in the lobby it connected to.
/// </summary>
/// <param name="sessionService">Service that owns the login sessions.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="lobbyTrackerService">Service that tracks the lobby population.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GameCheckSessionHandler(
    SessionService sessionService,
    CharacterService characterService,
    LobbyTrackerService lobbyTrackerService,
    SessionHelper sessionHelper,
    ILogger<GameCheckSessionHandler> logger) : ICommandHandler
{
    /// <summary>Length of the session field the client derives from its login token.</summary>
    private const int SessionFieldLength = 16;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length < 4 + SessionFieldLength)
        {
            logger.LogInformation("In-lobby session check: payload too short ({Length} bytes)", packet.Payload.Length);
            await sessionHelper.SendResultAsync(session, CommandConstants.GameCheckSessionResult, ErrorCodeConstants.ResultLobbyLoginAgain, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var claimedCharacterIdentifier = (int)reader.ReadUInt32();
        var sessionField = CryptoUtility.StoredSessionFieldFromWire(reader.ReadBytes(SessionFieldLength));

        var storedSession = await sessionService.FindByTokenAsync(sessionField, cancellationToken);
        if (storedSession is null)
        {
            logger.LogInformation("In-lobby session check: no account holds the presented session");
            await sessionHelper.SendResultAsync(session, CommandConstants.GameCheckSessionResult, ErrorCodeConstants.ResultLobbyLoginAgain, cancellationToken);
            return;
        }

        session.UserIdentifier = storedSession.UserIdentifier;

        // The client chooses which of its characters enters, so the check is
        // ownership rather than equality with anything the server last saw.
        var character = await characterService.FindByIdAsync(claimedCharacterIdentifier, cancellationToken);
        if (character is null || character.UserIdentifier != storedSession.UserIdentifier)
        {
            logger.LogInformation(
                "In-lobby session check: account {UserIdentifier} claimed character {CharacterIdentifier}, which it does not own",
                storedSession.UserIdentifier,
                claimedCharacterIdentifier);
            await sessionHelper.SendResultAsync(session, CommandConstants.GameCheckSessionResult, ErrorCodeConstants.ResultLobbyLoginAgain, cancellationToken);
            return;
        }

        session.CharacterIdentifier = claimedCharacterIdentifier;

        // The lobby is the server the client connected to, stamped on the
        // session when the connection was accepted.
        if (session.LobbyIdentifier is { } lobbyIdentifier)
        {
            await characterService.SetLobbyAsync(claimedCharacterIdentifier, lobbyIdentifier, cancellationToken);
            lobbyTrackerService.JoinLobby(session, lobbyIdentifier);
            await lobbyTrackerService.SynchronizeAllLobbyCountsAsync(cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.GameCheckSessionResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>
/// Registers the client's peer-to-peer endpoint so a join reply can hand it
/// to every player entering the host's room.
/// </summary>
/// <param name="gameService">Service that owns the endpoints.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetPlayerDataHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Length of the private address field.</summary>
    private const int PrivateIpLength = 16;

    /// <summary>Shortest payload that carries an endpoint.</summary>
    private const int ConnectionInformationSize = 2 + PrivateIpLength + 2;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length < ConnectionInformationSize)
        {
            // Nothing readable to register, but the client blocks on this
            // reply, so it is acknowledged anyway.
            await sessionHelper.SendResultAsync(session, CommandConstants.GetPlayerDataResult, ErrorCodeConstants.ResultNone, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var privatePort = reader.ReadUInt16();
        var privateIpAddress = StringUtility.ReadFixedString(reader.ReadBytes(PrivateIpLength), 0, PrivateIpLength);
        var publicPort = reader.ReadUInt16();

        var characterIdentifier = session.CharacterIdentifier;
        var publicIpAddress = PublicIpAddressFrom(session.RemoteAddress);
        if (characterIdentifier is null || publicIpAddress is null)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.GetPlayerDataResult, ErrorCodeConstants.ResultNone, cancellationToken);
            return;
        }

        // The public address comes from the socket rather than the payload,
        // which the client cannot be trusted about.
        await gameService.SaveConnectionInformationAsync(
            characterIdentifier.Value,
            new ConnectionInformation(publicIpAddress, publicPort, privateIpAddress, privatePort),
            cancellationToken);

        await sessionHelper.SendResultAsync(session, CommandConstants.GetPlayerDataResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }

    /// <summary>Strips the port from a remote endpoint, handling both address families.</summary>
    /// <param name="remoteAddress">Endpoint formatted as "address:port".</param>
    private static string? PublicIpAddressFrom(string remoteAddress)
    {
        if (remoteAddress.StartsWith('['))
        {
            var end = remoteAddress.IndexOf(']');
            return end > 0 ? remoteAddress[1..end] : null;
        }

        var colon = remoteAddress.LastIndexOf(':');
        return colon > 0 ? remoteAddress[..colon] : remoteAddress;
    }
}
