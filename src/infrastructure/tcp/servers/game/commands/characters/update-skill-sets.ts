import { injectable, inject } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketReader } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { CharacterService } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

const SKILL_SET_NAME_LENGTH = 63;
/** u32 modes + 5 skill bytes + 5 level bytes + 63-byte name. */
const SKILL_SET_RECORD_SIZE = 4 + 5 + 5 + SKILL_SET_NAME_LENGTH;

/**
 * 0x4141 — the client's skill-set save. In this build the request is
 * three records of { u32 modes, 5×u8 skill ids, 5×u8 levels, 63-byte name }
 * (builder 0xf0c6ec: one 0x50-stride loop, append-bytes for the id/level
 * runs), and the ack is parsed on the SAME id 0x4141 (waiter 0xf06bfc reads
 * one u32 result). Replying 0x4142 left the client's request slot hanging.
 */
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
      while (reader.remaining() >= SKILL_SET_RECORD_SIZE) {
        const modes = reader.readUint32();
        const skills = [
          reader.readUint8(),
          reader.readUint8(),
          reader.readUint8(),
          reader.readUint8(),
          reader.readUint8(),
        ];
        const levels = [
          reader.readUint8(),
          reader.readUint8(),
          reader.readUint8(),
          reader.readUint8(),
          reader.readUint8(),
        ];
        const name = reader.readFixedString(SKILL_SET_NAME_LENGTH);

        // The wire carries five skill/level slots; storage models four.
        // The fifth slot is part of the record and is consumed either way.
        sets.push({
          idx: index,
          name,
          modes,
          skill_1: skills[0],
          skill_2: skills[1],
          skill_3: skills[2],
          skill_4: skills[3],
          level_1: levels[0],
          level_2: levels[1],
          level_3: levels[2],
          level_4: levels[3],
        });
        index++;
      }

      await this.characterService.updateSkillSets(characterId, sets);
    }
    // Ack on the request's own id; the waiter reads the first u32 as result.
    await sendPacket(session, 0x4141, new Uint8Array(4));
  }
}
