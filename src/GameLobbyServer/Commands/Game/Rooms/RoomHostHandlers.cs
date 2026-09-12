using System.Text.Json;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// Answers the host's peer-to-peer state machine. All three of its
/// register-with-server round-trips read a result word and a peer-table key,
/// so the key is echoed from the request and a short read would stall the
/// state machine until it disconnects the peer.
/// </summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPeerRegistrationHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Echoes the request's leading word back as the peer-table key.</summary>
    public static async Task ReplyAsync(
        TcpSession session,
        ushort replyCommand,
        Packet packet,
        SessionHelper sessionHelper,
        CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var key = reader.Remaining >= 4 ? reader.ReadUInt32() : 0;

        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32(key);
        await sessionHelper.SendPacketAsync(session, replyCommand, writer.Build(), cancellationToken);
    }

    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        ReplyAsync(session, CommandConstants.HostPlayerConnectedResult, packet, sessionHelper, cancellationToken);
}

/// <summary>Answers the host's disconnect notification.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPlayerDisconnectedHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        HostPeerRegistrationHandler.ReplyAsync(session, CommandConstants.HostPlayerDisconnectedResult, packet, sessionHelper, cancellationToken);
}

/// <summary>Answers the host's team registration.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostSetPlayerTeamHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        HostPeerRegistrationHandler.ReplyAsync(session, CommandConstants.HostSetPlayerTeamResult, packet, sessionHelper, cancellationToken);
}

/// <summary>Answers the host's finished-connect registration.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPlayerConnectFinishHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        HostPeerRegistrationHandler.ReplyAsync(session, CommandConstants.HostPlayerConnectFinishResult, packet, sessionHelper, cancellationToken);
}

/// <summary>Acknowledges a host hand-off.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class HostPassHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(session, CommandConstants.HostPassResult, ErrorCodeConstants.ResultNone, cancellationToken);
}

/// <summary>
/// Migrates the room to the successor the client elected when the host leaves.
/// </summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class PassRoundHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        if (game is not null &&
            session.CharacterIdentifier is { } characterIdentifier &&
            game.HostIdentifier == characterIdentifier &&
            packet.Payload.Length >= 8)
        {
            var reader = new PacketReader(packet.Payload);
            reader.Skip(4);
            var targetIdentifier = (int)reader.ReadUInt32();

            var roster = await gameService.GetPlayersAsync(game.Identifier, game.HostIdentifier, cancellationToken);
            var isPlayer = roster.Contains(targetIdentifier);

            if (isPlayer && targetIdentifier != game.HostIdentifier)
            {
                await gameService.UpdateAsync(game.Identifier, room => room.HostIdentifier = targetIdentifier, cancellationToken);
                await gameService.RemovePlayerAsync(game.Identifier, characterIdentifier, cancellationToken);
            }
            else
            {
                // The successor nominated by the client's fallback pass can
                // already be gone; dropping the command would leave the room
                // keyed to a host who just quit.
                await gameService.DeleteAsync(game.Identifier, cancellationToken);
                session.GameIdentifier = null;
            }
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.PassRoundResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Selects the rotation entry the room is staging.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SetGameHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Number of rotation entries.</summary>
    private const int RotationRounds = 16;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        if (game is not null && packet.Payload.Length >= 1)
        {
            var index = packet.Payload[0];
            var rotation = ParseRotation(game.Games);

            if (index < RotationRounds && index < rotation.Count)
            {
                await gameService.UpdateAsync(game.Identifier, room => room.CurrentGame = index, cancellationToken);
            }
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.SetGameResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }

    private static List<int[]> ParseRotation(string games)
    {
        try
        {
            return JsonSerializer.Deserialize<List<int[]>>(games) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

/// <summary>Stores the round-trip times the host reports for the room.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdatePingsHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var game = session.GameIdentifier is { } gameIdentifier
            ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken)
            : null;

        if (game is not null && packet.Payload.Length >= 4)
        {
            var reader = new PacketReader(packet.Payload);
            var hostPing = (int)reader.ReadUInt32();
            var playerPings = new Dictionary<int, int>();

            // There is no count field: the loop runs to the end of the stream.
            while (reader.Remaining >= 8)
            {
                var characterIdentifier = (int)reader.ReadUInt32();
                var ping = (int)reader.ReadUInt32();
                if (characterIdentifier != 0)
                {
                    playerPings[characterIdentifier] = ping;
                }
            }

            await gameService.UpdatePingsAsync(game.Identifier, hostPing, playerPings, cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.UpdatePingsResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Acknowledges a single client-side setting without storing it.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class PutClientSettingHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(session, CommandConstants.PutClientSettingResult, ErrorCodeConstants.ResultNone, cancellationToken);
}

/// <summary>
/// Acknowledges the host's per-skill experience report. Every character is
/// served the fixed maximum-level catalogue, so there is nothing to store.
/// </summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class HostSkillExperienceHandler(
    SessionHelper sessionHelper,
    ILogger<HostSkillExperienceHandler> logger) : ICommandHandler
{
    /// <summary>Cap the client's own serializer places on skill records.</summary>
    private const int MaximumSkillRecords = 127;

    /// <summary>Bytes one skill record occupies on the wire.</summary>
    private const int SkillRecordSize = 3;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var targetIdentifier = 0;
        var reported = 0;

        if (packet.Payload.Length >= 8)
        {
            var reader = new PacketReader(packet.Payload);
            targetIdentifier = (int)reader.ReadUInt32();
            var count = Math.Min((int)reader.ReadUInt32(), MaximumSkillRecords);

            if (reader.Remaining >= count * SkillRecordSize)
            {
                reported = count;
            }
        }

        logger.LogInformation(
            "Character {TargetIdentifier} reported {Reported} skill record(s); acknowledged, not persisted (all skills are served at their maximum level)",
            targetIdentifier,
            reported);

        await sessionHelper.SendResultAsync(session, CommandConstants.HostSkillExperienceResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Advances the round, snapshotting the roster that played it.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class StartRoundHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // Snapshot the roster: everyone in the room now played this round,
        // which the end-of-round attribution checks consult.
        if (session.GameIdentifier is { } gameIdentifier)
        {
            await gameService.MarkRoundPlayersAsync(gameIdentifier, cancellationToken);
        }

        // The reply is a result word plus a token that must be zero: the
        // client republishes a nonzero token to every peer, where it gates the
        // instructor-recognition prompt.
        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32(0);

        // Each client build parses exactly one of the two pairings.
        var replyCommand = packet.Header.Command == CommandConstants.StartRoundAlias
            ? CommandConstants.StartRoundAliasResult
            : CommandConstants.StartRoundResult;

        await sessionHelper.SendPacketAsync(session, replyCommand, writer.Build(), cancellationToken);
    }
}
