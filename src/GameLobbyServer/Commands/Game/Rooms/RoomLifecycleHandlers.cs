using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// Joins a room, handing the joiner the host's peer-to-peer endpoint.
/// <para>
/// The request names the mode the join is for, and the room has to be running it;
/// the reference refuses a mismatch, and a mismatch is worth refusing because a
/// room entered under one mode leaves the client unable to state which lobby it is
/// in. A payload that stops before that byte makes no claim about a mode, and a
/// room created before the mode column existed cannot be asked for its own, so
/// neither is refused on this rule.
/// </para>
/// <para>
/// A reserved Survival host room is not a room to enter at all: it is leased to one
/// match and belongs to the two teams that match named. The room browser leaves
/// such a room out of the list, but the list is not the rule — a client that knows
/// the identifier could still ask to join it, and a room with a stranger in it
/// stops being an idle host for the next match. So the rule is applied here, where
/// the join is served.
/// </para>
/// </summary>/// <param name="gameService">Service that owns the rooms.</param>
    /// <param name="characterService">Service that owns the characters' host settings.</param>
    /// <param name="assignmentService">Service that knows which match a room is leased to.</param>
    /// <param name="sessionHelper">Helper used to write the replies.</param>/// <param name="logger">Logger of this handler.</param>
/// <param name="coordination">Stream the joined game is announced on.</param>
public sealed class JoinGameHandler(
    GameService gameService,
    CharacterService characterService,
    EventAssignmentService assignmentService,
    SessionHelper sessionHelper,
    LobbyCoordinationClientService coordination,
    ILogger<JoinGameHandler> logger) : ICommandHandler
{

    /// <summary>Size of the success reply, including the two unread trailing bytes.</summary>
    private const int SuccessSize = 43;

    /// <summary>Length of an address field.</summary>
    private const int IpLength = 16;

    /// <summary>Length of the password field.</summary>
    private const int PasswordLength = 16;

    /// <summary>Player cap of a room.</summary>
    private const int MaximumPlayers = 18;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        if (reader.Remaining < 4)
        {
            await SendResultAsync(session, ErrorCodeConstants.ResultGeneral, null, cancellationToken);
            return;
        }

        var gameIdentifier = (int)reader.ReadUInt32();
        var password = reader.Remaining >= PasswordLength
            ? StringUtility.ReadFixedString(reader.ReadBytes(PasswordLength), 0, PasswordLength)
            : string.Empty;

        var game = gameIdentifier > 0 ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken) : null;
        if (game is null)
        {
            await SendResultAsync(session, ErrorCodeConstants.ResultGeneral, null, cancellationToken);
            return;
        }

        var requestedSubtype = reader.Remaining >= 1
            ? reader.ReadUInt8()
            : EventHostEligibilityUtils.UnnamedSubtype;

        // Whether a room is holding a host role is its host's own claim, read
        // from the settings it saved. It is read only for a room named for a
        // role: an ordinary room's answer is its own mode either way, so the
        // query would be spent on every join to learn nothing.
        var hostSettings = EventHostEligibilityUtils.IsReservedHostName(game.Name)
            ? (await characterService.FindHostSettingsAsync([game.HostIdentifier], cancellationToken))
                .GetValueOrDefault(game.HostIdentifier)
            : null;

        if (!EventHostEligibilityUtils.AcceptsJoinMode(game, hostSettings, requestedSubtype))
        {
            // Logged with both modes because this is the rule a live client can
            // contradict: the refusal names the mismatch rather than leaving it to
            // be guessed at from the code.
            logger.LogWarning(
                "Refused a join of room {GameIdentifier}: the room runs mode {GameSubtype} and the request named {RequestedSubtype}",
                game.Identifier,
                game.LobbySubtype,
                requestedSubtype);
            await SendResultAsync(session, ErrorCodeConstants.ResultJoinGameRefused, null, cancellationToken);
            return;
        }

        if (EventHostEligibilityUtils.IsSurvivalHost(game, hostSettings))
        {
            var assignment = await assignmentService.FindByGameAsync(game.Identifier, cancellationToken);
            var participant = session.CharacterIdentifier ?? 0;
            var isAssigned = assignment is not null
                && (assignment.FirstTeam.IndexOfParticipant(participant) >= 0
                    || assignment.SecondTeam.IndexOfParticipant(participant) >= 0);

            if (!isAssigned)
            {
                await SendResultAsync(session, ErrorCodeConstants.ResultJoinGameRefused, null, cancellationToken);
                return;
            }
        }

        if (game.Password.Length > 0 && game.Password != password)
        {
            await SendResultAsync(session, ErrorCodeConstants.ResultGamePasswordIncorrect, null, cancellationToken);
            return;
        }

        var capacity = game.MaximumPlayers > 0
            ? Math.Min(game.MaximumPlayers, MaximumPlayers)
            : MaximumPlayers;
        var occupants = await gameService.CountPlayersAsync(game.Identifier, cancellationToken);
        if (occupants >= capacity)
        {
            await SendResultAsync(session, ErrorCodeConstants.ResultGameFull, null, cancellationToken);
            return;
        }

        // Without the host's registered peer-to-peer endpoint a peer
        // connection is impossible, so this is a failure, not an empty success.
        var endpoint = await gameService.GetConnectionInformationAsync(game.HostIdentifier, cancellationToken);
        if (endpoint is null)
        {
            await SendResultAsync(session, ErrorCodeConstants.ResultGeneral, null, cancellationToken);
            return;
        }

        await gameService.AddPlayerAsync(game.Identifier, session.CharacterIdentifier ?? 0, cancellationToken);
        session.GameIdentifier = game.Identifier;

        // Announced up the coordination stream, where the HTTP API turns it into
        // the channel message: a lobby knows nothing of Discord itself.
        coordination.GameJoined(session.CharacterIdentifier ?? 0, game.Name);

        // The rating gate that actually decides: the client keeps no memory
        // across joins, so only the server can stop a repeat vote.
        var canRate = session.CharacterIdentifier != game.HostIdentifier &&
            !await gameService.HasRatedHostOfAsync(game.Identifier, session.CharacterIdentifier ?? 0, cancellationToken);

        await SendResultAsync(session, ErrorCodeConstants.ResultNone, endpoint, cancellationToken, canRate, game.CurrentGame);
    }

    private Task SendResultAsync(
        TcpSession session,
        uint result,
        ConnectionInformation? endpoint,
        CancellationToken cancellationToken,
        bool canRateHost = false,
        int currentGame = 0)
    {
        if (result != ErrorCodeConstants.ResultNone || endpoint is null)
        {
            var refusal = new PacketWriter();
            refusal.WriteUInt32(result);
            return sessionHelper.SendPacketAsync(session, CommandConstants.JoinGameResult, refusal.Build(), cancellationToken);
        }

        var writer = new PacketWriter();
        writer.WriteUInt32(result);
        writer.WriteFixedString(endpoint.PublicIpAddress, IpLength);
        writer.WriteUInt16(endpoint.PublicPort);
        writer.WriteFixedString(endpoint.PrivateIpAddress, IpLength);
        writer.WriteUInt16(endpoint.PrivatePort);
        writer.WriteUInt8(canRateHost ? 1 : 0);
        writer.WriteUInt8(currentGame);
        writer.WriteUInt8(0);
        writer.WritePadding(Math.Max(0, SuccessSize - writer.Size));
        return sessionHelper.SendPacketAsync(session, CommandConstants.JoinGameResult, writer.Build(), cancellationToken);
    }
}

