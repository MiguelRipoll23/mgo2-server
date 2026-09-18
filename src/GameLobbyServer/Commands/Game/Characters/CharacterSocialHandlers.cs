using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Domain.Presence;
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

/// <summary>Lists the requested state of the caller's friends or blocked entries.</summary>
/// <param name="characterService">Service that owns the friends lists.</param>
/// <param name="presenceService">Service that records which lobby a character is in.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetFriendsBlockedListHandler(
    CharacterService characterService,
    CharacterPresenceService presenceService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Maximum number of entries the client's roster holds.</summary>
    private const int MaximumEntries = 32;

    /// <summary>Entries per packet: 17 × 59 bytes of record is 1003, inside the payload.</summary>
    private const int MaximumPerPacket = 17;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // Friends and blocked are two lists in one table, and the state asked for is
        // the request's first byte. The reply is keyed to it rather than sending both
        // states with a per-row marker: the record has no room for one, and the client
        // keys the transaction by the state it asked for.
        var reader = new PacketReader(packet.Payload);
        var state = packet.Payload.Length >= 1 ? reader.ReadUInt8() : 0;

        var characterIdentifier = session.CharacterIdentifier ?? 0;
        var entries = characterIdentifier > 0
            ? await characterService.GetFriendsAndBlockedWithNamesAsync(characterIdentifier, cancellationToken)
            : [];
        entries = [.. entries.Where(entry => entry.Type == state).Take(MaximumEntries)];

        // One query for the whole roster rather than one per row: this is a list screen
        // of up to 32 entries, and asking per entry would be 32 round trips to draw
        // one page. Presence is shared, so a friend connected to another lobby is
        // reported where they are rather than as absent.
        var locations = await presenceService.FindLocationsAsync(
            [.. entries.Select(entry => entry.TargetIdentifier)],
            cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetFriendsBlockedListStart, cancellationToken);

        for (var offset = 0; offset < entries.Count; offset += MaximumPerPacket)
        {
            var page = entries.Skip(offset).Take(MaximumPerPacket).ToList();
            var writer = new PacketWriter();
            foreach (var entry in page)
            {
                WriteEntry(writer, entry, locations);
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetFriendsBlockedListPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetFriendsBlockedListEnd, cancellationToken);
    }

    /// <summary>
    /// Writes one entry: the target, their name, then where they are.
    /// <para>
    /// The record is 59 bytes and carries no state byte of its own, because the state
    /// is the transaction rather than the row. A character who is not connected
    /// anywhere keeps their row and gets a zeroed location, which the client draws as
    /// the lobby column's placeholder rather than dropping the entry — an offline
    /// friend still belongs on the list.
    /// </para>
    /// </summary>
    /// <param name="writer">Writer the entry is appended to.</param>
    /// <param name="entry">Entry being written.</param>
    /// <param name="locations">Where each listed character is.</param>
    private static void WriteEntry(
        PacketWriter writer,
        CharacterFriendEntry entry,
        IReadOnlyDictionary<int, CharacterLocation> locations)
    {
        writer.WriteUInt32((uint)entry.TargetIdentifier);
        writer.WriteFixedString(entry.TargetName, 16);
        CharacterLocationWriter.Write(writer, locations.GetValueOrDefault(entry.TargetIdentifier));
    }
}

/// <summary>Searches for a player by name and reports where they are.</summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="presenceService">Service that records which lobby a character is in.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SearchPlayerHandler(
    CharacterService characterService,
    CharacterPresenceService presenceService,
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
                var locations = await presenceService.FindLocationsAsync(
                    [character.Identifier],
                    cancellationToken);

                var writer = new PacketWriter();
                writer.WriteUInt32((uint)character.Identifier);
                writer.WriteFixedString(character.Name, 16);
                // The tail is the same location block the friend roster carries, and
                // it is the only tail the record has: a searched player connected to
                // any lobby is reported where they are, and one who is not connected
                // gets the empty block, which the client draws as a blank row rather
                // than dropping the result.
                CharacterLocationWriter.Write(writer, locations.GetValueOrDefault(character.Identifier));

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
