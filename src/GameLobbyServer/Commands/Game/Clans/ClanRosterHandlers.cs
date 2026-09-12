using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Serves the full record of one clan.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanMemberInfoHandler(
    ClanService clanService,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length < 4)
        {
            await sessionHelper.SendErrorAsync(session, CommandConstants.GetClanMemberInfoResult, ErrorCodeConstants.ErrorClanDoesNotExist, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var clanIdentifier = (int)reader.ReadUInt32();

        var details = await clanService.FindByIdFullAsync(clanIdentifier, cancellationToken);
        var clan = await clanService.FindByIdAsync(clanIdentifier, cancellationToken);

        if (details is null || clan is null)
        {
            await sessionHelper.SendErrorAsync(session, CommandConstants.GetClanMemberInfoResult, ErrorCodeConstants.ErrorClanDoesNotExist, cancellationToken);
            return;
        }

        var member = session.CharacterIdentifier is { } characterIdentifier
            ? await clanService.GetMemberAsync(clanIdentifier, characterIdentifier, cancellationToken)
            : null;

        // The emblem editor is shown to members, defaulting to the leader.
        var emblemEditorCharacterIdentifier = 0;
        if (member is not null)
        {
            emblemEditorCharacterIdentifier = clan.EmblemEditorIdentifier is { } editorIdentifier
                ? await clanService.GetCharacterIdentifierByMemberAsync(editorIdentifier, cancellationToken) ?? details.LeaderCharacterIdentifier
                : details.LeaderCharacterIdentifier;
        }

        var noticeWriterCharacterName = string.Empty;
        if (clan.Notice.Length > 0 && clan.NoticeWriterIdentifier is { } writerIdentifier)
        {
            var writerCharacterIdentifier = await clanService.GetCharacterIdentifierByMemberAsync(writerIdentifier, cancellationToken);
            if (writerCharacterIdentifier is not null)
            {
                var writerCharacter = await characterService.FindByIdAsync(writerCharacterIdentifier.Value, cancellationToken);
                noticeWriterCharacterName = writerCharacter?.Name ?? "[Deleted]";
            }
        }

        var comment = clan.Comment.Length > 0 ? clan.Comment : "No comment.";

        var response = new PacketWriter();
        response.WriteUInt32(0);
        response.WriteUInt32((uint)clan.Identifier);
        response.WriteFixedString(clan.Name, 16);
        response.WriteUInt8(0);
        response.WriteUInt32((uint)details.LeaderCharacterIdentifier);
        response.WriteFixedString(details.LeaderCharacterName, 16);
        response.WriteUInt32(0);
        response.WriteFixedString(string.Empty, 16);
        response.WritePadding(32);
        response.WritePadding(2);
        response.WriteUInt8(clan.EmblemWorkInProgress is not null ? 2 : 0);
        response.WriteUInt8(clan.Emblem is not null ? 3 : 0);
        response.WriteFixedString(comment, 128);
        response.WriteUInt32((uint)emblemEditorCharacterIdentifier);
        response.WriteFixedString(clan.Notice, 512);
        response.WriteUInt32((uint)clan.NoticeTime);
        response.WriteFixedString(noticeWriterCharacterName, 16);
        response.WriteUInt32(0);
        response.WriteUInt32(0);
        response.WriteUInt32((uint)clan.Identifier);

        await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanMemberInfoResult, response.Build(), cancellationToken);
    }
}

/// <summary>Lists the members of a clan with where each of them is now.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="activeGameSessions">Connections currently in the lobby.</param>
/// <param name="gameService">Service that owns the rooms.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanRosterHandler(
    ClanService clanService,
    ActiveGameSessionsService activeGameSessions,
    GameService gameService,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var clanIdentifier = packet.Payload.Length >= 4 ? (int)reader.ReadUInt32() : 0;

        var members = clanIdentifier > 0
            ? await clanService.GetMembersWithNamesAsync(clanIdentifier, cancellationToken)
            : [];

        var sessionsByCharacter = activeGameSessions.List()
            .Where(candidate => candidate.CharacterIdentifier is not null && candidate.GameIdentifier is not null)
            .ToDictionary(candidate => candidate.CharacterIdentifier!.Value);

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetClanRosterStart, cancellationToken);

        for (var offset = 0; offset < members.Count; offset += ClanEntryWriter.MaximumPerPacket)
        {
            var page = members.Skip(offset).Take(ClanEntryWriter.MaximumPerPacket).ToList();
            var writer = new PacketWriter();

            foreach (var member in page)
            {
                writer.WriteUInt32((uint)member.CharacterIdentifier);
                writer.WriteFixedString(member.CharacterName, 16);
                writer.WriteUInt8(1);
                writer.WriteUInt32(0);
                writer.WriteUInt32((uint)member.CharacterIdentifier);

                var wroteGameInformation = false;
                if (sessionsByCharacter.TryGetValue(member.CharacterIdentifier, out var memberSession) &&
                    memberSession.LobbyIdentifier is { } lobbyIdentifier)
                {
                    var game = await gameService.FindByIdAsync(memberSession.GameIdentifier!.Value, cancellationToken);
                    var lobby = await gameService.FindLobbyAsync(lobbyIdentifier, cancellationToken);
                    if (game is not null && lobby is not null)
                    {
                        var host = await characterService.FindByIdAsync(game.HostIdentifier, cancellationToken);
                        writer.WriteUInt16(lobby.Identifier);
                        writer.WriteFixedString(lobby.Name, 16);
                        writer.WriteUInt32((uint)game.Identifier);
                        writer.WriteFixedString(host?.Name ?? string.Empty, 16);
                        writer.WriteUInt8(lobby.SubtypeIdentifier);
                        wroteGameInformation = true;
                    }
                }

                if (!wroteGameInformation)
                {
                    writer.WriteUInt16(0);
                    writer.WriteFixedString(string.Empty, 16);
                    writer.WriteUInt32(0);
                    writer.WriteFixedString(string.Empty, 16);
                    writer.WriteUInt8(0);
                }
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanRosterPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetClanRosterEnd, cancellationToken);
    }
}

/// <summary>Lists the pending applicants of a clan.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanApplicantsHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Size of one applicant record.</summary>
    private const int ApplicantRecordSize = 93;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var clanIdentifier = packet.Payload.Length >= 4 ? (int)reader.ReadUInt32() : 0;
        var applicants = clanIdentifier > 0
            ? await clanService.GetApplicantsAsync(clanIdentifier, cancellationToken)
            : [];

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetClanApplicantsStart, cancellationToken);

        for (var offset = 0; offset < applicants.Count; offset += ClanEntryWriter.MaximumPerPacket)
        {
            var page = applicants.Skip(offset).Take(ClanEntryWriter.MaximumPerPacket).ToList();
            var writer = new PacketWriter();

            foreach (var applicant in page)
            {
                var recordStart = writer.Size;
                writer.WriteUInt32((uint)applicant.CharacterIdentifier);
                // Applications carry no message on the wire.
                writer.WritePadding(64);
                writer.WriteFixedString(applicant.Name, 16);
                writer.WritePadding(ApplicantRecordSize - (writer.Size - recordStart));
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanApplicantsPage, writer.Build(), cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(session, CommandConstants.GetClanApplicantsEnd, cancellationToken);
    }
}

/// <summary>Answers the clan-statistics request with two empty packets.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanStatsHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanStatsResult, null, cancellationToken);
        await sessionHelper.SendPacketAsync(session, CommandConstants.GetClanStatsDetail, null, cancellationToken);
    }
}
