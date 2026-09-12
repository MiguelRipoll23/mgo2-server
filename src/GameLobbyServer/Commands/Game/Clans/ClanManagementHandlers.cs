using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Creates a clan with the caller as its first member.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class CreateClanHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Length of the clan name field.</summary>
    private const int ClanNameLength = 15;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.CreateClanResult, null, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var name = reader.ReadFixedString(ClanNameLength);

        var existing = await clanService.FindByNameAsync(name, cancellationToken);
        if (existing is not null)
        {
            await sessionHelper.SendErrorAsync(session, CommandConstants.CreateClanResult, ErrorCodeConstants.ErrorClanNameTaken, cancellationToken);
            return;
        }

        await clanService.CreateAsync(name, characterIdentifier, cancellationToken);
        await sessionHelper.SendPacketAsync(session, CommandConstants.CreateClanResult, null, cancellationToken);
    }
}

/// <summary>Disbands the clan the caller leads.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class DisbandClanHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier)
        {
            var clans = await clanService.FindAllAsync(cancellationToken: cancellationToken);
            foreach (var clan in clans)
            {
                var member = await clanService.GetMemberAsync(clan.Identifier, characterIdentifier, cancellationToken);
                if (member is not null && clan.LeaderIdentifier == member.Identifier)
                {
                    await clanService.DisbandAsync(clan.Identifier, cancellationToken);
                    break;
                }
            }
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.DisbandClanResult, null, cancellationToken);
    }
}

/// <summary>Removes the caller from the clan it names.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class LeaveClanHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length >= 4)
        {
            var reader = new PacketReader(packet.Payload);
            var clanIdentifier = (int)reader.ReadUInt32();
            await clanService.RemoveMemberAsync(clanIdentifier, characterIdentifier, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.LeaveClanResult, null, cancellationToken);
    }
}

/// <summary>Records the caller's application to join a clan.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class ApplyToClanHandler(
    ClanService clanService,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var clanIdentifier = packet.Payload.Length >= 4 ? (int)reader.ReadUInt32() : 0;
        var characterIdentifier = session.CharacterIdentifier;

        if (characterIdentifier is null || clanIdentifier == 0)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.ApplyToClanResult, ErrorCodeConstants.ResultClanNotFound, cancellationToken);
            return;
        }

        var clan = await clanService.FindByIdAsync(clanIdentifier, cancellationToken);
        if (clan is null)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.ApplyToClanResult, ErrorCodeConstants.ResultClanNotFound, cancellationToken);
            return;
        }

        if (await characterService.GetClanInformationAsync(characterIdentifier.Value, cancellationToken) is not null)
        {
            await sessionHelper.SendResultAsync(session, CommandConstants.ApplyToClanResult, ErrorCodeConstants.ResultAlreadyInClan, cancellationToken);
            return;
        }

        await clanService.ApplyAsync(characterIdentifier.Value, clanIdentifier, cancellationToken);
        await sessionHelper.SendResultAsync(session, CommandConstants.ApplyToClanResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>
/// Returns the caller's clan state. Members and applicants receive the record
/// twice, because the client expects two packets in that case.
/// </summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateClanStateHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.UpdateClanStateResult, null, cancellationToken);
            return;
        }

        var membership = await clanService.FindMembershipByCharacterAsync(characterIdentifier, cancellationToken);

        if (membership is not null)
        {
            var role = membership.IsLeader ? 2 : 1;
            var emblemStatus = membership.HasEmblem ? 3 : 0;

            byte[] BuildPayload()
            {
                var writer = new PacketWriter();
                writer.WriteUInt32(0);
                writer.WriteUInt32((uint)membership.ClanIdentifier);
                writer.WriteUInt8(role);
                writer.WriteUInt16(0);
                writer.WriteUInt8(emblemStatus);
                writer.WriteFixedString(membership.ClanName, 16);
                return writer.Build();
            }

            await sessionHelper.SendPacketAsync(session, CommandConstants.UpdateClanStateResult, BuildPayload(), cancellationToken);
            await sessionHelper.SendPacketAsync(session, CommandConstants.UpdateClanStateResult, BuildPayload(), cancellationToken);
            return;
        }

        var notInClan = new PacketWriter();
        notInClan.WriteUInt32(0);
        notInClan.WriteUInt32(0);
        notInClan.WriteUInt8(0xff);
        notInClan.WriteUInt16(0);
        notInClan.WriteUInt8(0);
        notInClan.WriteFixedString(string.Empty, 16);
        await sessionHelper.SendPacketAsync(session, CommandConstants.UpdateClanStateResult, notInClan.Build(), cancellationToken);
    }
}

/// <summary>Transfers clan leadership to another membership row.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class TransferClanLeadershipHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length >= 8)
        {
            var reader = new PacketReader(packet.Payload);
            var clanIdentifier = (int)reader.ReadUInt32();
            var newLeaderMemberIdentifier = (int)reader.ReadUInt32();
            await clanService.SetLeaderAsync(clanIdentifier, newLeaderMemberIdentifier, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.TransferClanLeadershipResult, null, cancellationToken);
    }
}

/// <summary>Assigns the clan's emblem editor to a membership row.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SetEmblemEditorHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length >= 8)
        {
            var reader = new PacketReader(packet.Payload);
            var clanIdentifier = (int)reader.ReadUInt32();
            var memberIdentifier = (int)reader.ReadUInt32();
            await clanService.SetEmblemEditorAsync(clanIdentifier, memberIdentifier, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.SetEmblemEditorResult, null, cancellationToken);
    }
}

/// <summary>Stores the clan comment.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateClanCommentHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length >= 4)
        {
            var reader = new PacketReader(packet.Payload);
            var clanIdentifier = (int)reader.ReadUInt32();
            var comment = reader.ReadFixedString(128);
            await clanService.UpdateCommentAsync(clanIdentifier, comment, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.UpdateClanCommentResult, null, cancellationToken);
    }
}

/// <summary>Stores the clan notice and the member that wrote it.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateClanNoticeHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var characterIdentifier = session.CharacterIdentifier ?? 0;

        if (packet.Payload.Length >= 4)
        {
            var reader = new PacketReader(packet.Payload);
            var clanIdentifier = (int)reader.ReadUInt32();
            var notice = reader.ReadFixedString(512);
            await clanService.UpdateNoticeAsync(clanIdentifier, notice, characterIdentifier, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.UpdateClanNoticeResult, null, cancellationToken);
    }
}
