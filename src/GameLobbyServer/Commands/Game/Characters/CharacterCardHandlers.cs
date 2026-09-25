using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Domain.Characters;
using Mgo2Server.Shared.Interfaces;
using Mgo2Server.Shared.Types;
using Mgo2Server.Shared.Utils;

namespace Mgo2Server.GameLobbyServer.Commands.Game.Characters;

/// <summary>
/// Serves another character's card: the reply to the player-details request (<c>0x4220</c>).
/// A character that no longer exists gets the client's own deleted code rather than a card
/// built from whatever the row still holds — see <see cref="CharacterCardPayloadBuilder"/>.
/// </summary>
/// <param name="characterService">Service that owns the character records.</param>
/// <param name="sessionHelper">Helper used to write the replies.</param>
public sealed class GetCharacterCardHandler(
    CharacterService characterService,
    SessionHelper sessionHelper) : ICommandHandler
{
    /// <inheritdoc />
    public async Task HandleAsync(TcpSession session, Packet packet, CancellationToken cancellationToken)
    {
        var reader = new PacketReader(packet.Payload);
        if (reader.Remaining < sizeof(uint))
        {
            // A request that names nobody gets the generic refusal, never the deleted one:
            // a truncated packet is this side's fault, and the client would print it as the
            // player's character having been deleted — a specific lie in place of a vague
            // truth. The client's code for a character that is not there is -266.
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetCharacterCardResult,
                CharacterCardPayloadBuilder.BuildResult(ErrorCodeConstants.ResultGeneral),
                cancellationToken);
            return;
        }

        var targetIdentifier = (int)reader.ReadUInt32();
        var character = targetIdentifier > 0
            ? await characterService.FindByIdAsync(targetIdentifier, cancellationToken)
            : null;

        // A deleted character keeps its row: the soft delete clears the active flag and
        // renames the character, so the lookup finds one and the flag is the only thing that
        // says it is no longer a player. Both a deleted character and an identifier that was
        // never issued get the client's own "has been deleted and no longer exists" — the
        // one value it renders as a sentence of its own, so a stale roster row or a search
        // result from before the deletion says something true rather than "unable to
        // acquire character information" over an empty card.
        if (character is null || !character.Active)
        {
            await sessionHelper.SendPacketAsync(
                session,
                CommandConstants.GetCharacterCardResult,
                CharacterCardPayloadBuilder.BuildResult(ErrorCodeConstants.ResultCharacterGone),
                cancellationToken);
            return;
        }

        var clan = await characterService.GetClanInformationAsync(targetIdentifier, cancellationToken);
        await sessionHelper.SendPacketAsync(
            session,
            CommandConstants.GetCharacterCardResult,
            CharacterCardPayloadBuilder.Build(character, targetIdentifier, clan),
            cancellationToken);
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
        if (session.CharacterIdentifier is null)
        {
            await sessionHelper.SendPacketAsync(session, CommandConstants.GetPostGameInfoResult, null, cancellationToken);
            return;
        }

        var characterIdentifier = session.CharacterIdentifier.Value;

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
            writer.WriteUInt16(CharacterSkillCatalogue.MaximumSkillExperience);
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
