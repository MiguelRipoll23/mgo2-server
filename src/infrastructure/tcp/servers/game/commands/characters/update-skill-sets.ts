import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const SKILL_SET_NAME_LENGTH = 63;

@injectable()
@GameCommandHandler(0x4141)
export class UpdateSkillSetsHandler implements ICommandHandler {
  constructor(private characterService = inject(CharacterService)) {}

  async handle(session: TcpSession, packet: Packet): Promise<void> {
    const characterId = session.characterId;
    if (characterId !== null && packet.payload.length > 0) {
      const reader = new PacketReader(packet.payload);
      const sets: Array<{
        idx: number;
        name: string;
        modes: number;
        skill_1: number;
        skill_2: number;
        skill_3: number;
        skill_4: number;
        level_1: number;
        level_2: number;
        level_3: number;
        level_4: number;
      }> = [];

      let index = 0;
      while (reader.remaining() >= SKILL_SET_NAME_LENGTH + 9 * 4) {
        const name = reader.readFixedString(SKILL_SET_NAME_LENGTH);
        const modes = reader.readUint32();
        const skill_1 = reader.readUint32();
        const skill_2 = reader.readUint32();
        const skill_3 = reader.readUint32();
        const skill_4 = reader.readUint32();
        const level_1 = reader.readUint32();
        const level_2 = reader.readUint32();
        const level_3 = reader.readUint32();
        const level_4 = reader.readUint32();
        sets.push({
          idx: index,
          name,
          modes,
          skill_1,
          skill_2,
          skill_3,
          skill_4,
          level_1,
          level_2,
          level_3,
          level_4,
        });
        index++;
      }

      await this.characterService.updateSkillSets(characterId, sets);
    }
    await sendPacket(session, 0x4142, null);
  }
}
