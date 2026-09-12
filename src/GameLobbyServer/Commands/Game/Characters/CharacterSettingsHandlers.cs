using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>Serves the chat macros of the session character, one page at a time.</summary>
/// <param name="characterService">Service that owns the macros.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetChatMacrosHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetChatMacrosResult, null, cancellationToken);
            return;
        }

        var macros = await characterService.GetChatMacrosAsync(characterIdentifier, cancellationToken);

        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetChatMacrosResult,
            CharacterPayloadBuilder.BuildChatMacrosPayload(macros, 0),
            cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetChatMacrosResult,
            CharacterPayloadBuilder.BuildChatMacrosPayload(macros, 1),
            cancellationToken);
    }
}

/// <summary>Stores the chat macros the client submits.</summary>
/// <param name="characterService">Service that owns the macros.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateChatMacrosHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is { } characterIdentifier && packet.Payload.Length > 0)
        {
            var reader = new PacketReader(packet.Payload);
            var macros = new List<Shared.Persistence.Entities.CharacterChatMacro>();

            while (reader.Remaining >= CharacterPayloadBuilder.MacroTextLength + 2)
            {
                var type = reader.ReadUInt8();
                var index = reader.ReadUInt8();
                var text = reader.ReadFixedString(CharacterPayloadBuilder.MacroTextLength);

                macros.Add(new Shared.Persistence.Entities.CharacterChatMacro
                {
                    Type = type,
                    Index = index,
                    Text = text,
                });
            }

            await characterService.UpdateChatMacrosAsync(characterIdentifier, macros, cancellationToken);
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.UpdateChatMacrosResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Serves the stored gameplay options and user-interface settings.</summary>
/// <param name="characterService">Service that owns the stored options.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetGameplayOptionsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var character = session.CharacterIdentifier is { } characterIdentifier
            ? await characterService.FindByIdAsync(characterIdentifier, cancellationToken)
            : null;

        var stored = GameplayOptionsCodec.ParseStored(character?.GameplayOptions);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetGameplayOptionsResult,
            GameplayOptionsCodec.BuildPayload(stored),
            cancellationToken);
    }
}

/// <summary>Stores the gameplay options the client submits.</summary>
/// <param name="characterService">Service that owns the stored options.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateGameplayOptionsHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var parsed = GameplayOptionsCodec.ParsePayload(packet.Payload);

        if (session.CharacterIdentifier is { } characterIdentifier && parsed is not null)
        {
            // The write-back is the read payload truncated before the list
            // preferences trailer, so it shares the codec with the reader.
            await characterService.UpdateGameplayOptionsAsync(
                characterIdentifier,
                parsed.ToJsonString(),
                cancellationToken);
        }

        // The reply carries a result word; without it the enter-game flow stalls.
        await sessionHelper.SendResultAsync(session, CommandConstants.UpdateGameplayOptionsResult, ErrorCodeConstants.ResultNone, cancellationToken);
    }
}

/// <summary>Acknowledges the user-interface settings blob, which is not persisted.</summary>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class UpdateUiSettingsHandler(SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken) =>
        sessionHelper.SendResultAsync(session, CommandConstants.UpdateUiSettingsResult, ErrorCodeConstants.ResultNone, cancellationToken);
}
