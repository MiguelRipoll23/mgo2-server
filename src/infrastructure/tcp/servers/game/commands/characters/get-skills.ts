import { injectable } from "@needle-di/core";
import { GameCommandHandler } from "../../../../../../core/tcp/decorators/game-command-handler-decorator.ts";
import type { ICommandHandler } from "../../../../../../core/tcp/interfaces/command-handler-interface.ts";
import type { TcpSession } from "../../../../../../core/tcp/types/session-type.ts";
import type { Packet } from "../../../../../../core/tcp/types/packet-type.ts";
import { PacketWriter } from "../../../../../../core/tcp/utils/packet-builder-util.ts";
import { maxLevelSkills } from "../../../../../../modules/character/character-service.ts";
import { sendPacket } from "../../../../../../core/tcp/utils/session-helpers-util.ts";

// The skill catalogue (0x4125): every defined skill, at its max level.
// Skill levels are NOT persisted — this fixed catalogue is the character's
// whole skill state (see maxLevelSkills in CharacterService for the level
// math: experience >> 13 capped at 3, legal maximum 24576, skill 17 capped
// at level 1 by its missing experience path). A missing record would be a
// zeroed client slot, so all 17 defined skills are always served.
@injectable()
@GameCommandHandler(0x4125)
export class GetSkillsHandler implements ICommandHandler {
  async handle(session: TcpSession, _packet: Packet): Promise<void> {
    const skills = maxLevelSkills();

    const writer = new PacketWriter();
    writer.writeUint32(skills.length);
    for (const skill of skills) {
      writer.writeUint8(skill.skill_id);
      writer.writeUint16(skill.experience);
      writer.writeUint8(skill.flag);
    }

    await sendPacket(session, 0x4125, writer.build());
  }
}
