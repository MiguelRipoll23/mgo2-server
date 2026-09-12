using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Accepts or declines a pending clan application.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
/// <param name="approve">Whether the application is accepted rather than declined.</param>
public sealed class ReviewClanApplicationHandler(
    ClanService clanService,
    SessionHelper sessionHelper,
    bool approve) : ICommandHandler
{
    private readonly ushort replyCommand = approve
        ? CommandConstants.AcceptClanJoinResult
        : CommandConstants.DeclineClanJoinResult;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var targetIdentifier = packet.Payload.Length >= 8 ? (int)ReadSecondWord(reader) : 0;

        var membership = session.CharacterIdentifier is { } characterIdentifier
            ? await clanService.FindMembershipByCharacterAsync(characterIdentifier, cancellationToken)
            : null;

        if (membership is not { IsLeader: true })
        {
            await sessionHelper.SendResultAsync(session, replyCommand, ErrorCodeConstants.ResultGeneral, cancellationToken);
            return;
        }

        var changed = approve
            ? await clanService.ApproveAsync(membership.ClanIdentifier, targetIdentifier, cancellationToken)
            : await clanService.DeclineAsync(membership.ClanIdentifier, targetIdentifier, cancellationToken);

        await sessionHelper.SendResultAsync(
            session,
            replyCommand,
            changed ? ErrorCodeConstants.ResultNone : ErrorCodeConstants.ResultGeneral,
            cancellationToken);
    }

    private static uint ReadSecondWord(PacketReader reader)
    {
        reader.ReadUInt32();
        return reader.ReadUInt32();
    }
}

/// <summary>Accepts a pending clan application.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class AcceptClanJoinHandler(ClanService clanService, SessionHelper sessionHelper)
    : ICommandHandler
{
    private readonly ReviewClanApplicationHandler inner = new(clanService, sessionHelper, approve: true);

    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        inner.HandleAsync(session, packet, cancellationToken);
}

/// <summary>Declines a pending clan application.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class DeclineClanJoinHandler(ClanService clanService, SessionHelper sessionHelper)
    : ICommandHandler
{
    private readonly ReviewClanApplicationHandler inner = new(clanService, sessionHelper, approve: false);

    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        inner.HandleAsync(session, packet, cancellationToken);
}

/// <summary>Removes a member from a clan.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class BanishClanMemberHandler(
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
            var targetIdentifier = (int)reader.ReadUInt32();
            await clanService.RemoveMemberAsync(clanIdentifier, targetIdentifier, cancellationToken);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.BanishClanMemberResult, null, cancellationToken);
    }
}
