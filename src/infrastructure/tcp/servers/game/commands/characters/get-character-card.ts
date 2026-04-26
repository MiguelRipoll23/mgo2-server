import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader, PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { CharacterStatsService } from "../../../../../../modules/character/character-stats-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const CHAR_CARD_BUFFER_SIZE = 207;

@injectable()
@GameCommandHandler(0x4220)
export class GetCharacterCardHandler implements ICommandHandler {
  constructor(
    private characterService = inject(CharacterService),
    private statsService = inject(CharacterStatsService),
  ) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const reader = new PacketReader(packet.payload);
    const targetCharacterId = reader.readUint32();

    const [character, stats, clan] = await Promise.all([
      targetCharacterId > 0 ? this.characterService.findById(targetCharacterId) : null,
      targetCharacterId > 0 ? this.statsService.findByCharacterId(targetCharacterId) : null,
      targetCharacterId > 0 ? this.characterService.getClanInfo(targetCharacterId) : null,
    ]);

    const experience = character?.experience ?? 0;
    const totalReward = stats?.score ?? 0;
    const comment = character?.comment ?? "";
    const hasClan = clan != null;
    const clanTag = hasClan ? `;${clan.clanName}` : "";

    const writer = new PacketWriter();
    writer.writeUint32(0);                                        // reserved
    writer.writeUint32(targetCharacterId);                        // character id
    writer.writeFixedString(character?.name ?? "", 16);           // name

    writer.writeUint16(0);
    writer.writeUint16(experience);                               // points (experience)
    writer.writeUint16(0);

    writer.writeUint32(totalReward);                              // total reward / score
    writer.writeUint32(0);                                        // playtime (placeholder)

    writer.writeUint8(0);
    writer.writeFixedString(comment, 127);                        // comment

    writer.writeUint16(0);
    writer.writeUint8(0);
    writer.writeUint8(hasClan ? 0x12 : 0x00);                    // clan flag
    writer.writeFixedString(clanTag, 13);                         // clan tag
    writer.writeUint8(0);

    writer.writeUint32(hasClan ? 1 : 0);                         // has clan
    writer.writeUint32(0);
    writer.writeUint32(0);
    writer.writeUint8(hasClan && clan.hasEmblem ? 3 : 0);        // has emblem
    writer.writeUint32(experience);                               // points (again as uint32)
    writer.writeUint32(0x0F00);
    writer.writeUint16(0x0100);

    if (writer.size < CHAR_CARD_BUFFER_SIZE) {
      writer.writePadding(CHAR_CARD_BUFFER_SIZE - writer.size);
    }

    await sendPacket(session, 0x4221, writer.build());
  }
}
