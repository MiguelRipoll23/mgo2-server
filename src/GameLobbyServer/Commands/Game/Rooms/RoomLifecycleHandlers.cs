using System.Text.Json;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>Creates a room from the settings the client pushed moments before.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="characterService">Service that owns the stored settings.</param>
/// <param name="automatchService">Queue told about the new room.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CreateGameHandler(
    GameService gameService,
    CharacterService characterService,
    AutomatchService automatchService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier ||
            session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.CreateGameResult,
                ErrorCodeConstants.ResultInvalidSession,
                cancellationToken);
            return;
        }

        var settings = await characterService.GetHostSettingsAsync(characterIdentifier, cancellationToken);
        var pushed = settings.FirstOrDefault(row => row.Type == HostSettingsType.Value);
        var block = pushed is not null ? HostSettingsBlobCodec.Decode(pushed.Settings) : null;
        var parsed = block is not null ? HostSettingsBlobCodec.Parse(block) : null;

        var name = parsed?.Name ?? string.Empty;
        var comment = parsed?.Comment ?? string.Empty;
        var password = parsed is { PasswordEnabled: true } ? parsed.Password : string.Empty;
        var maximumPlayers = parsed is { MaximumPlayers: > 0 } ? parsed.MaximumPlayers : 8;
        var rotation = parsed?.Rotation ?? [];

        var game = await gameService.CreateAsync(room =>
        {
            room.HostIdentifier = characterIdentifier;
            room.LobbyIdentifier = lobbyIdentifier;
            room.Name = name.Length > 0 ? name : $"Game_{characterIdentifier}";
            room.Password = password;
            room.Comment = comment;
            room.MaximumPlayers = maximumPlayers;
            room.Games = JsonSerializer.Serialize(rotation);
        }, cancellationToken);

        // The host is the room's first roster member: the roster row carries
        // its ping, team slot and round attribution.
        await gameService.AddPlayerAsync(game.Identifier, characterIdentifier, cancellationToken);
        session.GameIdentifier = game.Identifier;

        // Told to the queue so a pending match releases without waiting for
        // the next tick to notice the new row.
        automatchService.GameCreated(characterIdentifier, game.Identifier);

        // The reply is a result word followed by the room identifier: the
        // client reads the identifier before testing the result.
        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32((uint)game.Identifier);
        await sessionHelper.SendPacketAsync(session, CommandConstants.CreateGameResult, writer.Build(), cancellationToken);
    }
}

/// <summary>Joins a room, handing the joiner the host's peer-to-peer endpoint.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class JoinGameHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
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

/// <summary>Edits the details of the caller's own room in place.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostInGameInfoHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    // The stance offset differs from the settings blob's: here it is the word
    // the settings blob uses for the dedicated flag.
    private const int NameOffset = 0x00;
    private const int CommentOffset = 0x10;
    private const int PasswordFlagOffset = 0x90;
    private const int PasswordOffset = 0x91;
    private const int StanceOffset = 0xa1;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        if (game is not null && session.CharacterIdentifier is { } characterIdentifier && game.HostIdentifier == characterIdentifier)
        {
            var bytes = packet.Payload;
            var name = StringUtility.ReadFixedString(bytes, NameOffset, 16);
            var comment = ReadField(bytes, CommentOffset, 128);
            var passwordEnabled = bytes.Length > PasswordFlagOffset && bytes[PasswordFlagOffset] != 0;
            var password = passwordEnabled ? ReadField(bytes, PasswordOffset, 16) : string.Empty;
            var stance = bytes.Length > StanceOffset ? bytes[StanceOffset] : 0;

            await gameService.UpdateAsync(game.Identifier, room =>
            {
                room.Name = name.Length > 0 ? name : room.Name;
                room.Comment = comment;
                room.Password = password;
                room.Stance = stance;
            }, cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.HostInGameInfoResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }

    private static string ReadField(byte[] bytes, int offset, int maximum) =>
        offset >= bytes.Length ? string.Empty : StringUtility.ReadFixedString(bytes, offset, Math.Min(maximum, bytes.Length - offset));
}
