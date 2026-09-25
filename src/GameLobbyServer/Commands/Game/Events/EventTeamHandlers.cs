using Microsoft.Extensions.Logging;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Lobbies;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Events;

/// <summary>Creates an event team and answers with its active-game snapshot.</summary>
public sealed class CreateEventTeamHandler(
    EventTeamService teamService,
    CharacterService characterService,
    LobbyService lobbyService,
    SessionHelper sessionHelper,
    ILogger<CreateEventTeamHandler> logger) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // A team is formed by a character.
        if (session.CharacterIdentifier is null)
        {
            await RefuseAsync(session, "no-character", cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // It is formed in the lobby the connection landed in.
        if (session.LobbyIdentifier is null)
        {
            await RefuseAsync(session, "no-lobby", cancellationToken);
            return;
        }

        var lobbyIdentifier = session.LobbyIdentifier.Value;

        // The request has to carry the whole create record.
        if (!EventTeamCreationUtils.CanReadCreateRecord(packet.Payload.Length))
        {
            await RefuseAsync(
                session,
                $"short-record/{packet.Payload.Length}",
                cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var name = reader.ReadFixedString(16);
        var comment = reader.ReadFixedString(128);
        var flagBits = reader.ReadUInt8();
        var password = reader.ReadFixedString(16);
        var matchType = reader.ReadUInt8();

        // The record ends here. Its last six bytes are a provable constant zero
        // — the screen memsets its instance on entry, no site writes them, and
        // the reply parser has no field for them either — so nothing is read
        // past the match type, and the size above is what bounds the request.

        if (!EventTeamTextUtils.IsValidTeamName(name)
            || !EventTeamTextUtils.IsValidPassword(flagBits, password))
        {
            await RefuseAsync(
                session,
                $"rejected-text/{packet.Payload.Length}/{name.Length}",
                cancellationToken);
            return;
        }

        // The team's match type is the lobby it forms in; a mismatch means a
        // Survival team is being created in a Tournament lobby.
        var lobby = await lobbyService.FindByIdAsync(lobbyIdentifier, cancellationToken);
        if (matchType != lobby.SubtypeIdentifier)
        {
            await RefuseAsync(
                session,
                $"match-type/{matchType}-in-{lobby.SubtypeIdentifier}",
                cancellationToken);
            return;
        }

        // The request names no event, so the team is placed by the lobby it was
        // formed in rather than by anything the client could have asked for.
        if (!EventTeamCreationUtils.TryResolveEventIdentifier(
                lobby.SubtypeIdentifier,
                session.SelectedEventIdentifier,
                out var eventIdentifier))
        {
            await RefuseAsync(
                session,
                $"no-event/{lobby.SubtypeIdentifier}",
                cancellationToken);
            return;
        }

        logger.LogInformation(
            "Character {CharacterIdentifier} is forming team \"{TeamName}\" in lobby {LobbyIdentifier} (subtype {LobbySubtype}, match type {MatchType}, event {EventIdentifier}) from a {PayloadLength}-byte request",
            characterIdentifier,
            name,
            lobbyIdentifier,
            lobby.SubtypeIdentifier,
            matchType,
            eventIdentifier,
            packet.Payload.Length);

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var team = await teamService.CreateAsync(
            characterIdentifier,
            character?.Name ?? string.Empty,
            character?.Experience ?? 0,
            name,
            comment,
            flagBits,
            password,
            matchType,
            lobbyIdentifier,
            eventIdentifier,
            cancellationToken);

        logger.LogInformation(
            "Team {TeamIdentifier} formed in lobby {LobbyIdentifier} under event {EventIdentifier}",
            team.Identifier,
            lobbyIdentifier,
            eventIdentifier);

        session.EventTeamIdentifier = team.Identifier;

        // A team that has just been formed supersedes any event the connection
        // had merely looked at. Leaving the selection in place would let the
        // shared entry request read an older screen than the one the player is
        // on, and cancel a team instead of entering an event.
        session.SelectedEventIdentifier = null;

        var snapshot = EventTeamService.BuildSnapshot(team);

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, snapshot);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.CreateEventTeamResult,
            writer.Build(),
            cancellationToken);
    }

    /// <summary>
    /// Refuses the request, recording why.
    /// <para>
    /// The refusal is the client's "Unable to create team", carrying
    /// <see cref="ErrorCodeConstants.ResultGeneral"/> as its result word, so it
    /// reaches the player as a dialog and leaves nothing behind on the server.
    /// Every reason is logged with the request that caused it, because a create
    /// that fails this way is otherwise indistinguishable from one that was
    /// never sent.
    /// </para>
    /// </summary>
    /// <param name="session">Connection the request arrived on.</param>
    /// <param name="reason">Why the request was refused.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private Task RefuseAsync(TcpSession session, string reason, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Refused a team creation for {LogPrefix} because {Reason}; answered 0x{Result:x8}",
            session.LogPrefix,
            reason,
            ErrorCodeConstants.ResultGeneral);
        return sessionHelper.SendResultAsync(
            session,
            CommandConstants.CreateEventTeamResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
    }
}

/// <summary>Joins a team, commits the membership and pushes the new roster slot.</summary>
public sealed class JoinEventTeamHandler(
    EventTeamService teamService,
    EventTeamPushService pushService,
    EventInvitationService invitationService,
    EventMatchmakingService matchmakingService,
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Logical size of a join request.</summary>
    private const int LogicalWireSize = 20;

    /// <summary>Logical size plus the transport padding the client may append.</summary>
    private const int PaddedWireSize = 24;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // A join comes from a character.
        if (session.CharacterIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

        // It joins a team in the lobby the connection landed in.
        if (session.LobbyIdentifier is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var lobbyIdentifier = session.LobbyIdentifier.Value;

        // The request is the join record, with or without the transport padding
        // the client may append.
        var hasAcceptedSize = packet.Payload.Length is LogicalWireSize or PaddedWireSize;
        if (!hasAcceptedSize)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var teamIdentifier = (int)reader.ReadUInt32();
        var password = reader.ReadFixedString(16);
        if (teamIdentifier == 0)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var (outcome, team) = await teamService.JoinAsync(
            teamIdentifier,
            lobbyIdentifier,
            characterIdentifier,
            character?.Name ?? string.Empty,
            character?.Experience ?? 0,
            password,
            cancellationToken);

        if (outcome != EventJoinOutcome.Joined || team is null)
        {
            await RefuseAsync(session, cancellationToken);
            return;
        }

        // Membership is committed here, so the reservation an accepted
        // invitation made for this character is released with it.
        invitationService.ClearAccepted(characterIdentifier);
        session.EventTeamIdentifier = team.Identifier;
        session.SelectedEventIdentifier = null;
        var snapshot = EventTeamService.BuildSnapshot(team);

        var writer = new PacketWriter();
        EventSnapshotUtils.WriteCompact(writer, snapshot);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.JoinEventTeamResult,
            writer.Build(),
            cancellationToken);

        var slot = snapshot.IndexOfParticipant(characterIdentifier);
        await pushService.PushParticipantAddedAsync(snapshot, slot, session, cancellationToken);

        // A roster change can make a queued team no longer ready, which is
        // exactly what the reconcile turns into a cancellation.
        await matchmakingService.ReconcileAsync(team.Identifier, cancellationToken);
    }

    private Task RefuseAsync(TcpSession session, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(
            session,
            CommandConstants.JoinEventTeamResult,
            ErrorCodeConstants.ResultGeneral,
            cancellationToken);
}
