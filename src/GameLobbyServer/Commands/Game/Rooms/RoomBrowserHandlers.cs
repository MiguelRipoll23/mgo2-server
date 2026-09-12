using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Logging;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>Lists the rooms of the lobby the session is in.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetGameListHandler(
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Size of one room-list entry.</summary>
    private const int GameElementSize = 55;

    /// <summary>Player cap the browser's entry can carry.</summary>
    private const int MaximumPlayers = 18;

    /// <summary>Bit marking a password-locked room.</summary>
    private const int HostPasswordBit = 0b1;

    /// <summary>Always-set bit of the first common byte.</summary>
    private const int CommonAlwaysBit = 0b100;

    /// <summary>Auto-assign bit of the second common byte.</summary>
    private const int CommonAutoAssignBit = 0b10;

    /// <summary>Voice-chat bit of the second common byte.</summary>
    private const int CommonVoiceChatBit = 0b1000000;

    /// <summary>Trailing byte of a room-list entry, written verbatim.</summary>
    private const int TrailingByte = 0x63;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var lobbyIdentifier = session.LobbyIdentifier ?? 0;
        var games = lobbyIdentifier > 0
            ? await gameService.FindByLobbyAsync(lobbyIdentifier, cancellationToken)
            : [];

        var hostIdentifiers = games.Select(game => game.HostIdentifier).Distinct().ToList();
        var ratings = await gameService.GetHostRatingSummariesAsync(hostIdentifiers, cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetGameListStart, cancellationToken);

        foreach (var game in games)
        {
            var writer = new PacketWriter();
            var hostOptions = game.Password.Length > 0 ? HostPasswordBit : 0;
            var playerCount = Math.Min(
                await gameService.CountPlayersAsync(game.Identifier, cancellationToken),
                Math.Min(game.MaximumPlayers, MaximumPlayers));
            var rating = ratings.TryGetValue(game.HostIdentifier, out var summary) ? summary : default;

            writer.WriteUInt32((uint)game.Identifier);
            writer.WriteFixedString(game.Name, 16);
            writer.WriteUInt8(hostOptions);
            writer.WriteUInt8(0x8);
            writer.WriteUInt8(0);
            writer.WriteUInt8(0);
            writer.WriteUInt8(0);
            writer.WriteUInt8(Math.Min(game.MaximumPlayers, MaximumPlayers));
            writer.WriteUInt8(game.Stance);
            writer.WriteUInt8(CommonAlwaysBit);
            writer.WriteUInt8(CommonAutoAssignBit | CommonVoiceChatBit);
            writer.WriteUInt8(playerCount);
            writer.WriteUInt32((uint)game.Ping);
            writer.WriteUInt8(0);
            writer.WriteUInt8(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32(0);
            writer.WriteUInt32((uint)rating.RatingSum);
            writer.WriteUInt32((uint)rating.Votes);
            writer.WriteUInt16(0);
            writer.WriteUInt8(TrailingByte);
            writer.WritePadding(Math.Max(0, GameElementSize - writer.Size));

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetGameListPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetGameListEnd, cancellationToken);
    }
}

/// <summary>Serves the host settings the client last pushed.</summary>
/// <param name="characterService">Service that owns the stored settings.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetHostSettingsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        byte[]? blob = null;
        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            var settings = await characterService.GetHostSettingsAsync(characterIdentifier, cancellationToken);
            var saved = settings.FirstOrDefault(row => row.Type == HostSettingsType.Value);
            if (saved is not null)
            {
                blob = HostSettingsBlobCodec.Decode(saved.Settings);
            }
        }

        var payload = blob is not null && blob.Length >= HostSettingsBlobCodec.MinimumBlobLength
            ? HostSettingsBlobCodec.BuildReply(blob)
            : new byte[HostSettingsBlobCodec.EmptyReplySize];

        await sessionHelper.SendPacketAsync(session, CommandConstants.GetHostSettingsResult, payload, cancellationToken);
    }
}

