using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>
/// Serves the published emblem of a clan. There is one emblem, not a draft and a
/// published copy: an upload replaces it, so every fetch answers with the same bytes.
/// </summary>
/// <remarks>
/// <para>The client's emblem decoder (0xA9B3E8) reads a 768-byte block: a 4-byte "EMBD" magic,
/// a negative flag byte, a 48-byte palette, 512 packed 4-bit pixels and 203 bytes of padding.</para>
/// <para>That size is also the wire size: the 0x4b50 sender (0xD5804C) memcpy's exactly 0x300
/// bytes into the packet, and both reply parsers NUL-terminate at +768, so a truncated reply
/// loses the tail of the image.</para>
/// </remarks>
public abstract class ClanEmblemHandlerBase : ICommandHandler
{
    private readonly ClanService clanService;
    private readonly SessionHelper sessionHelper;

    /// <summary>Creates the handler over the services it needs.</summary>
    /// <param name="clanService">Service that owns the clans.</param>
    /// <param name="sessionHelper">Helper used to write the replies.</param>
    protected ClanEmblemHandlerBase(ClanService clanService, SessionHelper sessionHelper)
    {
        this.clanService = clanService;
        this.sessionHelper = sessionHelper;
    }

    /// <summary>Bytes one emblem occupies on the wire.</summary>
    public const int EmblemSize = 768;

    /// <summary>Reply the handler answers on.</summary>
    protected abstract ushort ReplyCommand { get; }

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length < 4)
        {
            await sessionHelper.SendErrorAsync(session, ReplyCommand, ErrorCodeConstants.ErrorClanDoesNotExist, cancellationToken);
            return;
        }

        var reader = new PacketReader(packet.Payload);
        var clanIdentifier = (int)reader.ReadUInt32();
        var clan = await clanService.FindByIdAsync(clanIdentifier, cancellationToken);

        if (clan is null)
        {
            await sessionHelper.SendErrorAsync(session, ReplyCommand, ErrorCodeConstants.ErrorClanDoesNotExist, cancellationToken);
            return;
        }

        await sessionHelper.SendPacketAsync(session, ReplyCommand, BuildEmblemPayload(clan.Emblem), cancellationToken);
    }

    /// <summary>Builds the emblem reply: a status word followed by the fixed-width emblem.</summary>
    /// <param name="emblemBytes">Emblem bytes, or <c>null</c> when the clan has none.</param>
    public static byte[] BuildEmblemPayload(byte[]? emblemBytes)
    {
        var writer = new PacketWriter();
        writer.WriteUInt32(0);

        if (emblemBytes is { Length: > 0 })
        {
            if (emblemBytes.Length >= EmblemSize)
            {
                writer.WriteBytes(emblemBytes.AsSpan(0, EmblemSize));
            }
            else
            {
                writer.WriteBytes(emblemBytes);
                writer.WritePadding(EmblemSize - emblemBytes.Length);
            }
        }
        else
        {
            writer.WritePadding(EmblemSize);
        }

        return writer.Build();
    }
}

/// <summary>Serves the published emblem of a clan shown in a lobby.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanEmblemLobbyHandler(ClanService clanService, SessionHelper sessionHelper)
    : ClanEmblemHandlerBase(clanService, sessionHelper)
{
    /// <inheritdoc />
    protected override ushort ReplyCommand => CommandConstants.GetClanEmblemLobbyResult;
}

/// <summary>Serves the published emblem of a clan.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanEmblemHandler(ClanService clanService, SessionHelper sessionHelper)
    : ClanEmblemHandlerBase(clanService, sessionHelper)
{
    /// <inheritdoc />
    protected override ushort ReplyCommand => CommandConstants.GetClanEmblemResult;
}

/// <summary>Serves the clan-detail fetch, whose emblem block is the published emblem.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanDetailHandler(ClanService clanService, SessionHelper sessionHelper)
    : ClanEmblemHandlerBase(clanService, sessionHelper)
{
    /// <inheritdoc />
    protected override ushort ReplyCommand => CommandConstants.GetClanDetailResult;
}

/// <summary>
/// Stores the emblem the client submits. The 0x4b50 payload is <c>{u8 mode, byte[768]}</c> —
/// the sender (0xD5804C) writes a u8 from stack 0x5A0 and then a fixed 0x300-byte block, so
/// the clan is the caller's own and there is no length field to honour.
/// </summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SetClanEmblemHandler(
    ClanService clanService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        // 0x4b51 is one s4 result word, not an empty body: the client's parser (0xD555D4)
        // reads it and publishes it as the emblem request's result.
        var result = ErrorCodeConstants.ResultNone;

        if (packet.Payload.Length >= 1 + ClanEmblemHandlerBase.EmblemSize && session.CharacterIdentifier is { } characterIdentifier)
        {
            var reader = new PacketReader(packet.Payload);
            var mode = reader.ReadUInt8();

            // Publishing an emblem is a clan-member operation; without this an
            // arbitrary player could overwrite any clan's published emblem.
            var membership = await clanService.FindMembershipByCharacterAsync(characterIdentifier, cancellationToken);
            if (membership is null)
            {
                result = ErrorCodeConstants.ResultClanEmblemUpdateFailed;
            }
            else
            {
                var emblemBytes = reader.ReadBytes(ClanEmblemHandlerBase.EmblemSize);

                // Mode 3 is "put on display" and the only value the client post-processes;
                // it is also the emblem flag the profile replies carry. The other modes
                // observed (2 and 4) are stored as uploads and nothing else, and mode 0
                // clears the emblem instead.
                if (mode == 0)
                {
                    await clanService.ClearEmblemAsync(membership.ClanIdentifier, cancellationToken);
                }
                else
                {
                    await clanService.SetEmblemAsync(membership.ClanIdentifier, emblemBytes, cancellationToken);
                }
            }
        }
        else
        {
            result = ErrorCodeConstants.ResultClanEmblemUpdateFailed;
        }

        await sessionHelper.SendResultAsync(session, CommandConstants.SetClanEmblemResult, result, cancellationToken);
    }
}
