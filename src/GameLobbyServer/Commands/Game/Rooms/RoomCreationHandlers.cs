using System.Text.Json;
using Mgo2Server.GameLobbyServer.Commands.Game.Chat;
using Mgo2Server.GameLobbyServer.Coordination;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Automatch;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Domain.Events;
using Mgo2Server.Shared.Domain.Games;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Persistence.Entities;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Rooms;

/// <summary>
/// Creates a room from the settings the client pushed moments before.
/// <para>
/// It is kept apart from the rest of the room lifecycle because that file is at
/// the project's line limit.
/// </para>
/// <para>
/// A request that names a host role is served here like any other, and written
/// the same way: a real row, owned by the character that asked for it. The
/// reference gives the role that same owner — the character of the client that
/// sent the command, which is the identity its dedicated hosts carry — and the
/// room is a room there too, not a fantasy the lobby keeps to itself.
/// </para>
/// <para>
/// A room named for the dedicated host is refused rather than written, because
/// that name is the gameplay server's own: a row a player opened under it would
/// be reported as a dedicated host the server never created. A request that
/// names a host role is not among those refused — a name on its own is a
/// player's to use.
/// </para>
/// <para>
/// A survival-host room is also offered to the event queue as soon as it exists, rather than
/// at the next sweep: the room that was just created may be the host two paired
/// teams are waiting on. The offer is made after the client has been answered,
/// where the reference makes it just before, so that a client is never handed a
/// match before it is told the room it is in exists — the reference buys the same
/// ordering with the half-second delay before it publishes an assignment.
/// </para>
/// </summary>/// <param name="gameService">Service that owns the rooms.</param>
    /// <param name="characterService">Service that owns the stored settings.</param>
    /// <param name="automatchService">Queue told about the new room.</param>
    /// <param name="assignmentService">Service told the new room may host a match.</param>
    /// <param name="sessionHelper">Helper used to write the replies.</param>/// <param name="externalJoinHintService">Service that raises the tailnet host's joinability line.</param>
/// <param name="coordination">Stream the new room is announced on.</param>
public sealed class CreateGameHandler(
    GameService gameService,
    CharacterService characterService,
    AutomatchService automatchService,
    EventAssignmentService assignmentService,
    SessionHelper sessionHelper,
    ExternalJoinHintService externalJoinHintService,
    LobbyCoordinationClientService coordination) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // Both halves are one refusal: a session missing either fact cannot create
        // a game.
        if (session.CharacterIdentifier is null || session.LobbyIdentifier is null)
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.CreateGameResult,
                ErrorCodeConstants.ResultInvalidSession,
                cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;
        var lobbyIdentifier = session.LobbyIdentifier.Value;

        var settings = await characterService.GetHostSettingsAsync(characterIdentifier, cancellationToken);
        var pushed = settings.FirstOrDefault(row => row.Type == HostSettingsType.Value);
        var defaultMaximumPlayers = pushed is { MaxPlayers: > 0 } ? (int)pushed.MaxPlayers : 8;

        var name = pushed?.Name is { Length: > 0 } pushedName ? pushedName : string.Empty;
        var comment = pushed?.Comment ?? string.Empty;
        var password = pushed is { Password.Length: > 0 } ? pushed.Password : string.Empty;
        var rotation = ReadRotation(pushed);

        // The dedicated-host prefix is the gameplay server's own, and a room
        // named for it is one the details screen reports as a dedicated host.
        // Letting a player open one would put a second room under that name that
        // no dedicated host wrote, so the request is refused with the same code
        // the settings push uses for a block the room cannot be built from.
        if (DedicatedHostNameUtils.IsDedicatedHostName(name))
        {
            await sessionHelper.SendResultAsync(
                session,
                CommandConstants.CreateGameResult,
                ErrorCodeConstants.ResultHostRequestRefused,
                cancellationToken);
            return;
        }

        // A room runs the mode its own settings named, because that is the mode
        // the host picked on the settings screen. A dedicated host is chosen by
        // that mode and its flag rather than by its name, so the name is only a
        // display name.
        var roomSubtype = pushed?.SettingsLobbySubtype ?? 0;

        var game = await gameService.CreateAsync(room =>
        {
            room.HostIdentifier = characterIdentifier;
            room.LobbyIdentifier = lobbyIdentifier;
            room.LobbySubtype = roomSubtype;
            room.Name = name.Length > 0 ? name : $"Game_{characterIdentifier}";
            room.Password = password ?? string.Empty;
            room.Comment = comment;
            room.MaximumPlayers = defaultMaximumPlayers;
            room.Games = JsonSerializer.Serialize(rotation);

            // Nothing else is written onto the room: the settings it runs are
            // the ones its host saved, read from that row when the event rules
            // ask. A copy here would be a second place the same settings live,
            // and a room whose copy and whose host's row disagreed would be read
            // as running settings nobody chose.
        }, cancellationToken);

        // The host is the room's first roster member: the roster row carries
        // its ping, team slot and round attribution.
        await gameService.AddPlayerAsync(game.Identifier, characterIdentifier, cancellationToken);
        session.GameIdentifier = game.Identifier;

        // Told to the queue so a pending match releases without waiting for
        // the next tick to notice the new row.
        automatchService.GameCreated(characterIdentifier, game.Identifier);

        await SendCreatedAsync(session, game.Identifier, cancellationToken);

        // Raised once the room exists and the host is its first member, so the
        // line arrives in a room the client has already been placed in.
        await externalJoinHintService.SendAsync(session, characterIdentifier, cancellationToken);

        // Announced up the coordination stream, where the HTTP API turns it into
        // the channel message: a lobby knows nothing of Discord itself.
        coordination.GameCreated(characterIdentifier, game.Name);

        // The room is offered to the waiting matches now rather than at the next
        // sweep. A room that cannot host one is not asked about twice: the rule is
        // applied there and the call returns as soon as no match is waiting.
        await assignmentService.TryAssignWaitingAsync(lobbyIdentifier, cancellationToken);
    }

    /// <summary>
    /// Answers with the identifier of the room that was created. The reply is a
    /// result word followed by the room identifier, and the client reads the
    /// identifier before testing the result.
    /// </summary>
    /// <param name="session">Connection the room was created for.</param>
    /// <param name="gameIdentifier">Identifier of the created room.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    private Task SendCreatedAsync(
        TcpSession session,
        int gameIdentifier,
        CancellationToken cancellationToken)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(ErrorCodeConstants.ResultNone);
        writer.WriteUInt32((uint)gameIdentifier);
        return sessionHelper.SendPacketAsync(
            session,
            CommandConstants.CreateGameResult,
            writer.Build(),
            cancellationToken);
    }

    /// <summary>Reads the non-empty rotation triples a push stored, rule first.</summary>
    /// <param name="settings">Stored settings row, or <c>null</c> when the host never pushed.</param>
    private static List<int[]> ReadRotation(CharacterHostSettings? settings)
    {
        var rotation = new List<int[]>();
        if (settings is null)
        {
            return rotation;
        }

        for (var index = 0; index < 16; index++)
        {
            var rules = settings.RotationRules;
            var maps = settings.RotationMaps;
            var flags = settings.RotationFlags;
            var rule = rules is not null && index < rules.Length ? rules[index] : (short)0;
            var map = maps is not null && index < maps.Length ? maps[index] : (short)0;
            if (rule == 0 && map == 0)
            {
                break;
            }

            var flag = flags is not null && index < flags.Length ? flags[index] : (short)0;
            rotation.Add([rule, map, flag]);
        }

        return rotation;
    }
}
