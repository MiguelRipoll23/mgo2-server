using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>Adds a character to the caller's friends or blocked list.</summary>
/// <param name="characterService">Service that owns the friends lists.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class AddFriendsBlockedHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length >= 5)
        {
            var reader = new PacketReader(packet.Payload);
            var type = reader.ReadUInt8();
            var targetIdentifier = (int)reader.ReadUInt32();

            await characterService.AddFriendOrBlockedAsync(characterIdentifier, targetIdentifier, type, cancellationToken);

            var target = await characterService.FindByIdAsync(targetIdentifier, cancellationToken);
            var writer = new PacketWriter();
            // The reply is a single entry: a leading word, the target, the
            // state byte and the name. There is no separate acknowledgement.
            writer.WriteUInt32(0);
            writer.WriteUInt32((uint)targetIdentifier);
            writer.WriteUInt8(type);
            writer.WriteFixedString(target?.Name ?? string.Empty, 16);
            await sessionHelper.SendPacketAsync(session, CommandConstants.AddFriendsBlockedResult, writer.Build(), cancellationToken);
            return;
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.AddFriendsBlockedResult, 1, cancellationToken);
    }
}

/// <summary>Removes a character from the caller's friends or blocked list.</summary>
/// <param name="characterService">Service that owns the friends lists.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class RemoveFriendsBlockedHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length >= 5)
        {
            var reader = new PacketReader(packet.Payload);
            var type = reader.ReadUInt8();
            var targetIdentifier = (int)reader.ReadUInt32();

            await characterService.RemoveFriendOrBlockedAsync(characterIdentifier, targetIdentifier, cancellationToken);

            var writer = new PacketWriter();
            // The entry shape differs from the add reply: the state byte
            // precedes the target and there is no name.
            writer.WriteUInt32(0);
            writer.WriteUInt8(type);
            writer.WriteUInt32((uint)targetIdentifier);
            await sessionHelper.SendPacketAsync(session, CommandConstants.RemoveFriendsBlockedResult, writer.Build(), cancellationToken);
            return;
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.RemoveFriendsBlockedResult, 1, cancellationToken);
    }
}

/// <summary>Lists the caller's friends and blocked entries, with presence.</summary>
/// <param name="characterService">Service that owns the friends lists.</param>
/// <param name="activeGameSessions">Connections currently in the lobby.</param>
/// <param name="lobbyService">Service that owns the lobby metadata.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetFriendsBlockedListHandler(
    CharacterService characterService,
    ActiveGameSessionsService activeGameSessions,
    LobbyService lobbyService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Maximum number of entries per page.</summary>
    private const int MaximumPerPacket = 15;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var characterIdentifier = session.CharacterIdentifier ?? 0;
        var entries = characterIdentifier > 0
            ? await characterService.GetFriendsAndBlockedWithNamesAsync(characterIdentifier, cancellationToken)
            : [];

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetFriendsBlockedListStart, cancellationToken);

        for (var offset = 0; offset < entries.Count; offset += MaximumPerPacket)
        {
            var page = entries.Skip(offset).Take(MaximumPerPacket).ToList();
            var writer = new PacketWriter();
            foreach (var entry in page)
            {
                await WriteEntryAsync(writer, entry, activeGameSessions, lobbyService, cancellationToken);
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetFriendsBlockedListPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetFriendsBlockedListEnd, cancellationToken);
    }

    private static async Task WriteEntryAsync(
        PacketWriter writer,
        CharacterFriendEntry entry,
        ActiveGameSessionsService activeGameSessions,
        LobbyService lobbyService,
        CancellationToken cancellationToken)
    {
        writer.WriteUInt8(entry.Type);
        writer.WriteUInt32((uint)entry.TargetIdentifier);
        writer.WriteFixedString(entry.TargetName, 16);

        var targetSession = activeGameSessions.List()
            .FirstOrDefault(candidate => candidate.CharacterIdentifier == entry.TargetIdentifier);
        var isOnline = targetSession is not null;
        writer.WriteUInt8(isOnline ? 1 : 0);
        writer.WritePadding(3);

        if (isOnline && targetSession!.LobbyIdentifier is { } lobbyIdentifier)
        {
            try
            {
                var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier, cancellationToken);
                writer.WriteUInt16(lobby.Identifier);
                writer.WriteFixedString(lobby.Name, 16);
            }
            catch
            {
                writer.WriteUInt16(0);
                writer.WriteFixedString(string.Empty, 16);
            }
        }
        else
        {
            writer.WriteUInt16(0);
            writer.WriteFixedString(string.Empty, 16);
        }
    }
}