/// <summary>Removes a joiner whose peer connection never formed.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class JoinGameFailedHandler(
    GameService gameService,
    SessionHelper sessionHelper,
    ILogger<JoinGameFailedHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            var game = await gameService.GameContainingAsync(characterIdentifier, cancellationToken);
            if (game is not null)
            {
                await gameService.RemovePlayerAsync(game.Identifier, characterIdentifier, cancellationToken);
                logger.LogInformation(
                    "Character {CharacterIdentifier} failed to join room {GameIdentifier} (peer connection never formed); removed from roster",
                    characterIdentifier,
                    game.Identifier);
            }
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.JoinGameFailedResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Leaves the current room, deleting it when the host leaves.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class QuitGameHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.GameIdentifier is { } gameIdentifier)
        {
            var game = await gameService.FindByIdAsync(gameIdentifier, cancellationToken);
            if (game is not null && game.HostIdentifier == session.CharacterIdentifier)
            {
                // Roster and round-snapshot rows cascade with the room.
                await gameService.DeleteAsync(gameIdentifier, cancellationToken);
            }
            else if (game is not null)
            {
                await gameService.RemovePlayerAsync(gameIdentifier, session.CharacterIdentifier ?? 0, cancellationToken);
            }

            session.GameIdentifier = null;
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.QuitGameResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Records a host-rating vote from the end-of-game star picker.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class RateHostHandler(
    GameService gameService,
    SessionHelper sessionHelper,
    ILogger<RateHostHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var voterIdentifier = session.CharacterIdentifier;

        if (voterIdentifier is not null && packet.Payload.Length >= 4)
        {
            var reader = new PacketReader(packet.Payload);
            var rating = (int)reader.ReadUInt32();

            if (rating is < GameService.MinimumHostRating or > GameService.MaximumHostRating)
            {
                // Out-of-range values are dropped rather than clamped: the
                // client will not send them, so one arriving means the reading
                // of the command is wrong and should be visible.
                logger.LogWarning(
                    "Host rating {Rating} is outside {Minimum}..{Maximum} — dropped",
                    rating,
                    GameService.MinimumHostRating,
                    GameService.MaximumHostRating);
            }
            else
            {
                var game = await gameService.GameContainingAsync(voterIdentifier.Value, cancellationToken);
                if (game is null)
                {
                    logger.LogInformation(
                        "Host rating {Rating} from character {VoterIdentifier}, who is in no room; dropped",
                        rating,
                        voterIdentifier);
                }
                else
                {
                    var recorded = await gameService.RecordHostVoteAsync(
                        game.Identifier,
                        game.HostIdentifier,
                        voterIdentifier.Value,
                        rating,
                        cancellationToken);

                    logger.LogInformation(
                        recorded
                            ? "Room {GameIdentifier}: character {VoterIdentifier} rated host {HostIdentifier} at {Rating} stars"
                            : "Room {GameIdentifier}: a vote by character {VoterIdentifier} was already recorded and is discarded",
                        game.Identifier,
                        voterIdentifier,
                        game.HostIdentifier,
                        rating);
                }
            }
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.RateHostResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}
