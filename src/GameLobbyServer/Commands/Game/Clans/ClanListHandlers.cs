using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Lists the clans on the clan-select screen.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanListHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var clans = await clanService.FindAllWithLeaderAsync(cancellationToken: cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetClanListStart, cancellationToken);

        for (var offset = 0; offset < clans.Count; offset += ClanEntryWriter.MaximumPerPacket)
        {
            var page = clans.Skip(offset).Take(ClanEntryWriter.MaximumPerPacket).ToList();
            var writer = new PacketWriter();
            foreach (var clan in page)
            {
                ClanEntryWriter.WriteClanListEntry(writer, clan);
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanListPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetClanListEnd, cancellationToken);
    }
}

/// <summary>Serves the simpler clan information screen.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanInfoHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var clanIdentifier = packet.Payload.Length >= 4 ? (int)reader.ReadUInt32() : 0;

        if (clanIdentifier == 0)
        {
            await sessionHelper.SendErrorAsync(session, CommandConstants.GetClanInfoResult, ErrorCodeConstants.ErrorClanDoesNotExist, cancellationToken);
            return;
        }

        var details = await clanService.FindByIdFullAsync(clanIdentifier, cancellationToken);
        if (details is null)
        {
            await sessionHelper.SendErrorAsync(session, CommandConstants.GetClanInfoResult, ErrorCodeConstants.ErrorClanDoesNotExist, cancellationToken);
            return;
        }

        var comment = details.Comment.Length > 0 ? details.Comment : "No comment.";

        var writer = new PacketWriter();
        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)details.Identifier);
        writer.WriteFixedString(details.Name, 16);
        writer.WriteUInt32((uint)details.LeaderCharacterIdentifier);
        writer.WriteFixedString(details.LeaderCharacterName, 16);
        writer.WriteUInt8(details.HasEmblem ? 3 : 0);
        writer.WriteFixedString(comment, 128);
        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)details.MemberCount);
        writer.WriteUInt32((uint)details.Identifier);
        writer.WritePadding(32);

        await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanInfoResult, writer.Build(), cancellationToken);
    }
}

/// <summary>Searches for a clan by name.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SearchClanHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var query = string.Empty;
        var exactOnly = false;

        if (packet.Payload.Length >= 18)
        {
            var reader = new PacketReader(packet.Payload);
            exactOnly = reader.ReadUInt8() != 0;
            reader.Skip(1);
            query = reader.ReadFixedString(16);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.SearchClanStart, cancellationToken);

        if (query.Length > 0)
        {
            var allWithLeader = await clanService.FindAllWithLeaderAsync(cancellationToken: cancellationToken);
            var clans = exactOnly
                ? allWithLeader.Where(clan => clan.ClanName == query).ToList()
                : allWithLeader.Where(clan => clan.ClanName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            for (var offset = 0; offset < clans.Count; offset += ClanEntryWriter.MaximumPerPacket)
            {
                var page = clans.Skip(offset).Take(ClanEntryWriter.MaximumPerPacket).ToList();
                var writer = new PacketWriter();
                foreach (var clan in page)
                {
                    ClanEntryWriter.WriteClanListEntry(writer, clan);
                }

                await sessionHelper.SendPacketAsync(session, CommandConstants.SearchClanPage, writer.Build(), cancellationToken);
            }
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.SearchClanEnd, cancellationToken);
    }
}
