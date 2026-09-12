using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Clans;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Clans;

/// <summary>Serves the emblem of one clan, published or still being edited.</summary>
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
    public const int EmblemSize = 565;

    /// <summary>Reply the handler answers on.</summary>
    protected abstract ushort ReplyCommand { get; }

    /// <summary>Whether the emblem still being edited is preferred over the published one.</summary>
    protected virtual bool PreferWorkInProgress => false;

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

        var emblem = PreferWorkInProgress
            ? clan.EmblemWorkInProgress ?? clan.Emblem
            : clan.Emblem;

        await sessionHelper.SendPacketAsync(session, ReplyCommand, BuildEmblemPayload(emblem), cancellationToken);
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

/// <summary>Serves the emblem a clan is currently editing.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetClanEmblemWorkInProgressHandler(ClanService clanService, SessionHelper sessionHelper)
    : ClanEmblemHandlerBase(clanService, sessionHelper)
{
    /// <inheritdoc />
    protected override ushort ReplyCommand => CommandConstants.GetClanEmblemWorkInProgressResult;

    /// <inheritdoc />
    protected override bool PreferWorkInProgress => true;
}

/// <summary>Stores the emblem the client submits.</summary>
/// <param name="clanService">Service that owns the clans.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class SetClanEmblemHandler(
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
            var emblemLength = (int)reader.ReadUInt32();

            if (emblemLength > 0 && reader.Remaining >= emblemLength)
            {
                var emblemBytes = reader.ReadBytes(emblemLength);
                await clanService.SetEmblemAsync(clanIdentifier, emblemBytes, cancellationToken);
            }
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.SetClanEmblemResult, null, cancellationToken);
    }
}