/// <summary>Searches for a player by name and reports where they are.</summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="activeGameSessions">Connections currently in the lobby.</param>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SearchPlayerHandler(
    CharacterService characterService,
    ActiveGameSessionsService activeGameSessions,
    GameService gameService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Length of the search query field.</summary>
    private const int SearchQueryLength = 16;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var query = reader.ReadFixedString(SearchQueryLength);

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.SearchPlayerStart, cancellationToken);

        if (query.Length > 0)
        {
            var character = await characterService.FindByNameAsync(query, cancellationToken);
            if (character is not null)
            {
                var writer = new PacketWriter();
                writer.WriteUInt32((uint)character.Identifier);
                writer.WriteFixedString(character.Name, 16);

                var targetSession = activeGameSessions.List()
                    .FirstOrDefault(candidate => candidate.CharacterIdentifier == character.Identifier);
                var isOnline = targetSession is not null;
                writer.WriteUInt8(isOnline ? 1 : 0);

                var wroteGameInformation = false;
                if (isOnline && targetSession!.GameIdentifier is { } gameIdentifier && targetSession.LobbyIdentifier is { } lobbyIdentifier)
                {
                    var game = await gameService.FindByIdAsync(gameIdentifier, cancellationToken);
                    var lobby = await gameService.FindLobbyAsync(lobbyIdentifier, cancellationToken);
                    if (game is not null && lobby is not null)
                    {
                        var host = await characterService.FindByIdAsync(game.HostIdentifier, cancellationToken);
                        writer.WriteUInt16(lobby.Identifier);
                        writer.WriteFixedString(lobby.Name, 16);
                        writer.WriteUInt32((uint)game.Identifier);
                        writer.WriteFixedString(host?.Name ?? string.Empty, 16);
                        wroteGameInformation = true;
                    }
                }

                if (!wroteGameInformation)
                {
                    writer.WriteUInt16(0);
                    writer.WriteFixedString(string.Empty, 16);
                    writer.WriteUInt32(0);
                    writer.WriteFixedString(string.Empty, 16);
                }

                await sessionHelper.SendPacketAsync(session, CommandConstants.SearchPlayerPage, writer.Build(), cancellationToken);
            }
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.SearchPlayerEnd, cancellationToken);
    }
}

/// <summary>Serves the met-players history derived from the round reports.</summary>
/// <param name="roundReportService">Service that owns the round reports.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetMatchHistoryHandler(
    RoundReportService roundReportService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Maximum number of rows the history returns.</summary>
    private const int HistoryLimit = 64;

    /// <summary>Maximum number of rows per page.</summary>
    private const int EntriesPerPacket = 40;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var characterIdentifier = packet.Payload.Length >= 4 ? (int)reader.ReadUInt32() : 0;

        var metPlayers = characterIdentifier > 0
            ? await roundReportService.MetPlayersAsync(characterIdentifier, HistoryLimit, cancellationToken)
            : [];

        await sessionHelper.SendResultAsync(session, CommandConstants.GetMatchHistoryStart, ErrorCodeConstants.ResultNone, cancellationToken);

        for (var start = 0; start < metPlayers.Count; start += EntriesPerPacket)
        {
            var page = metPlayers.Skip(start).Take(EntriesPerPacket).ToList();
            var writer = new PacketWriter();
            foreach (var player in page)
            {
                writer.WriteUInt32((uint)player.LastMetEpochSeconds);
                writer.WriteUInt32((uint)player.CharacterIdentifier);
                writer.WriteFixedString(player.Name, 16);
                writer.WriteUInt8(GameTypeLabel(player.LobbySubtype));
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetMatchHistoryPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.GetMatchHistoryEnd, ErrorCodeConstants.ResultNone, cancellationToken);
    }

    /// <summary>
    /// The trailing label of a history row: the lobby subtype where the
    /// client's table accepts it, otherwise a blank column.
    /// </summary>
    /// <param name="lobbySubtype">Game type of the lobby the match was played in.</param>
    private static int GameTypeLabel(int lobbySubtype) =>
        lobbySubtype is >= 1 and <= 9 ? lobbySubtype : 0;
}

/// <summary>Answers the match-history drill-down with an empty detail list.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetMatchDetailsHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The detail record layout is not decoded, so an empty list is the
        // honest answer: no details is true, invented rows would not be.
        await sessionHelper.SendResultAsync(session, CommandConstants.GetMatchDetailsStart, ErrorCodeConstants.ResultNone, cancellationToken);
        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetMatchDetailsPage, cancellationToken);
        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetMatchDetailsEnd, cancellationToken);
    }
}
