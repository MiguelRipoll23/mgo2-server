using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>Serves another character's card.</summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="statisticsService">Service that owns the lifetime statistics.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetCharacterCardHandler(
    CharacterService characterService,
    CharacterStatisticsService statisticsService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Exact size of the card payload.</summary>
    private const int CardBufferSize = 207;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        var targetIdentifier = (int)reader.ReadUInt32();

        var character = targetIdentifier > 0
            ? await characterService.FindByIdAsync(targetIdentifier, cancellationToken)
            : null;
        var statistics = targetIdentifier > 0
            ? await statisticsService.FindByCharacterIdentifierAsync(targetIdentifier, cancellationToken)
            : null;
        var clan = targetIdentifier > 0
            ? await characterService.GetClanInformationAsync(targetIdentifier, cancellationToken)
            : null;

        var experience = character?.Experience ?? 0;
        var totalReward = statistics?.Score ?? 0;
        var hasClan = clan is not null;
        var clanTag = hasClan ? $";{clan!.ClanName}" : string.Empty;

        var writer = new PacketWriter();
        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)targetIdentifier);
        writer.WriteFixedString(character?.Name ?? string.Empty, 16);
        writer.WriteUInt16(0);
        writer.WriteUInt16(experience);
        writer.WriteUInt16(0);
        writer.WriteUInt32((uint)totalReward);
        writer.WriteUInt32(0);
        writer.WriteUInt8(0);
        writer.WriteFixedString(character?.Comment ?? string.Empty, 127);
        writer.WriteUInt16(0);
        writer.WriteUInt8(0);
        writer.WriteUInt8(hasClan ? 0x12 : 0x00);
        writer.WriteFixedString(clanTag, 13);
        writer.WriteUInt8(0);
        writer.WriteUInt32(hasClan ? 1u : 0u);
        writer.WriteUInt32(0);
        writer.WriteUInt32(0);
        writer.WriteUInt8(hasClan && clan!.HasEmblem ? 3 : 0);
        writer.WriteUInt32((uint)experience);
        writer.WriteUInt32(0x0F00);
        writer.WriteUInt16(0x0100);

        if (writer.Size < CardBufferSize)
        {
            writer.WritePadding(CardBufferSize - writer.Size);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.GetCharacterCardResult, writer.Build(), cancellationToken);
    }
}

/// <summary>Serves the post-game statistics screen data.</summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetPostGameInfoHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <summary>Exact size of the post-game payload.</summary>
    private const int BufferSize = 0x8b;

    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        if (session.CharacterIdentifier is not { } characterIdentifier)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetPostGameInfoResult, null, cancellationToken);
            return;
        }

        var character = await characterService.FindByIdAsync(characterIdentifier, cancellationToken);
        var clan = await characterService.GetClanInformationAsync(characterIdentifier, cancellationToken);

        var experience = character?.Experience ?? 0;
        var writer = new PacketWriter();
        writer.WriteUInt32(0);
        writer.WriteUInt8(character?.Rank ?? 0);
        writer.WriteUInt32((uint)experience);
        writer.WriteUInt8(0);

        // The skill table is the same fixed catalogue the skill command serves.
        writer.WriteUInt32(CharacterSkillCatalogue.DefinedSkillCount);
        for (var identifier = 1; identifier <= CharacterSkillCatalogue.DefinedSkillCount; identifier++)
        {
            writer.WriteUInt8(identifier);
            writer.WriteUInt16(CharacterSkillCatalogue.HasProgressionPath(identifier)
                ? CharacterSkillCatalogue.MaximumSkillExperience
                : CharacterSkillCatalogue.SkillExperienceWithoutPath);
            writer.WriteUInt8(0);
        }

        writer.WriteUInt32(0);
        writer.WriteUInt32((uint)experience);
        writer.WriteUInt32(0xffffff);
        writer.WriteUInt32((uint)(clan?.ClanIdentifier ?? 0));
        writer.WriteUInt16(0);
        writer.WriteUInt8(1);
        writer.WriteUInt8(clan?.HasEmblem == true ? 3 : 0);
        writer.WriteUInt32((uint)characterIdentifier);
        writer.WriteUInt8(0);

        if (writer.Size < BufferSize)
        {
            writer.WritePadding(BufferSize - writer.Size);
        }

        await sessionHelper.SendPacketAsync(session, CommandConstants.GetPostGameInfoResult, writer.Build(), cancellationToken);
    }
}
