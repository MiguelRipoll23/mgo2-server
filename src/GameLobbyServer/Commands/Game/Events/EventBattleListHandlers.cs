using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Options;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;
using Microsoft.Extensions.Options;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Serves the Survival battle list. The whole stream is built before the start
/// marker is written, so a build failure cannot leave the client waiting for a
/// missing end marker.
/// </summary>
public sealed class GetSurvivalBattleListHandler(
    EventMatchService matchService,
    EventTeamService teamService,
    IOptions<EventOptions> options,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The battle list belongs to a lobby, so a connection in none has nothing
        // to stream.
        if (session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The command carries no body; one that does is asking for something the
        // list does not answer.
        if (packet.Payload.Length != 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var battles = new List<(EventSnapshot First, EventSnapshot Second)>();
        foreach (var match in await matchService.FindActiveByLobbyAsync(lobbyIdentifier, cancellationToken))
        {
            var first = await teamService.FindAsync(match.FirstTeamIdentifier, cancellationToken);
            var second = await teamService.FindAsync(match.SecondTeamIdentifier, cancellationToken);
            if (first is null || second is null)
            {
                continue;
            }

            battles.Add((
                EventTeamService.BuildSnapshot(first),
                EventTeamService.BuildSnapshot(second)));
        }

        var startWriter = new PacketWriter();
        EventBattleListUtils.WriteStart(
            startWriter,
            EventConstants.TransientEventIdentifier,
            EventHostEnvironment.CreateDefault(),
            options.Value.WinRewardTable(),
            options.Value.ParticipationReward);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSurvivalBattleListStart,
            startWriter.Build(),
            cancellationToken);

        var rowIndex = 0;
        foreach (var battle in battles)
        {
            await SendRowAsync(session, rowIndex++, battle.First, cancellationToken);
            await SendRowAsync(session, rowIndex++, battle.Second, cancellationToken);
        }

        var endWriter = new PacketWriter();
        endWriter.WriteInt32(EventConstants.TransientEventIdentifier);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSurvivalBattleListEnd,
            endWriter.Build(),
            cancellationToken);
    }

    private Task SendRowAsync(
        TcpSession session,
        int rowIndex,
        EventSnapshot team,
        CancellationToken cancellationToken)
    {
        var writer = new PacketWriter();
        EventBattleListUtils.WriteItem(
            writer,
            rowIndex,
            team,
            EventBattleListUtils.AverageParticipantExperience(team));
        return sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetSurvivalBattleListPage,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetSurvivalBattleListStart,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>Returns the detail of one team listed in the battle list.</summary>
public sealed class GetBattleTeamInformationHandler(
    EventTeamService teamService,
    EventMatchService matchService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var selectedTeamIdentifier = packet.Payload.Length == 8
            ? (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.Payload)
            : 0;

        // The second word is the list correlation the client was shown. Only the
        // transient correlation this server publishes is answerable.
        var correlationIdentifier = packet.Payload.Length == 8
            ? (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.Payload.AsSpan(4))
            : 0;

        // Zero names no team, and any correlation but the transient one names a
        // list this server never published.
        if (selectedTeamIdentifier == 0
            || correlationIdentifier != EventConstants.TransientEventIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // The roster belongs to the caller's own lobby.
        if (session.LobbyIdentifier is not { } lobbyIdentifier)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var match = await matchService.FindActiveByTeamAsync(selectedTeamIdentifier, cancellationToken);
        var team = match is not null && match.LobbyIdentifier == lobbyIdentifier
            ? await teamService.FindAsync(selectedTeamIdentifier, cancellationToken)
            : null;

        if (team is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, EventTeamService.BuildSnapshot(team));
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetBattleTeamInformationResult,
            writer.Build(),
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetBattleTeamInformationResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}