/// <summary>Stores the host settings block the client pushes.</summary>
/// <param name="characterService">Service that owns the stored settings.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CheckHostSettingsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length > 0)
        {
            // The blob is the whole payload; the room name starts at offset zero.
            await characterService.UpdateHostSettingsAsync(
                characterIdentifier,
                HostSettingsType.Value,
                HostSettingsBlobCodec.Encode(packet.Payload),
                cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.CheckHostSettingsResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Serves everything the details, join and player-list screens show.</summary>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="logger">Logger of this handler.</param>
public sealed class GetGameDetailsHandler(
    GameService gameService,
    CharacterService characterService,
    SessionHelper sessionHelper,
    ILogger<GetGameDetailsHandler> logger) : ICommandHandler
{
    /// <summary>Number of bytes the parser reads unconditionally.</summary>
    private const int FixedSize = 372;

    /// <summary>Size of one player entry.</summary>
    private const int PlayerEntrySize = 28;

    /// <summary>Maximum number of player entries.</summary>
    private const int MaximumPlayers = 18;

    /// <summary>Number of rotation entries.</summary>
    private const int RotationRounds = 16;

    /// <summary>Number of per-rule timer words.</summary>
    private const int RuleTimers = 17;

    /// <summary>Always-set bit of the first common byte.</summary>
    private const int CommonAlwaysBit = 0b100;

    /// <summary>Auto-assign bit of the second common byte.</summary>
    private const int CommonAutoAssignBit = 0b10;

    /// <summary>Voice-chat bit of the second common byte.</summary>
    private const int CommonVoiceChatBit = 0b1000000;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var gameIdentifier = (int)reader.ReadUInt32();
        var game = gameIdentifier > 0 ? await gameService.FindByIdAsync(gameIdentifier, cancellationToken) : null;

        if (game is null)
        {
            var refusal = new PacketWriter();
            refusal.WriteUInt32(ErrorCodeConstants.ResultNone);
            refusal.WritePadding(FixedSize - 4);
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetGameDetailsResult, refusal.Build(), cancellationToken);
            return;
        }

        var roster = (await gameService.GetPlayersAsync(game.Identifier, game.HostIdentifier, cancellationToken))
            .Take(MaximumPlayers)
            .ToList();
        var pings = await gameService.GetPlayerPingsAsync(game.Identifier, cancellationToken);
        var ratings = await gameService.GetHostRatingSummariesAsync([game.HostIdentifier], cancellationToken);
        var rating = ratings.TryGetValue(game.HostIdentifier, out var summary) ? summary : default;

        var experiences = new Dictionary<int, int>();
        var totalExperience = 0;
        foreach (var characterIdentifier in roster)
        {
            var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
            var experience = character?.Experience ?? 0;
            experiences[characterIdentifier] = experience;
            totalExperience += experience;
        }

        var averageExperience = roster.Count > 0 ? (int)Math.Round((double)totalExperience / roster.Count) : 0;
        var viewerIsHost = session.CharacterIdentifier == game.HostIdentifier;
        var rotation = ParseRotation(game.Games);

        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32((uint)game.Identifier);
        writer.WriteFixedString(game.Name, 16);
        writer.WriteFixedString(game.Comment, 128);
        writer.WriteUInt8(game.Password.Length > 0 ? 1 : 0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(session.LobbyIdentifier is { } lobbyIdentifier ? Math.Min(lobbyIdentifier, 0xff) : 0);
        writer.WriteUInt32((uint)averageExperience);
        writer.WriteUInt32((uint)rating.RatingSum);
        writer.WriteUInt32((uint)rating.Votes);
        // The rating gate: nonzero lets the client offer the star picker.
        writer.WriteUInt8(viewerIsHost ? 0 : 1);

        for (var index = 0; index < RotationRounds; index++)
        {
            var round = index < rotation.Count ? rotation[index] : [0, 0, 0];
            writer.WriteUInt8(round[0]);
            writer.WriteUInt8(round[1]);
            writer.WriteUInt8(round[2]);
        }

        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WritePadding(16);
        writer.WriteUInt8(game.MaximumPlayers);
        writer.WriteUInt8(roster.Count);
        writer.WriteUInt32(0);
        writer.WritePadding(22);
        writer.WriteUInt8(game.Stance);
        writer.WriteUInt8(0);
        writer.WriteUInt32(0);

        for (var index = 0; index < RuleTimers; index++)
        {
            writer.WriteUInt32(0);
        }

        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WritePadding(7);
        writer.WriteUInt8(CommonAlwaysBit);
        writer.WriteUInt8(CommonAutoAssignBit | CommonVoiceChatBit);
        writer.WriteUInt8(0);
        writer.WriteUInt16(0);
        writer.WriteUInt16(0);
        writer.WriteUInt32((uint)game.Ping);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WritePadding(8);
        writer.WriteUInt8(0);
        writer.WriteUInt8(0);
        writer.WritePadding(4);

        if (writer.Size != FixedSize)
        {
            logger.LogError(
                "Room {GameIdentifier}: the details reply fixed part is {Actual} bytes, expected {Expected}",
                game.Identifier,
                writer.Size,
                FixedSize);
        }

        foreach (var characterIdentifier in roster)
        {
            var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
            writer.WriteUInt32((uint)characterIdentifier);
            writer.WriteFixedString(character?.Name ?? string.Empty, 16);
            writer.WriteUInt32((uint)pings.GetValueOrDefault(characterIdentifier));
            writer.WriteUInt32((uint)experiences.GetValueOrDefault(characterIdentifier));
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.GetGameDetailsResult, writer.Build(), cancellationToken);
    }

    private static List<int[]> ParseRotation(string games)
    {
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<int[]>>(games);
            return parsed ?? [];
        }
        catch
        {
            return [];
        }
    }
}
