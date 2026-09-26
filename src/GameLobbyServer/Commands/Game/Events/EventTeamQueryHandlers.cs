using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>
/// Streams the joinable teams of the lobby the caller is in, real rows and
/// in-memory fake teams alike.
/// <para>
/// A fake team is listed after the rows because it is the testing device rather
/// than the population; both are written from the same snapshot projection, so
/// the client cannot tell where a row came from.
/// </para>
/// </summary>
public sealed class GetEventTeamListHandler(
    EventTeamService teamService,
    FakeTeamService fakeTeamService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // The list belongs to a lobby, so a connection in none has nothing to
        // stream.
        if (session.LobbyIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobbyIdentifier = session.LobbyIdentifier.Value;

        // The command carries no body; one that does is asking for something the
        // list does not answer.
        if (packet.Payload.Length != 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var teams = await teamService.FindJoinableAsync(lobbyIdentifier, cancellationToken);

        await sessionHelper.SendStartEndPacketAsync(
            session,
            CommandConstants.GetEventTeamListStart,
            cancellationToken);

        foreach (var team in teams)
        {
            var writer = new PacketWriter();
            EventTeamListUtils.WriteItem(writer, EventTeamService.BuildSnapshot(team));
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetEventTeamListPage,
                writer.Build(),
                cancellationToken);
        }

        foreach (var fakeTeam in fakeTeamService.ListTeams(lobbyIdentifier))
        {
            var writer = new PacketWriter();
            EventTeamListUtils.WriteItem(writer, fakeTeam.BuildSnapshot());
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetEventTeamListPage,
                writer.Build(),
                cancellationToken);
        }

        await sessionHelper.SendStartEndPacketAsync(
            session,
            CommandConstants.GetEventTeamListEnd,
            cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.GetEventTeamListStart,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}

/// <summary>Returns the detail of one joinable team, real or in-memory.</summary>
public sealed class GetEventTeamDetailsHandler(
    EventTeamService teamService,
    FakeTeamService fakeTeamService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var teamIdentifier = packet.Payload.Length == 4
            ? (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(packet.Payload)
            : 0;

        // An in-memory team is resolved first: its identifier comes from the
        // fake range, which no row can occupy, so the two can never collide and
        // the lookup is free either way.
        var fakeTeam = teamIdentifier > 0
            ? fakeTeamService.FindTeam(teamIdentifier)
            : null;
        if (fakeTeam is not null)
        {
            var fakeWriter = new PacketWriter();
            EventSnapshotUtils.WriteCompact(fakeWriter, fakeTeam.BuildSnapshot());
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetEventTeamDetailsResult,
                fakeWriter.Build(),
                cancellationToken);
            return;
        }

        var team = teamIdentifier > 0
            ? await teamService.FindAsync(teamIdentifier, cancellationToken)
            : null;

        if (team is null)
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.GetEventTeamDetailsResult,
                ErrorCodeConstants.ResultGeneral,
                cancellationToken);
            return;
        }

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, EventTeamService.BuildSnapshot(team));
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetEventTeamDetailsResult,
            writer.Build(),
            cancellationToken);
    }
}