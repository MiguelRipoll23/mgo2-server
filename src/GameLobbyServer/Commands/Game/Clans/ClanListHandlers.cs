using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Lists the clans on the clan-select screen, one window per request.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanListHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Length of the paging request: a kind byte, a signed amount, a trailing byte.</summary>
    private const int RequestSize = 6;

    /// <summary>
    /// Size of the window the client asks for, which is the size of its own array
    /// of rows. The request's amount is a one-based entry index rather than a page
    /// number: after being shown one entry the client asks for entry 101.
    /// </summary>
    private const int PageEntries = 100;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The kind byte selects which arm of the client's paging produced the
        // request and the trailing byte is unused; the signed amount is the entry
        // the window is to start at. A request shorter than the three fields is a
        // first fetch.
        var start = 1;
        if (packet.Payload.Length >= RequestSize)
        {
            var reader = new PacketReader(packet.Payload);
            reader.Skip(1);
            start = (int)reader.ReadUInt32();
            reader.Skip(1);
        }

        var total = await clanService.CountAsync(cancellationToken);

        // The client pages optimistically — it asks for the next hundred without
        // knowing whether they exist — so the window is clamped to the last
        // populated page. Honouring 101 literally answers "no clans, starting at
        // 101, one in total", which the screen renders as a page counter running
        // backwards and then corrupts the list on the next scroll.
        var lastPageStart = total == 0 ? 0 : ((total - 1) / PageEntries) * PageEntries;
        var offset = Math.Min(Math.Max(0, start - 1), lastPageStart);

        var clans = await clanService.FindAllWithLeaderAsync(offset, PageEntries, cancellationToken);

        // The header's two words are the offset and then the total, in that order:
        // they are what the client's page indicator is computed from.
        var header = new PacketWriter();
        header.WriteUInt32(ErrorCodeConstants.ResultNone);
        header.WriteUInt32((uint)offset);
        header.WriteUInt32((uint)total);
        await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanListStart, header.Build(), cancellationToken);

        for (var page = 0; page < clans.Count; page += ClanEntryWriter.MaximumPerPacket)
        {
            var writer = new PacketWriter();
            foreach (var clan in clans.Skip(page).Take(ClanEntryWriter.MaximumPerPacket))
            {
                ClanEntryWriter.WriteClanListEntry(writer, clan);
            }

            if (writer.Size > 0)
            {
                await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanListPage, writer.Build(), cancellationToken);
            }
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
    /// <summary>Length of the search query field.</summary>
    private const int SearchQueryLength = 16;

    /// <summary>Size of the two toggles that precede the query.</summary>
    private const int ToggleLength = 2;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The request carries the same two search settings the player search sends,
        // in the same order and with the same polarity: a match-criteria byte, where
        // zero asks for a partial match and one for the whole name, then a case byte
        // whose one is CASE SENSITIVE and whose zero is the ignoring value. Reading
        // the name from the start of the payload would take the toggles as its first
        // characters, so the toggles have to be consumed first.
        var query = string.Empty;
        var exactOnly = false;
        var ignoreCase = false;

        if (packet.Payload.Length >= ToggleLength + SearchQueryLength)
        {
            var reader = new PacketReader(packet.Payload);
            exactOnly = reader.ReadUInt8() != 0;
            ignoreCase = reader.ReadUInt8() == 0;
            query = reader.ReadFixedString(SearchQueryLength);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.SearchClanStart, cancellationToken);

        if (query.Length > 0)
        {
            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var allWithLeader = await clanService.FindAllWithLeaderAsync(cancellationToken: cancellationToken);
            var clans = allWithLeader
                .Where(clan => exactOnly
                    ? clan.ClanName.Equals(query, comparison)
                    : clan.ClanName.Contains(query, comparison))
                .ToList();

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
